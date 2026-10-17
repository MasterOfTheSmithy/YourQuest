REM C:\Users\Garri\YourQuest\Run_Project_Dump.bat
@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "SCRIPT=%ROOT%\YourQuest_ProjectDump.ps1"
set "LOG=%ROOT%\ProjectDump_LastRun.log"

cls
echo ============================================================
echo  YourQuest - ChatGPT Project Dump
echo ============================================================
echo.
echo Root:
echo %ROOT%
echo.
echo Script:
echo %SCRIPT%
echo.
echo Log:
echo %LOG%
echo.

if not exist "%SCRIPT%" (
    echo ERROR: PowerShell exporter was not found.
    echo Expected:
    echo "%SCRIPT%"
    echo.
    pause
    exit /b 1
)

if exist "%LOG%" del /q "%LOG%" >nul 2>nul

echo Starting project dump...
echo.
echo Do not close this window.
echo.

powershell.exe ^
    -NoLogo ^
    -NoProfile ^
    -NonInteractive ^
    -ExecutionPolicy Bypass ^
    -File "%SCRIPT%" ^
    -Root "%ROOT%" ^
    1>"%LOG%" 2>&1

set "ERR=%ERRORLEVEL%"

echo.
echo ============================================================
echo.

if not "%ERR%"=="0" (
    echo PROJECT DUMP FAILED
    echo Exit code: %ERR%
    echo.
    echo ---------------- ERROR LOG ----------------
    type "%LOG%"
    echo.
    echo -------------------------------------------
    echo.
    echo Full log saved to:
    echo "%LOG%"
    echo.
    pause
    exit /b %ERR%
)

echo PROJECT DUMP COMPLETED SUCCESSFULLY
echo.
type "%LOG%"
echo.
echo Output folder:
echo "%ROOT%\ChatGPT_ProjectDump"
echo.
echo ZIP:
echo "%ROOT%\ChatGPT_ProjectDump.zip"
echo.
echo Log:
echo "%LOG%"
echo.
pause

exit /b 0