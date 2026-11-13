@echo off
setlocal DisableDelayedExpansion
set "PORTABLE_ROOT=%~dp0"
set "DOTNET_ROOT=%PORTABLE_ROOT%Runtime"
set "DOTNET_ROOT_X64=%DOTNET_ROOT%"
set "DOTNET_MULTILEVEL_LOOKUP=0"
set "DOTNET_ROLL_FORWARD=LatestPatch"
set "TEMP=%PORTABLE_ROOT%Data\Temp"
set "TMP=%TEMP%"
set "PATH=%DOTNET_ROOT%;%PORTABLE_ROOT%App;%SystemRoot%\System32;%SystemRoot%"
if not exist "%PORTABLE_ROOT%portable.mode" goto missing
if not exist "%DOTNET_ROOT%\dotnet.exe" goto missing
if not exist "%PORTABLE_ROOT%App\QuickLook.Next.App.exe" goto missing
if "%~1"=="--smoke-portable" goto smoke
start "" /D "%PORTABLE_ROOT%App" "%PORTABLE_ROOT%App\QuickLook.Next.App.exe" %*
exit /b 0
:smoke
"%PORTABLE_ROOT%App\QuickLook.Next.App.exe" --smoke-portable
exit /b %errorlevel%
:missing
echo The portable package is incomplete. Extract the complete ZIP before starting.
pause
exit /b 1
