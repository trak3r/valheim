@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

REM =============================================================================
REM  Launch Valheim with Teflon Ted's mods from this repo.
REM
REM  Double-click this file, or from a terminal:
REM    Launch-Valheim.bat
REM    Launch-Valheim.bat -SkipBuild
REM
REM  Prerequisites (one-time):
REM    1. BepInExPack Valheim installed into your Valheim folder
REM    2. Environment.props created from Environment.props.example
REM    3. .NET SDK installed (only needed when building)
REM =============================================================================

set "SKIP_BUILD=0"
if /I "%~1"=="-SkipBuild" set "SKIP_BUILD=1"
if /I "%~1"=="/SkipBuild" set "SKIP_BUILD=1"

if not exist "%~dp0Environment.props" goto :err_no_props

REM Read VALHEIM_INSTALL via a temp file so paths with "(x86)" never hit for /f parsing.
set "VALHEIM_INSTALL="
set "_props_out=%TEMP%\teflonted_valheim_install.txt"
del /q "%_props_out%" 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$n = Select-Xml -Path '%~dp0Environment.props' -XPath '//*[local-name()=\"VALHEIM_INSTALL\"]'; if ($null -eq $n) { exit 2 }; [IO.File]::WriteAllText('%_props_out%', $n.Node.InnerText.Trim())"
if errorlevel 1 goto :err_bad_props
if not exist "%_props_out%" goto :err_bad_props

set /p VALHEIM_INSTALL=<"%_props_out%"
del /q "%_props_out%" 2>nul

REM Trim accidental quotes
set "VALHEIM_INSTALL=!VALHEIM_INSTALL:"=!"

if not defined VALHEIM_INSTALL goto :err_bad_props
if not exist "!VALHEIM_INSTALL!\valheim.exe" goto :err_no_exe
if not exist "!VALHEIM_INSTALL!\BepInEx\core\BepInEx.dll" goto :err_no_bepinex

if "!SKIP_BUILD!"=="0" goto :do_build
echo Skipping build ^(-SkipBuild^).
goto :launch

:do_build
where dotnet >nul 2>&1
if errorlevel 1 goto :err_no_dotnet

echo.
echo Building and deploying mods to BepInEx\plugins ...
echo.
dotnet build "%~dp0TeflonTed.Valheim.sln" -c Release
if errorlevel 1 goto :err_build_failed

:launch
echo.
echo Launching: !VALHEIM_INSTALL!\valheim.exe
echo Mods load from: !VALHEIM_INSTALL!\BepInEx\plugins\TeflonTed.*
echo.

REM Start from the game directory so Doorstop/BepInEx resolves correctly.
start "" /D "!VALHEIM_INSTALL!" "!VALHEIM_INSTALL!\valheim.exe"

endlocal
exit /b 0

:err_no_props
echo.
echo [ERROR] Environment.props not found.
echo         Copy Environment.props.example to Environment.props
echo         and set VALHEIM_INSTALL to your Valheim folder.
echo.
pause
exit /b 1

:err_bad_props
echo.
echo [ERROR] Could not read VALHEIM_INSTALL from Environment.props
echo.
pause
exit /b 1

:err_no_exe
echo.
echo [ERROR] valheim.exe not found at:
echo         !VALHEIM_INSTALL!
echo         Fix VALHEIM_INSTALL in Environment.props
echo.
pause
exit /b 1

:err_no_bepinex
echo.
echo [ERROR] BepInEx not found under:
echo         !VALHEIM_INSTALL!\BepInEx
echo         Install BepInExPack Valheim into that game folder first.
echo.
pause
exit /b 1

:err_no_dotnet
echo.
echo [ERROR] dotnet SDK not on PATH. Install it, or run:
echo         Launch-Valheim.bat -SkipBuild
echo.
pause
exit /b 1

:err_build_failed
echo.
echo [ERROR] Build failed — not launching.
echo.
pause
exit /b 1
