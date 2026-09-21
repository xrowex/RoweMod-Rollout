@echo off
cd /d "%~dp0"
title RoweMod Play
if exist "%~dp0dist\RoweMod.Play.exe" (
  start "" "%~dp0dist\RoweMod.Play.exe"
  exit /b 0
)
where dotnet >nul 2>&1
if errorlevel 1 (
  echo Install the .NET 8 SDK, then try again.
  start "" "https://dotnet.microsoft.com/download/dotnet/8.0"
  pause
  exit /b 1
)
dotnet run --project "%~dp0tools\RoweMod.Play\RoweMod.Play.csproj" -c Release
