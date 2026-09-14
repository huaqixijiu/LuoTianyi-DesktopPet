@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall-luotianyi-pet.ps1"
set "UNINSTALL_EXIT=%ERRORLEVEL%"
if not "%UNINSTALL_EXIT%"=="0" (
  echo.
  echo 卸载未完成。请保留本窗口中的错误信息。
  pause
)
exit /b %UNINSTALL_EXIT%
