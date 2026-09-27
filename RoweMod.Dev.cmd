@echo off
cd /d "%~dp0"
title RoweMod Developer
dotnet run --project "%~dp0tools\RoweMod.App\RoweMod.App.csproj" -c Release
if errorlevel 1 (
  echo Build failed. Source development requires a .NET SDK compatible with .NET 8.
  pause
  exit /b 1
)
