@echo off
rem Doble clic para compilar y probar la app en este equipo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0compilar-local.ps1"
pause
