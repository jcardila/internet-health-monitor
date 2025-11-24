# Changelog

All notable changes to the Internet Health Monitor project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-11-24

### Added

- 🎉 Initial release of Internet Health Monitor
- ⚡ Real-time monitoring of router/gateway connection quality
- 🌍 Real-time monitoring of internet connection quality
- 📊 Dynamic latency graphs with color-coded visualization (green/yellow/red)
- 📈 Statistical analysis: average latency and effective packet loss
- 🧠 Intelligent diagnostic system that identifies whether problems are local (router) or external (ISP)
- 💡 Step-by-step troubleshooting recommendations based on detected issues
- 🎯 Smart prioritization: warns to fix router issues before blaming ISP
- 📝 Detailed event logging with timestamps
- 💾 Export functionality: save complete reports with statistics and logs
- 🎨 Modern dark-themed professional UI using WPF
- ⚙️ Highly configurable thresholds and monitoring parameters
- 🔧 External JSON configuration file support
- 🚀 Easy-to-use launcher (RUN_ME.bat) that handles execution policies
- 📦 No installation required - fully portable
- 🔒 No administrator privileges needed
- 🌐 Multi-language support (Spanish interface with English documentation)
- ✅ Automatic gateway detection with fallback support
- 📊 Smooth Bezier curve graphs with gradient colors
- 🎨 Real-time color-coded status indicators
- 📋 Copy and clear log functionality

### Technical Features

- PowerShell + WPF (Windows Presentation Foundation)
- .NET Framework 4.7.2+ compatible
- Robust error handling with user-friendly messages
- Version checking for PowerShell compatibility
- XAML-based modern UI
- Rolling window statistical analysis
- Multiple internet target rotation for reliability

### Documentation

- Comprehensive README.md with installation and usage instructions
- Quick start guide (README.es.md) in Spanish
- MIT License
- .gitignore for clean repository management
- Detailed inline code comments
- Configuration examples for different DNS providers

### Tested On

- Windows 11 (Build 26100)
- PowerShell 7.x
- .NET Framework 4.7.2+

---

## [Unreleased]

### Planned Features

- [ ] Convert to standalone .exe executable
- [ ] System tray minimization support
- [ ] Windows notifications when issues are detected
- [ ] Persistent history between sessions
- [ ] Long-term historical graphs (24h, 7d, 30d)
- [ ] "Speedtest" mode to measure bandwidth
- [ ] Multiple language support (i18n)
- [ ] Export reports in PDF/HTML formats
- [ ] Auto-update functionality
- [ ] Command-line interface (CLI) mode
- [ ] Custom alert thresholds per connection type

---

## Version History

### Versioning Scheme

This project uses [Semantic Versioning](https://semver.org/):

- **MAJOR** version: Incompatible API changes
- **MINOR** version: New functionality (backward-compatible)
- **PATCH** version: Bug fixes (backward-compatible)

### How to Check Your Version

Run the script and check the title bar or look at the header in `InternetHealth.ps1`:

```powershell
# Version: X.Y.Z
```

---

## Contributing

See issues and feature requests at: https://github.com/jcardila/internet-health-monitor/issues

---

[1.0.0]: https://github.com/jcardila/internet-health-monitor/releases/tag/v1.0.0
