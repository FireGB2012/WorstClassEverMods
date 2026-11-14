# Builds ClassPack.dll against the game's own files and assembles the mod folder in dist\classpack.
# Usage:  .\build.ps1            build only
#         .\build.ps1 -Install   build, then copy the mod into the game's Mods folder
param([switch]$Install)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Worst Class Ever'
$managed = Join-Path $game 'Worst Class Ever_Data\Managed'
$dist = Join-Path $root 'dist\classpack'

if (-not (Test-Path $managed)) { throw "Game not found at $game" }

# The C# compiler that ships with the .NET SDK. No NuGet packages are needed:
# the mod compiles straight against the game's own dlls.
$sdk = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Sort-Object Name -Descending | Select-Object -First 1
$csc = Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

New-Item -ItemType Directory -Force $dist | Out-Null
$rsp = Join-Path $root 'dist\build.rsp'
$lines = @(
    '-nologo', '-target:library', '-nostdlib+', '-optimize+', '-debug-', '-deterministic+',
    '-langversion:9', '-nowarn:CS1701,CS1702',
    "-out:`"$dist\ClassPack.dll`""
)
$lines += Get-ChildItem $managed -Filter *.dll | ForEach-Object { "-r:`"$($_.FullName)`"" }
$lines += Get-ChildItem (Join-Path $root 'ClassPack') -Filter *.cs -Recurse | ForEach-Object { "`"$($_.FullName)`"" }
Set-Content -Path $rsp -Value $lines -Encoding utf8

& dotnet $csc "@$rsp"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

Copy-Item (Join-Path $root 'package\*') $dist -Recurse -Force
Write-Host "Built $dist"

if ($Install) {
    $target = Join-Path $game 'Mods\classpack'
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $dist '*') $target -Recurse -Force
    Write-Host "Installed to $target"
}
