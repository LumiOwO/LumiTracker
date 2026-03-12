@echo off
setlocal

cd /d "%~dp0"\..

:: Note: This uses the system python (which should have scikit-learn installed)
:: Because we are running in WSL1/Windows interop, we call python.exe directly
echo Running Feature Extraction Benchmark...
python.exe -E -m watcher.benchmark

endlocal
pause
exit /b 0
