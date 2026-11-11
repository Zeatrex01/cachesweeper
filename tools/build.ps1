#Requires -Version 5.1
# ============================================================================
#  DevSuite build
#    tools\build.ps1           Hub and CacheSweep into dist\ and dist-tr\
#    tools\build.ps1 -Extras   also the experimental tools into dist-extras\
#
#  Only needs the C# compiler that ships with Windows. Writes nothing outside
#  those folders. The exit code is the number of apps that failed to compile.
# ============================================================================
param([switch]$Extras)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Icon = Join-Path $Root 'assets\DevSuite.ico'

$csc = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { Write-Host 'ERROR: csc.exe not found. .NET Framework 4 is required.'; exit 1 }

$script:Failed = 0

function Build-App($manifestPath, $exeName, $outDir) {
    $m = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $out = Join-Path $outDir ($exeName + '.exe')
    [void][IO.Directory]::CreateDirectory($outDir)

    $argv = @('/nologo', '/target:winexe', '/optimize+', '/utf8output', "/out:$out",
              '/reference:System.dll', '/reference:System.Core.dll',
              '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll')
    foreach ($r in @($m.references)) { if ($r) { $argv += "/reference:$r" } }
    if (Test-Path $Icon) { $argv += "/win32icon:$Icon" }
    foreach ($s in @($m.sources)) { $argv += (Join-Path $Root ($s -replace '/', '\')) }

    # A locked exe makes csc fail while the old file stays, so only the exit code counts.
    $output = & $csc @argv 2>&1
    if ($LASTEXITCODE -ne 0) {
        $script:Failed++
        Write-Host "  x $exeName failed:"
        $output | ForEach-Object { Write-Host "    $_" }
        return
    }
    Write-Host ("  + {0}  ({1} KB)" -f $exeName, [math]::Round((Get-Item $out).Length / 1KB))
}

Write-Host "Compiler: $csc"

$dist = Join-Path $Root 'dist'
Build-App (Join-Path $Root 'hub\app.json') 'DevSuiteHub' $dist
foreach ($dir in Get-ChildItem (Join-Path $Root 'apps') -Directory) {
    $manifest = Join-Path $dir.FullName 'app.json'
    if (Test-Path $manifest) { Build-App $manifest $dir.Name $dist }
}

# Language pack: the same binaries plus lang.json.
$pack = Join-Path $Root 'dist-tr'
if ($script:Failed -eq 0) {
    [void][IO.Directory]::CreateDirectory($pack)
    Copy-Item (Join-Path $dist '*.exe') $pack -Force
    Copy-Item (Join-Path $Root 'lang\tr.json') (Join-Path $pack 'lang.json') -Force
    Write-Host '  + dist-tr  (Turkish language pack)'
}

if ($Extras) {
    Write-Host ''
    Write-Host 'Experimental tools. Read extras\README.md before running these.'
    $extrasOut = Join-Path $Root 'dist-extras'
    foreach ($dir in Get-ChildItem (Join-Path $Root 'extras') -Directory) {
        $manifest = Join-Path $dir.FullName 'app.json'
        if (-not (Test-Path $manifest)) { continue }
        Build-App $manifest $dir.Name $extrasOut
    }
    # The Hub lists what sits next to it, so a copy here shows the extras too.
    if ($script:Failed -eq 0) {
        Copy-Item (Join-Path $dist '*.exe') $extrasOut -Force
    }
}

Write-Host ''
if ($script:Failed -gt 0) { Write-Host "$script:Failed app(s) failed." } else { Write-Host "Done. Output: $dist" }
exit $script:Failed
