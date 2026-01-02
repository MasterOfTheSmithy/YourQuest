@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "LOG=%~dp0commit_push_log.txt"
echo ===== %DATE% %TIME% =====> "%LOG%"

echo STEP 1: Starting...>> "%LOG%"
cd /d "%~dp0" >> "%LOG%" 2>&1
echo STEP 2: cd done (err=%ERRORLEVEL%) in %CD%>> "%LOG%"

REM --- Verify git exists ---
where git >> "%LOG%" 2>&1
if errorlevel 1 (
  echo ERROR: git not found in PATH>> "%LOG%"
  echo ERROR: git not found. Install Git for Windows.
  pause
  exit /b 1
)
echo STEP 3: git found>> "%LOG%"

REM --- Verify this is a git work tree (works even if .git is a file/worktree) ---
git rev-parse --is-inside-work-tree >> "%LOG%" 2>&1
if errorlevel 1 (
  echo ERROR: Not a git repo/worktree at %CD%>> "%LOG%"
  echo ERROR: Not a git repo/worktree here: %CD%
  pause
  exit /b 1
)
echo STEP 4: confirmed inside git work tree>> "%LOG%"

REM --- Status ---
echo STEP 5: git status>> "%LOG%"
git status >> "%LOG%" 2>&1
git status

REM --- Stage ---
echo STEP 6: git add .>> "%LOG%"
git add . >> "%LOG%" 2>&1
if errorlevel 1 (
  echo ERROR: git add failed>> "%LOG%"
  echo ERROR: git add failed. See log: %LOG%
  pause
  exit /b 1
)

REM --- Commit ---
set "MSG=Unify WorldState model; harden LLM delta flow; add self-repair"
echo STEP 7: git commit>> "%LOG%"
git commit -m "%MSG%" >> "%LOG%" 2>&1

REM If nothing to commit, git commit exits non-zero; that's OK.
echo STEP 7b: commit exit code=%ERRORLEVEL%>> "%LOG%"

REM --- Determine current branch ---
for /f "delims=" %%B in ('git rev-parse --abbrev-ref HEAD') do set "BR=%%B"
echo STEP 8: branch=%BR%>> "%LOG%"

REM --- Push (prefer upstream if configured; else push to origin/branch) ---
echo STEP 9: attempting push to upstream>> "%LOG%"
git push >> "%LOG%" 2>&1
if errorlevel 1 (
  echo STEP 9b: upstream push failed; trying origin %BR%>> "%LOG%"
  git push -u origin "%BR%" >> "%LOG%" 2>&1
  if errorlevel 1 (
    echo ERROR: git push failed>> "%LOG%"
    echo ERROR: git push failed. Open %LOG% and paste the bottom part here.
    pause
    exit /b 1
  )
)

echo SUCCESS: Commit+Push complete>> "%LOG%"
echo SUCCESS: Commit+Push complete
pause
exit /b 0
