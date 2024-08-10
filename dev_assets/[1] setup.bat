@echo off
setlocal

:: Navigate to the directory containing requirements.txt
cd /d "%~dp0"\..

:: List of directories to be removed
set DIRS="python/Scripts" "python/Lib" "python/images"

:: Loop through each directory
for %%D in (%DIRS%) do (
    if exist %%D (
        rmdir /s /q %%D
        echo Directory %%D removed successfully.
    )
)

:: Set environment variables to ignore global settings
set PATH=
set PYTHONHOME=
set PYTHONPATH=
set PYTHONNOUSERSITE=1
@REM set PYTHONWARNINGS=ignore

set PYTHON_EXEC=python/python.exe

:: Run pip install to install packages from requirements.txt
echo Installing required packages...
"%PYTHON_EXEC%" -E "dev_assets/get-pip.py" --no-warn-script-location
"%PYTHON_EXEC%" -E -m pip install --no-deps --no-warn-script-location -r requirements.txt
if %errorlevel% neq 0 (
    echo Failed to install required packages
    pause
    exit /b %errorlevel%
)

echo Done!
endlocal
pause
exit /b 0