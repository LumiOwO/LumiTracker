@echo off
setlocal

cd /d "%~dp0"\..
.\python\python.exe -E -m watcher.database image 2> temp\a.txt

endlocal
pause
exit /b 0