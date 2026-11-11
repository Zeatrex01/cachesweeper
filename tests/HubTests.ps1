#Requires -Version 5.1
# ============================================================================
#  Hub tests. Compiles the Hub into a private temp folder and asks it which
#  modules it would list. Opens no window and starts no module.
#  Exit code: 0 when every test passes, otherwise the number of failures.
# ============================================================================

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Tmp  = Join-Path ([IO.Path]::GetFullPath($env:TEMP)) ('devsuite-hubtests-' + [guid]::NewGuid().ToString('N'))
$script:Failed = 0
$script:Passed = 0

function Test-Case($name, $ok) {
    if ($ok) { $script:Passed++; Write-Host "  pass  $name" } else { $script:Failed++; Write-Host "  FAIL  $name" }
}

try {
    [void][IO.Directory]::CreateDirectory($Tmp)
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    $m = Get-Content (Join-Path $Root 'hub\app.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $sources = @($m.sources | ForEach-Object { Join-Path $Root ($_ -replace '/', '\') })
    $exe = Join-Path $Tmp 'Hub.exe'
    $output = & $csc /nologo /target:winexe /optimize+ "/out:$exe" /reference:System.dll /reference:System.Core.dll `
        /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources 2>&1
    if ($LASTEXITCODE -ne 0) { throw ("Compile failed:`n" + ($output -join "`n")) }

    $asm = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
    $available = $asm.GetType('Hub.Modules').GetMethod('Available')
    function Get-Listed($folder) {
        @($available.Invoke($null, @(,[string]$folder)) | ForEach-Object { $_.GetType().GetField('Id').GetValue($_) })
    }

    $dir = Join-Path $Tmp 'empty'
    [void][IO.Directory]::CreateDirectory($dir)
    Test-Case 'an empty folder lists nothing' ((Get-Listed $dir).Count -eq 0)

    $dir = Join-Path $Tmp 'default'
    [void][IO.Directory]::CreateDirectory($dir)
    foreach ($f in 'CacheSweep.exe', 'unins000.exe', 'setup.exe', 'evil.exe') { [IO.File]::WriteAllText((Join-Path $dir $f), '') }
    $listed = Get-Listed $dir
    Test-Case 'a default install lists only CacheSweep' (($listed.Count -eq 1) -and ($listed -contains 'CacheSweep'))

    $dir = Join-Path $Tmp 'extras'
    [void][IO.Directory]::CreateDirectory($dir)
    foreach ($f in 'CacheSweep.exe', 'McpControl.exe', 'McpSetup.exe', 'SkillManager.exe') { [IO.File]::WriteAllText((Join-Path $dir $f), '') }
    Test-Case 'extras appear only when they are present' ((Get-Listed $dir).Count -eq 4)

    $code = Get-Content (Join-Path $Root 'hub\Hub.cs') -Raw
    Test-Case 'the Hub has no network code' ($code -notmatch 'System\.Net|WebClient|HttpClient|DownloadFile')
    $start = [regex]::Matches($code, 'Process\.Start').Count
    Test-Case 'the Hub starts a program in exactly one place' ($start -eq 1)

    $sweep = Get-Content (Join-Path $Root 'apps\CacheSweep\CacheSweep.cs') -Raw -Encoding UTF8
    Test-Case 'CacheSweep does not scan on open by default' ($sweep -match 'public bool\s+AutoScan = false;')
}
catch {
    $script:Failed++
    Write-Host "  FAIL  setup  ($($_.Exception.Message))"
}
finally {
    try { [IO.Directory]::Delete($Tmp, $true) } catch { Write-Host "Temp folder left behind: $Tmp" }
}

Write-Host ''
Write-Host ("{0} passed, {1} failed" -f $script:Passed, $script:Failed)
exit $script:Failed
