@echo off
cd /d "%~dp0"
title RoweMod

if exist "%~dp0dist\RoweMod.exe" (
  start "" "%~dp0dist\RoweMod.exe"
  exit /b 0
)
echo This is the source code, not the ready-to-run app.
echo Download RoweMod-Windows-x64.zip from Releases and extract it.
echo Then double-click RoweMod.cmd in that folder. No .NET install is needed.
echo Developers: use RoweMod.Dev.cmd to build from source.
start "" "https://github.com/xrowex/RoweMod-Rollout/releases"
pause
exit /b 1
