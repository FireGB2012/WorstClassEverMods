# Runs the mod's hands-free self-test of the swim or dance class (see ClassPack\AutoTest.cs):
# builds and installs the mod, leaves the one-shot "autotest.txt" in the mod's settings folder,
# starts the game and waits for it to close by itself. The pictures and the report end up in
# dist\autotest\<role> (dist\autotest\dance_<role> for the dance class), with overview sheets
# made by tools\selftest_sheets.py.
#
#   .\tools\selftest.ps1                      as a student, swim class
#   .\tools\selftest.ps1 -Role teacher -Kind 2    as a teacher (0 normal, 1 gym, 2 lunch lady, 3 janitor, 4 wig)
#   .\tools\selftest.ps1 -Class dance         the dance class
param([string]$Role = 'student', [int]$Kind = 0, [string]$Class = 'swim', [switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$settings = Join-Path $env:USERPROFILE 'AppData\LocalLow\Funcore Games\Worst Class Ever\Mods\Config\classpack'
$made = Join-Path $settings 'autotest'
$name = if ($Role -eq 'teacher') { "teacher$Kind" } else { 'student' }
if ($Class -eq 'dance') { $name = "dance_$name" }
$keep = Join-Path $root "dist\autotest\$name"

function Game-Running { [bool](Get-Process | Where-Object { $_.ProcessName -like 'Worst Class Ever*' }) }

if (Game-Running) { throw 'The game is running. Close it first.' }
if (-not $NoBuild) {
    & (Join-Path $root 'build.ps1') -Install 2>&1 | Where-Object { "$_" -notmatch 'warning CS' } | Select-Object -Last 2
}
New-Item -ItemType Directory -Force $settings | Out-Null
if (Test-Path $made) { Get-ChildItem $made -File | Remove-Item -Force }
Set-Content -Path (Join-Path $settings 'autotest.txt') -Value @("role=$Role", "kind=$Kind", "class=$Class") -Encoding ascii

Start-Process 'steam://rungameid/5175310'
$deadline = (Get-Date).AddMinutes(9)
$seen = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    if (Game-Running) { $seen = $true } elseif ($seen) { break }
}
if (Game-Running) {
    'The game did not close by itself. Closing it.'
    Get-Process | Where-Object { $_.ProcessName -like 'Worst Class Ever*' } | Stop-Process -Force -Confirm:$false
    Start-Sleep -Seconds 2
}
if (-not $seen) { 'The game never started.' }

# A flag the mod did not pick up must not be left behind for the player's next start.
$flag = Join-Path $settings 'autotest.txt'
if (Test-Path $flag) { Remove-Item $flag -Force; 'The mod never read the test flag.' }

New-Item -ItemType Directory -Force $keep | Out-Null
Get-ChildItem $keep -File | Remove-Item -Force
if (Test-Path $made) { Copy-Item (Join-Path $made '*') $keep -Force }
'--- report'
$report = Join-Path $keep 'report.txt'
if (Test-Path $report) { Get-Content $report } else { 'no report was written' }
'--- pictures'
(Get-ChildItem $keep -File | Where-Object { $_.Extension -in '.jpg', '.png' }).Name -join ', '
'--- log lines'
$log = Join-Path $env:USERPROFILE 'AppData\LocalLow\Funcore Games\Worst Class Ever\Player.log'
Select-String -Path $log -Pattern 'Mod:classpack|error in |NullReference|Exception' |
    Where-Object { $_.Line -notmatch 'autotest:' } | Select-Object -Last 40 | ForEach-Object { $_.Line }
python (Join-Path $PSScriptRoot 'selftest_sheets.py') $keep
