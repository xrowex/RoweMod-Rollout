@echo off
cd /d "%~dp0"
title RoweMod Play
if exist "%~dp0dist\RoweMod.Play.exe" (
  start "" "%~dp0dist\RoweMod.Play.exe"
  exit /b 0
)
call "%~dp0RoweMod.cmd"
