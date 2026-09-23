@echo off
setlocal EnableExtensions
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

if not exist "%~dp0Environment.props" (
  echo.
  echo [ERROR] Environment.props not found.
  echo         Copy Environment.props.example to Environment.props
  echo         and set VALHEIM_INSTALL to your Valheim folder.
  echo.
  pause
  exit /b 1
)

for /f "usebackq delims=" %%I in (`
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$n = Select-Xml -Path '%~dp0Environment.props' -XPath '//*[local-name()=\"VALHEIM_INSTALL\"]'; if ($n) { $n.Node.InnerText.Trim() }"
`) do set "VALHEIM_INSTALL=%%I"

if not defined VALHEIM_INSTALL (
  echo.
  echo [ERROR] Could not read VALHEIM_INSTALL from Environment.props
  echo.
  pause
  exit /b 1
)

REM Trim quotes if someone wrapped the path
set "VALHEIM_INSTALL=%VALHEIM_INSTALL:"=%"

if not exist "%VALHEIM_INSTALL%\valheim.exe" (
  echo.
  echo [ERROR] valheim.exe not found at:
  echo         %VALHEIM_INSTALL%
  echo         Fix VALHEIM_INSTALL in Environment.props
  echo.
  pause
  exit /b 1
)

if not exist "%VALHEIM_INSTALL%\BepInEx\core\BepInEx.dll" (
  echo.
  echo [ERROR] BepInEx not found under:
  echo         %VALHEIM_INSTALL%\BepInEx
  echo         Install BepInExPack Valheim into that game folder first.
  echo.
  pause
  exit /b 1
)

if "%SKIP_BUILD%"=="0" (
  where dotnet >nul 2>&1
  if errorlevel 1 (
    echo.
    echo [ERROR] dotnet SDK not on PATH. Install it, or run:
    echo         Launch-Valheim.bat -SkipBuild
    echo.
    pause
    exit /b 1
  )

  echo.
  echo Building and deploying mods to BepInEx\plugins ...
  echo.
  dotnet build "%~dp0TeflonTed.Valheim.sln" -c Release
  if errorlevel 1 (
    echo.
    echo [ERROR] Build failed — not launching.
    echo.
    pause
    exit /b 1
  )
) else (
  echo Skipping build ^(-SkipBuild^).
)

echo.
echo Launching: %VALHEIM_INSTALL%\valheim.exe
echo Mods load from: %VALHEIM_INSTALL%\BepInEx\plugins\TeflonTed.*
echo.

REM Start from the game directory so Doorstop/BepInEx resolves correctly.
start "" /D "%VALHEIM_INSTALL%" "%VALHEIM_INSTALL%\valheim.exe"

endlocal
exit /b 0
