@echo off
setlocal

REM Always run from the folder this .bat is in (your project root)
cd /d "%~dp0" || (
  echo Failed to cd into project directory: %~dp0
  pause
  exit /b 1
)

git status

REM Stage everything (or change to specific files if you prefer)
git add .

REM Commit with message to avoid editor issues
set MSG=Unify WorldState model; harden LLM delta flow; add self-repair
git commit -m "%MSG%"

REM Push if you want one-button publish (optional)
REM git push origin main

pause
