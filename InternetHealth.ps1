# ===================================================================
# Internet Health Monitor - Friendly GUI (PowerShell + WPF)
# ===================================================================
# Version: 1.0.0
# Author: jcardila
# Repository: https://github.com/jcardila/internet-health-monitor
# Last Updated: November 24, 2025
# License: MIT
#
# TESTED ON:
#   - Windows 11 (Build 26100)
#   - PowerShell 7.x
#   - .NET Framework 4.7.2+
#
# REQUIREMENTS:
#   - Windows 10 (version 1809+) or Windows 11
#   - PowerShell 5.1 or higher
#   - .NET Framework 4.7.2 or higher (included in Windows)
#   - No administrator rights required
#
# HOW TO RUN:
#   Method 1: Double-click "RUN_ME.bat" (easiest)
#   Method 2: Right-click this file > "Run with PowerShell"
#   Method 3: Open PowerShell and run:
#             powershell -ExecutionPolicy Bypass -File ".\InternetHealth.ps1"
#
# WHAT IT DOES:
#   - Monitors your router/gateway connection quality
#   - Monitors your internet connection quality
#   - Provides real-time diagnostics and troubleshooting steps
#   - Shows latency graphs and packet loss statistics
#
# ===================================================================
#
# ===================================================================
# CONFIGURATION SECTION
# ===================================================================
# Default configuration values (can be overridden by config.json)
# --------------------------------------------------------------

# Default values
$SampleWindow = 30      # how many recent samples to keep in the rolling window
$PingIntervalMs = 1500    # how often to ping (milliseconds)
$HighLatencyMsRouter = 30      # treat router pings > this ms as "loss" (user experience will feel bad)
$HighLatencyMsInternet = 150     # treat internet pings > this ms as "loss"
$PingTimeoutMs = 1200    # per-ping timeout
$InternetTargets = @("8.8.8.8", "8.8.4.4")  # rotate through these for internet reachability
$WindowTitle = "Internet Health Monitor"
$LogMaxLines = 500     # cap the log length
$LogAllPings = $true   # set to $true to log every ping (for debugging)
$FallbackGateway = "192.168.1.1"  # fallback if auto-detection fails

# Try to load config.json if it exists
$configPath = Join-Path $PSScriptRoot "config.json"
if (Test-Path $configPath) {
    try {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        
        # Override defaults with config file values
        if ($config.monitoring.sampleWindow) { $SampleWindow = $config.monitoring.sampleWindow }
        if ($config.monitoring.pingIntervalMs) { $PingIntervalMs = $config.monitoring.pingIntervalMs }
        if ($config.monitoring.pingTimeoutMs) { $PingTimeoutMs = $config.monitoring.pingTimeoutMs }
        
        if ($config.thresholds.router.highLatencyMs) { $HighLatencyMsRouter = $config.thresholds.router.highLatencyMs }
        if ($config.thresholds.internet.highLatencyMs) { $HighLatencyMsInternet = $config.thresholds.internet.highLatencyMs }
        
        if ($config.targets.internet) { $InternetTargets = $config.targets.internet }
        
        if ($config.ui.windowTitle) { $WindowTitle = $config.ui.windowTitle }
        if ($config.ui.logMaxLines) { $LogMaxLines = $config.ui.logMaxLines }
        if ($null -ne $config.ui.logAllPings) { $LogAllPings = $config.ui.logAllPings }
        
        if ($config.advanced.fallbackGateway) { $FallbackGateway = $config.advanced.fallbackGateway }
        
        Write-Host "Configuration loaded from config.json" -ForegroundColor Green
    }
    catch {
        Write-Host "Warning: Could not parse config.json, using default values" -ForegroundColor Yellow
        Write-Host "Error: $_" -ForegroundColor Gray
    }
}

# --------------------------------------------------------------

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 5) {
    [System.Windows.Forms.MessageBox]::Show(
        "This script requires PowerShell 5.1 or higher.`n`nYour version: $($PSVersionTable.PSVersion)`n`nPlease update PowerShell.",
        "Version Error",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    )
    exit 1
}

# Load required assemblies with error handling
try {
    Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase -ErrorAction Stop
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
}
catch {
    Write-Host "ERROR: Failed to load required .NET assemblies." -ForegroundColor Red
    Write-Host "This script requires .NET Framework 4.7.2 or higher." -ForegroundColor Yellow
    Write-Host "`nError details: $_" -ForegroundColor Gray
    Write-Host "`nPress any key to exit..." -ForegroundColor Cyan
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

function Get-DefaultGateway {
    try {
        $route = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction Stop | Sort-Object RouteMetric | Select-Object -First 1
        if ($route -and $route.NextHop) { return $route.NextHop }
    }
    catch {}
    # Fallback to ipconfig parsing
    $gw = ipconfig | Select-String -Pattern 'Puerta de enlace predeterminada|Default Gateway' -Context 0, 2 | ForEach-Object {
        ($_ | Select-Object -ExpandProperty Line).Split(':')[-1].Trim()
    } | Where-Object { $_ -match '^(\d{1,3}\.){3}\d{1,3}$' } | Select-Object -First 1
    return $gw
}

# Simple helper to create a colored status pill
function New-StatusPill([string]$text, [string]$color) {
    @"
    <Border Background="$color" CornerRadius="10" Padding="6,2" Margin="6,0,0,0">
        <TextBlock Text="$text" Foreground="White" FontWeight="SemiBold"/>
    </Border>
"@
}

# Build XAML UI
[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="$WindowTitle" Height="820" Width="800" WindowStartupLocation="CenterScreen"
        Background="#0b1220">
  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="280"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
    </Grid.RowDefinitions>

    <!-- Header -->
    <StackPanel Orientation="Horizontal" Grid.Row="0" VerticalAlignment="Center">
      <TextBlock Text="Internet Health Monitor" Foreground="White" FontSize="22" FontWeight="Bold"/>
      <Border Background="#1f2937" CornerRadius="8" Padding="8" Margin="12,0,0,0">
        <TextBlock x:Name="GatewayText" Text="Router: " Foreground="#93c5fd" FontSize="12"/>
      </Border>
      <Border Background="#1f2937" CornerRadius="8" Padding="8" Margin="6,0,0,0">
        <TextBlock x:Name="TargetsText" Text="Targets: " Foreground="#93c5fd" FontSize="12"/>
      </Border>
    </StackPanel>

    <!-- Cards -->
    <Grid Grid.Row="1">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>

      <!-- Router Card -->
      <Border Grid.Column="0" CornerRadius="14" Background="#111827" Padding="14" Margin="0,10,8,10">
        <StackPanel>
          <TextBlock Text="Conexion al Router" Foreground="White" FontSize="18" FontWeight="SemiBold"/>
          <StackPanel Orientation="Horizontal" Margin="0,8,0,8" VerticalAlignment="Center">
            <Ellipse x:Name="RouterDot" Width="16" Height="16" Fill="#6b7280" />
            <TextBlock Text="  Estado:" Foreground="#d1d5db" FontSize="14"/>
            <TextBlock x:Name="RouterState" Text="-" Foreground="White" FontSize="14" FontWeight="Bold" Margin="6,0,0,0"/>
          </StackPanel>
          <UniformGrid Columns="2" Rows="2" Margin="0,4,0,0">
            <TextBlock Text="Promedio (ms)" Foreground="#9ca3af"/><TextBlock x:Name="RouterAvg" Text="-" Foreground="White" HorizontalAlignment="Right"/>
            <TextBlock Text="Perdida efectiva" Foreground="#9ca3af"/><TextBlock x:Name="RouterLoss" Text="-" Foreground="White" HorizontalAlignment="Right"/>
          </UniformGrid>
          
          <!-- Mini Graph Router -->
          <Border Background="#0f172a" CornerRadius="8" Padding="6" Margin="0,10,0,0" Height="64">
            <Canvas x:Name="RouterChart" Width="290" Height="52" HorizontalAlignment="Stretch" VerticalAlignment="Stretch"/>
          </Border>
          
          <TextBlock Text="Si aqui sale bien y Conexión a Internet sale mal, tu Wi-Fi/cable esta OK; el problema es el Internet del router." Foreground="#93c5fd" FontSize="11" Margin="0,8,0,0" TextWrapping="Wrap"/>
        </StackPanel>
      </Border>

      <!-- Internet Card -->
      <Border Grid.Column="1" CornerRadius="14" Background="#111827" Padding="14" Margin="8,10,0,10">
        <StackPanel>
          <TextBlock Text="Conexion a Internet" Foreground="White" FontSize="18" FontWeight="SemiBold"/>
          <StackPanel Orientation="Horizontal" Margin="0,8,0,8" VerticalAlignment="Center">
            <Ellipse x:Name="NetDot" Width="16" Height="16" Fill="#6b7280" />
            <TextBlock Text="  Estado:" Foreground="#d1d5db" FontSize="14"/>
            <TextBlock x:Name="NetState" Text="-" Foreground="White" FontSize="14" FontWeight="Bold" Margin="6,0,0,0"/>
          </StackPanel>
          <UniformGrid Columns="2" Rows="2" Margin="0,4,0,0">
            <TextBlock Text="Promedio (ms)" Foreground="#9ca3af"/><TextBlock x:Name="NetAvg" Text="-" Foreground="White" HorizontalAlignment="Right"/>
            <TextBlock Text="Perdida efectiva" Foreground="#9ca3af"/><TextBlock x:Name="NetLoss" Text="-" Foreground="White" HorizontalAlignment="Right"/>
          </UniformGrid>
          
          <!-- Mini Graph Internet -->
          <Border Background="#0f172a" CornerRadius="8" Padding="6" Margin="0,10,0,0" Height="64">
            <Canvas x:Name="InternetChart" Width="290" Height="52" HorizontalAlignment="Stretch" VerticalAlignment="Stretch"/>
          </Border>
          
          <TextBlock Text="Si aqui sale mal y el Router sale mal, primero debes revisar la conexion a tu Router. Si aqui sale mal y el Router sale bien; el problema es entre el Router y el Internet." Foreground="#93c5fd" FontSize="11" Margin="0,8,0,0" TextWrapping="Wrap"/>
        </StackPanel>
      </Border>
    </Grid>

    <!-- Diagnostic Panel -->
    <Border Grid.Row="2" x:Name="DiagnosticPanel" CornerRadius="14" Padding="16" Margin="0,10,0,10" Background="#1a1f2e" Height="180">
      <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>
          <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="&#x1F4A1;" FontSize="20" Margin="0,0,8,0"/>
            <TextBlock Text="Diagnostico y Recomendaciones" Foreground="White" FontSize="16" FontWeight="SemiBold" VerticalAlignment="Center"/>
          </StackPanel>
          <TextBlock x:Name="DiagnosticTitle" Text="Analizando..." Foreground="#fbbf24" FontSize="14" FontWeight="Bold" Margin="0,0,0,6"/>
          <TextBlock x:Name="DiagnosticMessage" Text="Recopilando datos de red..." Foreground="#d1d5db" FontSize="13" TextWrapping="Wrap" Margin="0,0,0,8"/>
          <TextBlock x:Name="DiagnosticSteps" Text="" Foreground="#93c5fd" FontSize="12" TextWrapping="Wrap" Margin="0,0,0,0"/>
        </StackPanel>
      </ScrollViewer>
    </Border>

    <!-- Log -->
    <Border Grid.Row="3" CornerRadius="14" Background="#0f172a" Padding="10" Margin="0,0,0,0">
      <DockPanel LastChildFill="True">
        <StackPanel Orientation="Horizontal" DockPanel.Dock="Top">
          <TextBlock Text="Registro" Foreground="White" FontWeight="SemiBold" FontSize="16"/>
          <Button x:Name="BtnClear" Content="Limpiar" Margin="10,0,0,0" Padding="8,4" />
          <Button x:Name="BtnCopy" Content="Copiar" Margin="6,0,0,0" Padding="8,4" />
          <Button x:Name="BtnExport" Content="Exportar Reporte" Margin="6,0,0,0" Padding="8,4" />
        </StackPanel>
        <ScrollViewer x:Name="LogScroller" VerticalScrollBarVisibility="Auto" Margin="0,6,0,0">
          <TextBox x:Name="LogBox" Text="" Foreground="#e5e7eb" Background="#0f172a" BorderBrush="#1f2937"
                   FontFamily="Consolas" FontSize="12" IsReadOnly="True" AcceptsReturn="True" TextWrapping="Wrap"/>
        </ScrollViewer>
      </DockPanel>
    </Border>
  </Grid>
</Window>
"@

# Parse XAML with error handling
try {
    $reader = (New-Object System.Xml.XmlNodeReader $xaml)
    $window = [Windows.Markup.XamlReader]::Load($reader)
}
catch {
    Write-Host "ERROR: Failed to create the user interface." -ForegroundColor Red
    Write-Host "The XAML markup could not be parsed." -ForegroundColor Yellow
    Write-Host "`nError details: $_" -ForegroundColor Gray
    Write-Host "`nThis might be due to:" -ForegroundColor Cyan
    Write-Host "  - Corrupted script file" -ForegroundColor Gray
    Write-Host "  - Missing .NET components" -ForegroundColor Gray
    Write-Host "  - Incompatible Windows version" -ForegroundColor Gray
    Write-Host "`nPress any key to exit..." -ForegroundColor Cyan
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Bind controls with error checking
try {
    $RouterDot = $window.FindName("RouterDot")
    $RouterState = $window.FindName("RouterState")
    $RouterAvg = $window.FindName("RouterAvg")
    $RouterLoss = $window.FindName("RouterLoss")
    $RouterChart = $window.FindName("RouterChart")

    $NetDot = $window.FindName("NetDot")
    $NetState = $window.FindName("NetState")
    $NetAvg = $window.FindName("NetAvg")
    $NetLoss = $window.FindName("NetLoss")
    $InternetChart = $window.FindName("InternetChart")

    $GatewayText = $window.FindName("GatewayText")
    $TargetsText = $window.FindName("TargetsText")
    $DiagnosticPanel = $window.FindName("DiagnosticPanel")
    $DiagnosticTitle = $window.FindName("DiagnosticTitle")
    $DiagnosticMessage = $window.FindName("DiagnosticMessage")
    $DiagnosticSteps = $window.FindName("DiagnosticSteps")
    $LogBox = $window.FindName("LogBox")
    $LogScroller = $window.FindName("LogScroller")
    $BtnClear = $window.FindName("BtnClear")
    $BtnCopy = $window.FindName("BtnCopy")
    $BtnExport = $window.FindName("BtnExport")
    
    # Verify critical controls were found
    if (-not $RouterDot -or -not $NetDot -or -not $LogBox) {
        throw "One or more critical UI controls could not be found"
    }
}
catch {
    Write-Host "ERROR: Failed to bind UI controls." -ForegroundColor Red
    Write-Host "`nError details: $_" -ForegroundColor Gray
    Write-Host "`nPress any key to exit..." -ForegroundColor Cyan
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Utility: append to log
function Add-Log([string]$msg) {
    $timestamp = (Get-Date).ToString("HH:mm:ss")
    $line = "[$timestamp] $msg"
    $LogBox.AppendText($line + [Environment]::NewLine)
    # cap lines
    $lines = $LogBox.LineCount
    if ($lines -gt $LogMaxLines) {
        # remove top 50 lines
        $text = $LogBox.Text
        $split = $text -split "`r?`n"
        $LogBox.Text = ($split[ - $LogMaxLines..-1] -join [Environment]::NewLine)
    }
    # Force scroll to bottom
    $LogScroller.ScrollToBottom()
    $LogBox.CaretIndex = $LogBox.Text.Length
}

# Utility: draw mini chart (smooth curved line with gradient colors)
function New-MiniChart($canvas, $dataList, $thresholdLatency) {
    $canvas.Children.Clear()
    
    $pointCount = $dataList.Count
    if ($pointCount -eq 0) { return }
    
    $canvasWidth = 290
    $canvasHeight = 52
    $padding = 4
    $graphWidth = $canvasWidth - ($padding * 2)
    $graphHeight = $canvasHeight - ($padding * 2)
    
    # Find the maximum and minimum value in the data (excluding timeouts) for dynamic scaling
    $validLatencies = @($dataList | Where-Object { $_ -ge 0 })
    
    if ($validLatencies.Count -eq 0) {
        # All timeouts - show message
        $textBlock = New-Object Windows.Controls.TextBlock
        $textBlock.Text = "Sin datos"
        $textBlock.Foreground = New-Object Windows.Media.SolidColorBrush ([Windows.Media.ColorConverter]::ConvertFromString("#ef4444"))
        $textBlock.FontSize = 12
        [Windows.Controls.Canvas]::SetLeft($textBlock, $canvasWidth / 2 - 30)
        [Windows.Controls.Canvas]::SetTop($textBlock, $canvasHeight / 2 - 8)
        $canvas.Children.Add($textBlock) | Out-Null
        return
    }
    
    $maxDataValue = ($validLatencies | Measure-Object -Maximum).Maximum
    $minDataValue = ($validLatencies | Measure-Object -Minimum).Minimum
    
    # Calculate dynamic scale with padding for smooth visualization
    $dataRange = $maxDataValue - $minDataValue
    if ($dataRange -lt 3) { $dataRange = 3 }  # Minimum range for visibility
    
    $scaleMin = [math]::Max(0, $minDataValue - ($dataRange * 0.3))
    $scaleMax = $maxDataValue + ($dataRange * 0.3)
    
    # Function to get color based on latency value
    function Get-LatencyColor($latency, $threshold) {
        if ($latency -lt 0) {
            return "#ef4444"  # Red for timeout
        }
        
        if ($latency -gt $threshold) {
            return "#ef4444"  # Red - above threshold
        }
        elseif ($latency -gt ($threshold / 2)) {
            return "#f59e0b"  # Orange - warning
        }
        else {
            return "#10b981"  # Green - good
        }
    }
    
    # Create points array with coordinates
    $pointsData = @()
    
    # Handle case of single point
    if ($pointCount -eq 1) {
        $xStep = 0
    }
    else {
        $xStep = $graphWidth / ($pointCount - 1)
    }
    
    for ($i = 0; $i -lt $pointCount; $i++) {
        $latency = $dataList[$i]
        
        if ($latency -lt 0) {
            if ($pointsData.Count -gt 0) {
                $x = $padding + ($i * $xStep)
                $pointsData += @{
                    X       = $x
                    Y       = $pointsData[-1].Y
                    Latency = $latency
                    Color   = "#ef4444"
                }
            }
        }
        else {
            $normalizedValue = ($latency - $scaleMin) / ($scaleMax - $scaleMin)
            $y = $padding + ($graphHeight - ($normalizedValue * $graphHeight))
            $x = $padding + ($i * $xStep)
            
            $pointsData += @{
                X       = $x
                Y       = $y
                Latency = $latency
                Color   = Get-LatencyColor $latency $thresholdLatency
            }
        }
    }
    
    if ($pointsData.Count -lt 2) { return }
    
    # Draw smooth curved path with color segments
    for ($i = 0; $i -lt ($pointsData.Count - 1); $i++) {
        $pt1 = $pointsData[$i]
        $pt2 = $pointsData[$i + 1]
        
        # Calculate control points for Bezier curve (smooth)
        $dx = $pt2.X - $pt1.X
        $cp1x = $pt1.X + ($dx * 0.5)
        $cp1y = $pt1.Y
        $cp2x = $pt2.X - ($dx * 0.5)
        $cp2y = $pt2.Y
        
        # Create path with Bezier curve
        $path = New-Object Windows.Shapes.Path
        $pathGeometry = New-Object Windows.Media.PathGeometry
        $pathFigure = New-Object Windows.Media.PathFigure
        $pathFigure.StartPoint = New-Object Windows.Point($pt1.X, $pt1.Y)
        
        $bezierSegment = New-Object Windows.Media.BezierSegment
        $bezierSegment.Point1 = New-Object Windows.Point($cp1x, $cp1y)
        $bezierSegment.Point2 = New-Object Windows.Point($cp2x, $cp2y)
        $bezierSegment.Point3 = New-Object Windows.Point($pt2.X, $pt2.Y)
        
        $pathFigure.Segments.Add($bezierSegment)
        $pathGeometry.Figures.Add($pathFigure)
        $path.Data = $pathGeometry
        
        # Create gradient brush from pt1 color to pt2 color
        $gradientBrush = New-Object Windows.Media.LinearGradientBrush
        $gradientBrush.StartPoint = New-Object Windows.Point(0, 0)
        $gradientBrush.EndPoint = New-Object Windows.Point(1, 0)
        
        $stop1 = New-Object Windows.Media.GradientStop
        $stop1.Color = [Windows.Media.ColorConverter]::ConvertFromString($pt1.Color)
        $stop1.Offset = 0.0
        
        $stop2 = New-Object Windows.Media.GradientStop
        $stop2.Color = [Windows.Media.ColorConverter]::ConvertFromString($pt2.Color)
        $stop2.Offset = 1.0
        
        $gradientBrush.GradientStops.Add($stop1)
        $gradientBrush.GradientStops.Add($stop2)
        
        $path.Stroke = $gradientBrush
        $path.StrokeThickness = 3
        $path.StrokeStartLineCap = "Round"
        $path.StrokeEndLineCap = "Round"
        
        $canvas.Children.Add($path) | Out-Null
    }
    
    # Add subtle shadow/glow effect under the curve
    for ($i = 0; $i -lt ($pointsData.Count - 1); $i++) {
        $pt1 = $pointsData[$i]
        $pt2 = $pointsData[$i + 1]
        
        $dx = $pt2.X - $pt1.X
        $cp1x = $pt1.X + ($dx * 0.5)
        $cp1y = $pt1.Y
        $cp2x = $pt2.X - ($dx * 0.5)
        $cp2y = $pt2.Y
        
        # Create filled path for glow effect
        $fillPath = New-Object Windows.Shapes.Path
        $fillGeometry = New-Object Windows.Media.PathGeometry
        $fillFigure = New-Object Windows.Media.PathFigure
        $fillFigure.StartPoint = New-Object Windows.Point($pt1.X, $pt1.Y)
        
        $bezier = New-Object Windows.Media.BezierSegment
        $bezier.Point1 = New-Object Windows.Point($cp1x, $cp1y)
        $bezier.Point2 = New-Object Windows.Point($cp2x, $cp2y)
        $bezier.Point3 = New-Object Windows.Point($pt2.X, $pt2.Y)
        
        $fillFigure.Segments.Add($bezier)
        
        # Close to bottom
        $bottomY = $canvasHeight - $padding
        
        $lineToBottom2 = New-Object Windows.Media.LineSegment
        $lineToBottom2.Point = New-Object Windows.Point($pt2.X, $bottomY)
        $fillFigure.Segments.Add($lineToBottom2)
        
        $lineToBottom1 = New-Object Windows.Media.LineSegment
        $lineToBottom1.Point = New-Object Windows.Point($pt1.X, $bottomY)
        $fillFigure.Segments.Add($lineToBottom1)
        
        $fillFigure.IsClosed = $true
        $fillGeometry.Figures.Add($fillFigure)
        $fillPath.Data = $fillGeometry
        
        # Gradient fill
        $fillGradient = New-Object Windows.Media.LinearGradientBrush
        $fillGradient.StartPoint = New-Object Windows.Point(0, 0)
        $fillGradient.EndPoint = New-Object Windows.Point(1, 0)
        
        $fillStop1 = New-Object Windows.Media.GradientStop
        $fillColor1 = [Windows.Media.ColorConverter]::ConvertFromString($pt1.Color)
        $fillStop1.Color = $fillColor1
        $fillStop1.Offset = 0.0
        
        $fillStop2 = New-Object Windows.Media.GradientStop
        $fillColor2 = [Windows.Media.ColorConverter]::ConvertFromString($pt2.Color)
        $fillStop2.Color = $fillColor2
        $fillStop2.Offset = 1.0
        
        $fillGradient.GradientStops.Add($fillStop1)
        $fillGradient.GradientStops.Add($fillStop2)
        
        $fillPath.Fill = $fillGradient
        $fillPath.Opacity = 0.15
        $fillPath.Stroke = $null
        
        # Insert at beginning so it's behind the line
        $canvas.Children.Insert(0, $fillPath)
    }
}

# Utility: update diagnostic panel
function Update-Diagnostic($routerStatus, $internetStatus) {
    # LOGICA: Si router esta mal, NO podemos saber el estado real de Internet
    # porque todo el trafico pasa por el router. Siempre priorizar arreglar router primero.
    
    if ($routerStatus -eq "OK" -and $internetStatus -eq "OK") {
        # Todo esta bien
        $DiagnosticPanel.Background = "#1a3a1a"  # Verde oscuro
        $DiagnosticTitle.Text = "[OK] TODO FUNCIONA CORRECTAMENTE"
        $DiagnosticTitle.Foreground = "#10b981"
        $DiagnosticMessage.Text = "Tu conexion esta funcionando perfectamente. La senal WiFi o conexion cableada es buena y el Internet esta estable."
        $DiagnosticSteps.Text = ""
        
    }
    elseif ($routerStatus -eq "OK" -and ($internetStatus -eq "Degradado" -or $internetStatus -eq "Problemas")) {
        # Router OK pero Internet mal - ESTE ES EL UNICO CASO donde sabemos que el problema es Internet
        $DiagnosticPanel.Background = "#3a2a1a"  # Naranja oscuro
        $DiagnosticTitle.Text = "[ATENCION] PROBLEMA CON EL SERVICIO DE INTERNET"
        $DiagnosticTitle.Foreground = "#f59e0b"
        $DiagnosticMessage.Text = "Tu computador esta conectado correctamente a la red. La senal WiFi/cable esta bien. El problema es el MODEM o el servicio de Internet del proveedor."
        $DiagnosticSteps.Text = "QUE PUEDES HACER:`n1. REINICIA EL MODEM: Desconecta el cable de corriente del modem, espera 15 segundos, y vuelve a conectarlo. Espera 2-3 minutos a que reinicie completamente.`n2. Verifica que todas las luces del modem esten encendidas correctamente (revisa el manual del modem).`n3. Si el problema persiste por mas de 10 minutos, LLAMA A TU PROVEEDOR DE INTERNET y reporta el problema.`n4. Ten a mano el numero de cuenta y describe que hay perdida de paquetes o alta latencia hacia Internet."
        
    }
    elseif ($routerStatus -eq "Degradado" -or $routerStatus -eq "Problemas") {
        # Router tiene problemas - NO importa que diga Internet, el problema es el router primero
        $DiagnosticPanel.Background = "#3a1a1a"  # Rojo oscuro
        $DiagnosticTitle.Text = "[ATENCION] PROBLEMA CON LA CONEXION AL ROUTER"
        $DiagnosticTitle.Foreground = "#ef4444"
        $DiagnosticMessage.Text = "Tu computador tiene problemas para conectarse al router. Puede ser por senal WiFi debil, interferencia o problemas con el router. IMPORTANTE: Aunque veas problemas de Internet tambien, primero debes solucionar la conexion al router."
        $DiagnosticSteps.Text = "QUE PUEDES HACER (EN ESTE ORDEN):`n`n1. ARREGLA PRIMERO LA CONEXION AL ROUTER:`n   - Si usas WiFi: ACERCATE AL ROUTER o usa un cable de red ethernet.`n   - Aleja el router de microondas, telefonos inalambricos y otros dispositivos electronicos.`n   - Si tu router tiene WiFi de 2.4GHz y 5GHz, prueba cambiando de red.`n   - REINICIA EL ROUTER: Desconecta el cable de corriente, espera 15 segundos, y reconectalo.`n`n2. DESPUES DE MEJORAR LA CONEXION AL ROUTER:`n   - Espera 1-2 minutos y observa si el problema de Internet desaparece.`n   - Si el router ahora marca OK pero Internet sigue con problemas, entonces SI es un problema del proveedor.`n`n3. SI PERSISTE TODO MAL:`n   - Puede ser un problema del modem/router del proveedor.`n   - LLAMA A TU PROVEEDOR DE INTERNET y reporta problemas de conectividad al router y a Internet.`n   - Menciona que ya probaste reiniciar el equipo y acercarte al router."
        
    }
    else {
        # Degradado leve
        $DiagnosticPanel.Background = "#2a2a1a"  # Amarillo oscuro
        $DiagnosticTitle.Text = "[AVISO] CONEXION DEGRADADA"
        $DiagnosticTitle.Foreground = "#fbbf24"
        $DiagnosticMessage.Text = "La conexion funciona pero con calidad reducida. Puede haber lentitud o cortes ocasionales."
        $DiagnosticSteps.Text = "QUE PUEDES HACER:`n1. Monitorea si mejora en los proximos minutos (puede ser temporal).`n2. Si usas WiFi, acercate mas al router.`n3. Cierra programas que usen mucho Internet (descargas, streaming, videollamadas).`n4. Si persiste, sigue los pasos de los diagnosticos anteriores segun donde este el problema."
    }
}

# Utility: set status visual
function Set-Status($dot, $stateTextBlock, $avgBlock, $lossBlock, [double]$avg, [double]$lossPct) {
    if ($avg -ge 0) {
        $avgStr = [math]::Round($avg, 0).ToString()
    }
    else {
        $avgStr = "-"
    }
    if ($lossPct -ge 0) {
        $lossStr = ([math]::Round($lossPct, 1).ToString() + "%")
    }
    else {
        $lossStr = "-"
    }
    $avgBlock.Text = $avgStr
    $lossBlock.Text = $lossStr

    # Verdict by loss/latency
    if ($lossPct -lt 2 -and $avg -ge 0 -and $avg -lt 50) {
        $color = "#10b981" ; $text = "OK"
    }
    elseif ($lossPct -lt 10) {
        $color = "#f59e0b" ; $text = "Degradado"
    }
    else {
        $color = "#ef4444" ; $text = "Problemas"
    }
    $stateTextBlock.Text = $text
    $dot.Fill = (New-Object Windows.Media.SolidColorBrush ( [Windows.Media.ColorConverter]::ConvertFromString($color) ))
    
    # Return status for diagnostic
    return $text
}

# Rolling buffers (use script scope for timer access)
$script:routerSamples = New-Object System.Collections.ArrayList
$script:routerLatencies = New-Object System.Collections.ArrayList
$script:netSamples = New-Object System.Collections.ArrayList
$script:netLatencies = New-Object System.Collections.ArrayList

# Chart history (last 20 samples for visualization)
$script:routerChartData = New-Object System.Collections.ArrayList
$script:internetChartData = New-Object System.Collections.ArrayList
$ChartMaxBars = 20

# Resolve targets
$script:defaultGw = Get-DefaultGateway
if (-not $script:defaultGw) { $script:defaultGw = $FallbackGateway }  # use configured fallback
$GatewayText.Text = "Router: $script:defaultGw"
$TargetsText.Text = "Targets: " + ($InternetTargets -join ", ")

# Ping helpers
$script:PingObj = New-Object System.Net.NetworkInformation.Ping
$script:targetIndex = 0

function Measure-Ping($target, $timeoutMs) {
    try {
        $reply = $script:PingObj.Send($target, $timeoutMs)
        if ($reply.Status -eq "Success") { return [int]$reply.RoundtripTime }
        return -1
    }
    catch { return -1 }
}

# Timer loop
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds($PingIntervalMs)

$timer.Add_Tick({
        # Router
        try {
            $rttRouter = Measure-Ping $script:defaultGw $PingTimeoutMs
        }
        catch {
            $rttRouter = -1
            Add-Log "ERROR pinging router: $_"
        }
        $isLossRouter = $false
        if ($rttRouter -lt 0 -or $rttRouter -gt $HighLatencyMsRouter) { $isLossRouter = $true }

        if ($isLossRouter) {
            [void]$script:routerSamples.Add(1)
        }
        else {
            [void]$script:routerSamples.Add(0)
        }
        [void]$script:routerLatencies.Add( $rttRouter )

        while ($script:routerSamples.Count -gt $SampleWindow) { $script:routerSamples.RemoveAt(0) }
        while ($script:routerLatencies.Count -gt $SampleWindow) { $script:routerLatencies.RemoveAt(0) }

        $lossRouterPct = if ($script:routerSamples.Count -gt 0) { (100.0 * ($script:routerSamples | Measure-Object -Sum).Sum / $script:routerSamples.Count) } else { -1 }
        $avgRouter = ($script:routerLatencies | Where-Object { $_ -ge 0 -and $_ -le $HighLatencyMsRouter })
        $avgRouterVal = if ($avgRouter.Count -gt 0) { [math]::Round(($avgRouter | Measure-Object -Average).Average, 0) } else { -1 }

        $routerStatusResult = Set-Status -dot $RouterDot -stateTextBlock $RouterState -avgBlock $RouterAvg -lossBlock $RouterLoss -avg $avgRouterVal -lossPct $lossRouterPct

        # Internet (rotate targets)
        $target = $InternetTargets[$script:targetIndex % $InternetTargets.Count]
        $script:targetIndex++
        $rttNet = Measure-Ping $target $PingTimeoutMs
        $isLossNet = $false
        if ($rttNet -lt 0 -or $rttNet -gt $HighLatencyMsInternet) { $isLossNet = $true }

        if ($isLossNet) {
            [void]$script:netSamples.Add(1)
        }
        else {
            [void]$script:netSamples.Add(0)
        }
        [void]$script:netLatencies.Add( $rttNet )

        while ($script:netSamples.Count -gt $SampleWindow) { $script:netSamples.RemoveAt(0) }
        while ($script:netLatencies.Count -gt $SampleWindow) { $script:netLatencies.RemoveAt(0) }

        $lossNetPct = if ($script:netSamples.Count -gt 0) { (100.0 * ($script:netSamples | Measure-Object -Sum).Sum / $script:netSamples.Count) } else { -1 }
        $avgNet = ($script:netLatencies | Where-Object { $_ -ge 0 -and $_ -le $HighLatencyMsInternet })
        $avgNetVal = if ($avgNet.Count -gt 0) { [math]::Round(($avgNet | Measure-Object -Average).Average, 0) } else { -1 }

        $internetStatusResult = Set-Status -dot $NetDot -stateTextBlock $NetState -avgBlock $NetAvg -lossBlock $NetLoss -avg $avgNetVal -lossPct $lossNetPct

        # Update diagnostic panel
        Update-Diagnostic -routerStatus $routerStatusResult -internetStatus $internetStatusResult

        # Update charts
        [void]$script:routerChartData.Add($rttRouter)
        [void]$script:internetChartData.Add($rttNet)
    
        # Keep only last N bars
        while ($script:routerChartData.Count -gt $ChartMaxBars) { $script:routerChartData.RemoveAt(0) }
        while ($script:internetChartData.Count -gt $ChartMaxBars) { $script:internetChartData.RemoveAt(0) }
    
        # Draw charts
        New-MiniChart -canvas $RouterChart -dataList $script:routerChartData -thresholdLatency $HighLatencyMsRouter
        New-MiniChart -canvas $InternetChart -dataList $script:internetChartData -thresholdLatency $HighLatencyMsInternet

        # Log notable events
        if ($LogAllPings) {
            $rtrDisplay = if ($rttRouter -lt 0) { "TIMEOUT" } else { "${rttRouter}ms" }
            $netDisplay = if ($rttNet -lt 0) { "TIMEOUT" } else { "${rttNet}ms" }
            $rLossFmt = if ($lossRouterPct -ge 0) { "$([math]::Round($lossRouterPct,1))%" } else { "N/A" }
            $iLossFmt = if ($lossNetPct -ge 0) { "$([math]::Round($lossNetPct,1))%" } else { "N/A" }
            Add-Log "Router($script:defaultGw): $rtrDisplay | Internet($target): $netDisplay | Loss R:$rLossFmt I:$iLossFmt"
        }
        else {
            if ($rttRouter -lt 0) { Add-Log "Router ($script:defaultGw): sin respuesta" }
            elseif ($rttRouter -gt $HighLatencyMsRouter) { Add-Log "Router ($script:defaultGw): latencia alta ${rttRouter}ms" }

            if ($rttNet -lt 0) { Add-Log "Internet ($target): sin respuesta" }
            elseif ($rttNet -gt $HighLatencyMsInternet) { Add-Log "Internet ($target): latencia alta ${rttNet}ms" }
        }
    })

# Buttons
$BtnClear.Add_Click({ $LogBox.Clear() })
$BtnCopy.Add_Click({
        [System.Windows.Clipboard]::SetText($LogBox.Text)
    })

$BtnExport.Add_Click({
        try {
            # Prepare report data
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $filename = "InternetHealthReport_$timestamp.txt"
            
            # Create save file dialog
            $saveDialog = New-Object System.Windows.Forms.SaveFileDialog
            $saveDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            $saveDialog.FileName = $filename
            $saveDialog.Title = "Guardar Reporte de Internet Health"
            
            if ($saveDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
                $reportPath = $saveDialog.FileName
                
                # Generate report
                $report = @"
========================================
INTERNET HEALTH MONITOR - REPORTE
========================================
Generado: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Version: 1.0.0

CONFIGURACIÓN DE RED:
---------------------
Router/Gateway: $script:defaultGw
Objetivos Internet: $($InternetTargets -join ', ')
Intervalo de ping: ${PingIntervalMs}ms
Ventana de muestras: $SampleWindow
Umbral latencia router: ${HighLatencyMsRouter}ms
Umbral latencia internet: ${HighLatencyMsInternet}ms

ESTADÍSTICAS ACTUALES:
---------------------
ROUTER ($script:defaultGw):
  Estado: $($RouterState.Text)
  Promedio latencia: $($RouterAvg.Text)
  Pérdida efectiva: $($RouterLoss.Text)

INTERNET:
  Estado: $($NetState.Text)
  Promedio latencia: $($NetAvg.Text)
  Pérdida efectiva: $($NetLoss.Text)

DIAGNÓSTICO:
---------------------
$($DiagnosticTitle.Text)
$($DiagnosticMessage.Text)

$($DiagnosticSteps.Text)

REGISTRO COMPLETO:
========================================
$($LogBox.Text)

========================================
Fin del Reporte
========================================
"@
                
                # Save to file
                [System.IO.File]::WriteAllText($reportPath, $report, [System.Text.Encoding]::UTF8)
                
                # Show success message
                Add-Log "Reporte exportado exitosamente a: $reportPath"
                [System.Windows.MessageBox]::Show(
                    "Reporte guardado exitosamente en:`n`n$reportPath",
                    "Exportación Exitosa",
                    [System.Windows.MessageBoxButton]::OK,
                    [System.Windows.MessageBoxImage]::Information
                )
            }
        }
        catch {
            Add-Log "ERROR al exportar reporte: $_"
            [System.Windows.MessageBox]::Show(
                "Error al guardar el reporte:`n`n$_",
                "Error de Exportación",
                [System.Windows.MessageBoxButton]::OK,
                [System.Windows.MessageBoxImage]::Error
            )
        }
    })

# Start
Add-Log "Usando router: $script:defaultGw"
Add-Log "Objetivos Internet: $($InternetTargets -join ', ')"
Add-Log "Muestreo cada ${PingIntervalMs}ms; ventana $SampleWindow; latencia alta Router>${HighLatencyMsRouter}ms; Internet>${HighLatencyMsInternet}ms"
Add-Log "Iniciando monitoreo..."
$timer.Start()

# Show window
$null = $window.ShowDialog()
