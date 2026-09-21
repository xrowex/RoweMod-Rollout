@echo off
cd /d "%~dp0"
title RoweMod

if exist "%~dp0.git\" (
  where git >nul 2>&1
  if not errorlevel 1 (
    echo Checking for RoweMod updates...
    git -C "%~dp0" fetch --quiet origin 2>nul
    git -C "%~dp0" pull --ff-only --quiet 2>nul
    if errorlevel 1 (
      echo Could not auto-update ^(local changes?^). Continuing with this copy.
    ) else (
      echo Repo is current.
    )
    echo.
  )
)

if exist "%~dp0dist\RoweMod.exe" (
  start "" "%~dp0dist\RoweMod.exe"
  exit /b 0
)

where dotnet >nul 2>&1
if errorlevel 1 (
  echo.
  echo RoweMod needs the .NET 8 SDK.
  echo Install it, then double-click RoweMod.cmd again.
  echo.
  echo Opening the download page...
  start "" "https://dotnet.microsoft.com/download/dotnet/8.0"
  echo.
  pause
  exit /b 1
)

echo Starting RoweMod ^(first launch can take a minute^)...
dotnet run --project "%~dp0tools\RoweMod.App\RoweMod.App.csproj" -c Release
if errorlevel 1 (
  echo.
  echo RoweMod failed to start. Install the .NET 8 SDK and try again.
  pause
  exit /b 1
)
