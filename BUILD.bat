@echo off
rem Builds the Hub and CacheSweep into dist\. Pass -Extras to also build the experimental tools.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build.ps1" %*
pause
