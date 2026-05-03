@echo off
setlocal

set "APP_NAME=NaverProductOrganizer"
set "INSTALL_DIR=%LOCALAPPDATA%\Programs\%APP_NAME%"
set "EXE_PATH=%INSTALL_DIR%\NaverProductOrganizer.exe"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\Naver Product Organizer.lnk"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Naver Product Organizer"
set "START_MENU_SHORTCUT=%START_MENU_DIR%\Naver Product Organizer.lnk"

echo Installing %APP_NAME%...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
robocopy "%~dp0" "%INSTALL_DIR%" /E /XF install.cmd /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
    echo Install copy failed.
    pause
    exit /b 1
)

if not exist "%START_MENU_DIR%" mkdir "%START_MENU_DIR%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$shell=New-Object -ComObject WScript.Shell; foreach($path in @('%DESKTOP_SHORTCUT%','%START_MENU_SHORTCUT%')) { $s=$shell.CreateShortcut($path); $s.TargetPath='%EXE_PATH%'; $s.WorkingDirectory='%INSTALL_DIR%'; $s.IconLocation='%EXE_PATH%,0'; $s.Save() }"

echo Installed to %INSTALL_DIR%
start "" "%EXE_PATH%"
endlocal
