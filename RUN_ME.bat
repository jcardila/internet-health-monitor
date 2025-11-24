@echo off
REM ===================================================================
REM Internet Health Monitor Launcher
REM ===================================================================
REM This batch file launches the Internet Health Monitor without
REM requiring manual PowerShell execution policy changes.
REM ===================================================================

REM Check if the PowerShell script exists
if not exist "%~dp0InternetHealth.ps1" (
    echo ERROR: InternetHealth.ps1 not found!
    echo.
    echo Please make sure this batch file is in the same folder as InternetHealth.ps1
    echo.
    pause
    exit /b 1
)

REM Launch PowerShell in a new window that will close automatically
REM Using -WindowStyle Hidden keeps the PowerShell console hidden
start "" powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File "%~dp0InternetHealth.ps1"

