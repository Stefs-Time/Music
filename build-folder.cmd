@echo off
REM Folder-based publish (no single-file, no extraction step).
REM Use this if build.cmd's single-file output crashes on first run — a
REM folder publish gives the OS a normal .exe + side-by-side DLLs.
REM Output: publish-folder\MusicSorter.exe (run this one)
setlocal
pushd "%~dp0"

echo ==^> Restoring packages...
dotnet restore MusicSorter.sln || goto :err

echo ==^> Publishing folder-based win-x64 release...
dotnet publish MusicSorter\MusicSorter.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=false ^
    -o publish-folder || goto :err

echo.
echo ==^> Build complete: %CD%\publish-folder\MusicSorter.exe
echo     (the whole publish-folder is needed — you can't move the .exe alone)
popd
exit /b 0

:err
echo.
echo ==^> Build FAILED
popd
exit /b 1
