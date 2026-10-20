@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Create-Shortcut.ps1" -Desktop
if errorlevel 1 pause
