@echo off
cd /d "%~dp0"
if exist "%~dp0dist\RoweMod.exe" (
  start "" "%~dp0dist\RoweMod.exe"
  exit /b 0
)
dotnet run --project "%~dp0tools\RoweMod.App\RoweMod.App.csproj" -c Release
