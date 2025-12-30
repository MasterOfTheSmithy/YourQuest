@echo off
cd /d "C:\Users\Garri\Documents\Unity\YourQuest"

git add .

:: Use -m to provide a commit message quickly
set /p msg=Enter commit message: 
git commit -m "%msg%"

pause