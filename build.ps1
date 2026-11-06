# Release build script for AICrosswordGen (single-file)
param(
    [string]$Runtime = "win-x64",
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"

dotnet publish -c $Config -r $Runtime --self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

$out = "bin/$Config/net10.0/$Runtime/publish/AICrosswordGen.exe"
if (Test-Path $out)
{
    Write-Host "Published to $out"
}
else
{
    Write-Host "Checking publish folder..."
    Get-ChildItem "bin/$Config/net10.0/$Runtime/publish" | Select-Object Name, Length
}
