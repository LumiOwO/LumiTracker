@echo off
setlocal

cd /d "%~dp0\.."
python.exe dev_assets\package.py "%~dp0\..\publish" skip_full

endlocal
pause
exit /b 0