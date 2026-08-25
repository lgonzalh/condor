@echo off
chcp 65001 >nul
title Condor CLI - Ave Trabajo V16
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0condor_unicode_v16.ps1"
pause
