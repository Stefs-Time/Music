@echo off
REM Build a single-file Windows .exe for MusicSorter.
REM Requirements:  .NET 8 SDK installed (https://dotnet.microsoft.com/download)
REM Output:        publish\MusicSorter.exe
setlocal
pushd "%~dp0"

echo ==^> Restoring packages...
dotnet restore MusicSorter.sln || goto :err

echo ==^> Publishing single-file win-x64 release...
dotnet publish MusicSorter\MusicSorter.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeAllContentForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o publish || goto :err

echo.
echo ==^> Build complete: %CD%\publish\MusicSorter.exe
popd
exit /b 0

:err
echo.
echo ==^> Build FAILED
popd
exit /b 1
