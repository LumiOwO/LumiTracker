@echo off
setlocal

:: Set environment variables to ignore global settings
set PATH=
set PYTHONHOME=
set PYTHONPATH=
set PYTHONNOUSERSITE=1
@REM set PYTHONWARNINGS=ignore

set PYTHON_EXEC=python/python.exe

:: Navigate to the directory containing requirements.txt
cd /d "%~dp0"

:: Run pip install to install packages from requirements.txt
echo Installing required packages...
"%PYTHON_EXEC%" -E "python/get-pip.py"
"%PYTHON_EXEC%" -E -m pip install --use-pep517 -r requirements.txt
if %errorlevel% neq 0 (
    echo Failed to install required packages
    pause
    exit /b %errorlevel%
)

echo Done!
endlocal
pause
exit /b 0