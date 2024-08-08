@echo off
setlocal

:: Check if at least one argument is provided
if "%~1"=="" (
    echo Usage: %0 ^<path^> ^<version_number^>
    echo Or: %0 uninstall
    goto :eof
)

:: Handle the 'uninstall' case
if "%~1"=="uninstall" (
    echo Uninstall process initiated.
    :: Add your uninstall logic here, if any
    goto :eof
)

:: If we get here, there should be at least two arguments
if "%~2"=="" (
    echo Usage: %0 ^<path^> ^<version_number^>
    echo Or: %0 uninstall
    goto :eof
)

:: Process the arguments
set "VERSION_NUMBER=%~1"
set "TARGET_DIR=%~2"

set "INI_FILE=%TARGET_DIR%\LumiTracker.ini"
(
    echo [Application]
    echo Version = %VERSION_NUMBER%
    echo Console = 0
    echo Patch = 0
    echo Python = 0
    echo Assets = 0
) > "%INI_FILE%"

echo Created %INI_FILE%

if not exist "%TARGET_DIR%\Utils.bat" (
    mklink %TARGET_DIR%\Utils.bat "%~dp0"\Utils.bat
)

endlocal
