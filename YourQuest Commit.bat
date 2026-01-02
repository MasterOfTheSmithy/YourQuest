@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM --------------------------------------------
REM Always run from the folder this .bat is in
REM (project root safety)
REM --------------------------------------------
cd /d "%~dp0" || (
  echo ERROR: Failed to cd into project directory: %~dp0
  pause
  exit /b 1
)

echo ================================
echo Git status
echo ================================
git status
if errorlevel 1 goto git_error

echo ================================
echo Staging files
echo ================================
git add .
if errorlevel 1 goto git_error

REM --------------------------------------------
REM Commit message (safe for Windows CMD)
REM --------------------------------------------
set MSG=Unify WorldState model; harden LLM delta flow; add self-repair

echo ================================
echo Committing
echo ================================
git commit -m "%MSG%"
if errorlevel 1 (
  echo NOTE: Commit may have failed (nothing to commit or error).
)

echo ================================
echo Pushing to origin/main
echo ================================
git push origin main
if errorlevel 1 goto git_error

echo ================================
echo SUCCESS: Commit and push complete
echo ================================
pause
exit /b 0

:git_error
echo ================================
echo ERROR: Git command failed
echo ================================
pause
exit /b 1
