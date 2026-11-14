# Builds ClassPack.dll against the game's own files and assembles the mod folder in dist\classpack.
# Usage:  .\build.ps1            build only
#         .\build.ps1 -Install   build, then copy the mod into the game's Mods folder
#         .\build.ps1 -Share     build, then make a zip in share\ to give to friends
param([switch]$Install, [switch]$Share)

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

if ($Share) {
    # A zip holding the classpack folder plus the install note, named after the mod version.
    $version = (Get-Content (Join-Path $root 'package\mod.json') -Raw | ConvertFrom-Json).version
    $stage = Join-Path $root 'dist\share-stage'
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -Confirm:$false }
    New-Item -ItemType Directory -Force (Join-Path $stage 'classpack') | Out-Null
    Copy-Item (Join-Path $dist '*') (Join-Path $stage 'classpack') -Recurse -Force
    Copy-Item (Join-Path $root 'share-notes\*') $stage -Force
    $shareDir = Join-Path $root 'share'
    New-Item -ItemType Directory -Force $shareDir | Out-Null
    $zip = Join-Path $shareDir "ClassPack-$version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force -Confirm:$false }
    # Entries are added one by one so their names use forward slashes, which every unzip tool reads.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem $stage -Recurse -File) {
            $name = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name)
        }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "Share zip: $zip"
}

if ($Install) {
    $target = Join-Path $game 'Mods\classpack'
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $dist '*') $target -Recurse -Force
    Write-Host "Installed to $target"
}
