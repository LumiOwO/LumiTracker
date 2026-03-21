@echo off
setlocal

cd /d "%~dp0"\..

:: Note: This uses the system python (which should have scikit-learn installed)
:: Because we are running in WSL1/Windows interop, we call python.exe directly
echo Running Feature Extraction Benchmark (Baseline)...
python.exe -E -m watcher.benchmark.pipeline --tag baseline

echo.
echo Running Summary...
python.exe -E -m watcher.benchmark.summary

endlocal
pause
exit /b 0
