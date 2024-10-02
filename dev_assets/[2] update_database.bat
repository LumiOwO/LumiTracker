@echo off
setlocal

cd /d "%~dp0"\..
cd cards
python.exe generate.py

cd ..
.\python\python.exe -E -m watcher.database image 2> temp\a.txt

endlocal
pause
exit /b 0