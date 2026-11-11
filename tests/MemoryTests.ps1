#Requires -Version 5.1
# ============================================================================
#  Scan memory tests. Compiles CacheSweep into a private temp folder and calls
#  the memory and fingerprint code directly. Opens no window. Only files this
#  script created inside that temp folder are ever deleted.
#  Exit code: 0 when every test passes, otherwise the number of failures.
# ============================================================================

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Tmp  = Join-Path ([IO.Path]::GetFullPath($env:TEMP)) ('devsuite-memorytests-' + [guid]::NewGuid().ToString('N'))
$script:Failed = 0
$script:Passed = 0
$script:Links = @()

function Test-Case($name, [scriptblock]$body) {
    try { $ok = & $body } catch { $ok = $false; Write-Host "        ($($_.Exception.Message))" }
    if ($ok -eq $true) { $script:Passed++; Write-Host "  pass  $name" } else { $script:Failed++; Write-Host "  FAIL  $name" }
}

try {
    [void][IO.Directory]::CreateDirectory($Tmp)
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    $m = Get-Content (Join-Path $Root 'apps\CacheSweep\app.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $sources = @($m.sources | ForEach-Object { Join-Path $Root ($_ -replace '/', '\') })
    $refs = @($m.references | ForEach-Object { "/reference:$_" })
    $exe = Join-Path $Tmp 'Sweep.exe'
    $output = & $csc /nologo /target:winexe /optimize+ "/out:$exe" /reference:System.dll /reference:System.Core.dll `
        /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $refs $sources 2>&1
    if ($LASTEXITCODE -ne 0) { throw ("Compile failed:`n" + ($output -join "`n")) }

    $asm = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
    $Flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
    $memory = $asm.GetType('CacheSweep.ScanMemory')
    $targetType = $asm.GetType('CacheSweep.Target')
    $rememberedType = $asm.GetType('CacheSweep.Remembered')

    function New-Target($folder) {
        $t = [Activator]::CreateInstance($targetType)
        $t.GetType().GetField('Paths').SetValue($t, [string[]]@($folder))
        [void]$t.GetType().GetField('Found').GetValue($t).Add($folder)
        $t
    }
    function Get-Print($t) { $memory.GetMethod('Fingerprint', $Flags).Invoke($null, @($t)) }
    function Set-Later($path) { [IO.Directory]::SetLastWriteTimeUtc($path, [DateTime]::UtcNow.AddMinutes(-5)) }
    function Load-File($file) {
        $memory.GetMethods($Flags) | Where-Object { $_.Name -eq 'Load' -and $_.GetParameters().Count -eq 1 } |
            ForEach-Object { $_.Invoke($null, @([string]$file)) }
    }
    function New-Map {
        $type = [Collections.Generic.Dictionary``2].MakeGenericType([string], $rememberedType)
        ,[Activator]::CreateInstance($type, @([StringComparer]::OrdinalIgnoreCase))
    }
    function New-Remembered($bytes, $print) {
        $r = [Activator]::CreateInstance($rememberedType)
        $r.GetType().GetField('Bytes').SetValue($r, [long]$bytes)
        $r.GetType().GetField('Print').SetValue($r, [string]$print)
        $r.GetType().GetField('At').SetValue($r, [DateTime]::UtcNow)
        $r
    }

    # ---- fingerprint
    $cache = Join-Path $Tmp 'cache'
    [void][IO.Directory]::CreateDirectory("$cache\a\b\c\d\e")
    [IO.File]::WriteAllText("$cache\top.bin", 'x')
    [IO.File]::WriteAllText("$cache\a\mid.bin", 'x')
    [IO.File]::WriteAllText("$cache\a\b\c\d\e\deep.bin", 'x')
    $t = New-Target $cache
    # Windows updates the dates of a folder just written a moment late. Settling here costs a
    # real scan at most one extra measurement, never a missed change.
    [void](Get-Print $t)
    Start-Sleep -Milliseconds 200
    $first = Get-Print $t

    Write-Host 'Fingerprint'
    Test-Case 'is the same when nothing changed' { (Get-Print $t) -eq $first -and $first.Length -gt 0 }
    Test-Case 'changes when a file appears at the top' {
        $before = Get-Print $t; [IO.File]::WriteAllText("$cache\new.bin", 'x'); (Get-Print $t) -ne $before
    }
    Test-Case 'changes when a file grows one level down' {
        $before = Get-Print $t; [IO.File]::WriteAllText("$cache\a\mid.bin", 'xxxx'); (Get-Print $t) -ne $before
    }
    Test-Case 'changes when a file is removed at the depth limit' {
        [IO.File]::WriteAllText("$cache\a\b\limit.bin", 'x')
        $before = Get-Print $t; [IO.File]::Delete("$cache\a\b\limit.bin"); (Get-Print $t) -ne $before
    }
    Test-Case 'does not see below the depth limit, which is why reused sizes are estimates' {
        $before = Get-Print $t
        [IO.File]::WriteAllText("$cache\a\b\c\d\e\deep.bin", 'much longer now')
        (Get-Print $t) -eq $before
    }
    Test-Case 'stays cheap on a huge folder and still notices a change' {
        $big = Join-Path $Tmp 'big'; [void][IO.Directory]::CreateDirectory($big)
        $limit = $memory.GetField('PrintBudget', $Flags).GetRawConstantValue()
        for ($i = 0; $i -lt ($limit + 50); $i++) { [IO.File]::WriteAllText("$big\f$i.bin", '') }
        $bt = New-Target $big
        $before = Get-Print $bt
        [IO.File]::WriteAllText("$big\zzz-past-the-budget.bin", '')
        (Get-Print $bt) -ne $before
    }
    Test-Case 'does not enter links' {
        $outside = Join-Path $Tmp 'outside'; [void][IO.Directory]::CreateDirectory($outside)
        $linked = Join-Path $Tmp 'linked'; [void][IO.Directory]::CreateDirectory($linked)
        $link = Join-Path $linked 'out'
        [void](cmd /c mklink /J "$link" "$outside" 2>&1)
        $script:Links += $link
        if (-not (Test-Path $link)) { throw 'junction was not created' }
        $lt = New-Target $linked
        $before = Get-Print $lt
        [IO.File]::WriteAllText("$outside\inside.bin", 'x')
        (Get-Print $lt) -eq $before
    }
    Test-Case 'gives no print rather than failing when the folder is gone' {
        (Get-Print (New-Target (Join-Path $Tmp 'not-there'))) -ne $null
    }

    # ---- the rule
    Write-Host 'When a quick scan must measure'
    $must = $memory.GetMethod('MustMeasure', $Flags)
    $known = New-Remembered 100 'same'
    Test-Case 'not when the print matches' { -not $must.Invoke($null, @((New-Target $cache), $known, 'same')) }
    Test-Case 'when the print differs' { $must.Invoke($null, @((New-Target $cache), $known, 'other')) }
    Test-Case 'when nothing is remembered' { $must.Invoke($null, @((New-Target $cache), $null, 'same')) }
    Test-Case 'when no print could be taken' { $must.Invoke($null, @((New-Target $cache), (New-Remembered 100 ''), '')) }
    Test-Case 'always for a target with an age rule' {
        $aged = New-Target $cache; $aged.GetType().GetField('MinAgeDays').SetValue($aged, 7)
        $must.Invoke($null, @($aged, $known, 'same'))
    }
    Test-Case 'always for Unity logs' {
        $logs = New-Target $cache; $logs.GetType().GetField('UnityLog').SetValue($logs, $true)
        $must.Invoke($null, @($logs, $known, 'same'))
    }

    # ---- the file
    Write-Host 'The file'
    $file = Join-Path $Tmp 'mem\last-scan.txt'
    $save = $memory.GetMethods($Flags) | Where-Object { $_.Name -eq 'Save' -and $_.GetParameters().Count -eq 2 }
    $map = New-Map
    $map.Add('C:\sample|C:\other', (New-Remembered 12345 'print=='))
    $save.Invoke($null, @($map, [string]$file))
    Test-Case 'round trips through save and load' {
        $back = Load-File $file
        $entry = $back['C:\sample|C:\other']
        ($back.Count -eq 1) -and ($entry.GetType().GetField('Bytes').GetValue($entry) -eq 12345) -and ($entry.GetType().GetField('Print').GetValue($entry) -eq 'print==')
    }
    Test-Case 'leaves no temp file behind' { @(Get-ChildItem (Split-Path $file)).Count -eq 1 }
    Test-Case 'overwrites an existing file' {
        $map['C:\sample|C:\other'] = New-Remembered 777 'p'
        $save.Invoke($null, @($map, [string]$file))
        $entry = (Load-File $file)['C:\sample|C:\other']
        $entry.GetType().GetField('Bytes').GetValue($entry) -eq 777
    }
    function Write-Mem($text) { [IO.File]::WriteAllText($file, $text) }
    Test-Case 'ignores a missing file' { (Load-File (Join-Path $Tmp 'no-such-file')).Count -eq 0 }
    Test-Case 'ignores a file with another header' { Write-Mem "something else`n1`t2`t3`tkey`n"; (Load-File $file).Count -eq 0 }
    Test-Case 'drops bad lines and keeps good ones' {
        Write-Mem "cachesweep-scan 1`nnot a line`nabc`t2`tp`tbad-time`n5`tabc`tp`tbad-size`n5`t-1`tp`tnegative`n99999999999999999999`t1`tp`ttoo-big`n5`t2`tp`t`n5`t2`tp`tgood`n"
        $back = Load-File $file
        ($back.Count -eq 1) -and $back.ContainsKey('good')
    }
    Test-Case 'ignores a file too large to be real' {
        Write-Mem ("cachesweep-scan 1`n5`t2`tp`tgood`n" + ('x' * 1100000))
        (Load-File $file).Count -eq 0
    }

    # ---- what the file can reach
    Write-Host 'What the file can reach'
    Test-Case 'cannot add a folder to a target' {
        $victim = Join-Path $Tmp 'precious'
        $target = New-Target $cache
        $key = $memory.GetMethod('KeyOf', $Flags).Invoke($null, @($target))
        $evil = New-Map
        $evil.Add($victim, (New-Remembered 1 'p'))
        $evil.Add($key, (New-Remembered 2 'p'))
        $list = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($targetType))
        [void]$list.GetType().GetMethod('Add').Invoke($list, @($target))
        $memory.GetMethod('Apply', $Flags).Invoke($null, @($list, $evil))
        $found = @($target.GetType().GetField('Found').GetValue($target))
        ($found.Count -eq 1) -and ($found[0] -eq $cache) -and ($target.GetType().GetField('Bytes').GetValue($target) -eq 2) -and $target.GetType().GetField('Estimated').GetValue($target)
    }
    Test-Case 'marks a target with no entry as unknown, not zero' {
        $target = New-Target $cache
        $list = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($targetType))
        [void]$list.GetType().GetMethod('Add').Invoke($list, @($target))
        $memory.GetMethod('Apply', $Flags).Invoke($null, @($list, (New-Map)))
        ($target.GetType().GetField('Bytes').GetValue($target) -eq -1) -and -not $target.GetType().GetField('Estimated').GetValue($target)
    }
    Test-Case 'a failed measurement keeps the older entry, and a vanished target loses it' {
        $target = New-Target $cache
        $key = $memory.GetMethod('KeyOf', $Flags).Invoke($null, @($target))
        $mem = New-Map; $mem.Add($key, (New-Remembered 50 'p'))
        $list = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($targetType))
        [void]$list.GetType().GetMethod('Add').Invoke($list, @($target))
        $target.GetType().GetField('Bytes').SetValue($target, [long]-1)
        $memory.GetMethod('Update', $Flags).Invoke($null, @($mem, $list))
        $kept = $mem.ContainsKey($key)
        $target.GetType().GetField('Found').GetValue($target).Clear()
        $memory.GetMethod('Update', $Flags).Invoke($null, @($mem, $list))
        $kept -and -not $mem.ContainsKey($key)
    }

    # ---- floating modes
    Write-Host 'Floating modes'
    $floating = Get-Content (Join-Path $Root 'apps\CacheSweep\MiniWindow.cs') -Raw -Encoding UTF8
    Test-Case 'cannot delete anything' { $floating -notmatch 'Delete\(|DeleteEntry|DeleteTree|DeleteFile' }
    Test-Case 'measure folders only through the scan button' {
        ($floating -notmatch 'Sweeper\.|Fingerprint') -and ([regex]::Matches($floating, 'RunScan').Count -eq 1)
    }
    # Windows once widened the icon to its minimum window width when the handle was created.
    # The handle is created without showing anything.
    Test-Case 'the icon stays a square circle once Windows creates it' {
        Add-Type -AssemblyName System.Windows.Forms
        $asm.GetType('DevSuite.Dpi').GetMethod('Init', $Flags).Invoke($null, @())
        $main = $asm.GetType('CacheSweep.MainWindow').GetConstructors($Flags)[0].Invoke(@())
        try {
            $icon = $asm.GetType('CacheSweep.IconWindow').GetConstructors($Flags)[0].Invoke(@($main))
            [void]$icon.Handle
            $side = $asm.GetType('DevSuite.Dpi').GetMethod('Px', $Flags, $null, [type[]]@([int]), $null).Invoke($null, @([int]72))
            $ok = $icon.Width -eq $side -and $icon.Height -eq $side
            if (-not $ok) { Write-Host "        (icon is $($icon.Width)x$($icon.Height), expected $side square)" }
            $icon.Dispose()
            $ok
        } finally { $main.Dispose() }
    }

    # ---- the delete path
    Write-Host 'Delete path'
    $code = Get-Content (Join-Path $Root 'apps\CacheSweep\CacheSweep.cs') -Raw -Encoding UTF8
    Test-Case 'a remembered size is measured before the confirmation' {
        $code -match 'if \(sel\.Any\(t => t\.Estimated\)\) \{ MeasureThenConfirm\(sel\); return; \}'
    }
    Test-Case 'opening the window does not scan unless the setting says so' {
        $code -match 'if \(_settings\.AutoScan\) \{ Scan\(_memory\.Count > 0\); return; \}\s+if \(_memory\.Count > 0\) \{ ShowRemembered\(\); return; \}'
    }
}
catch {
    $script:Failed++
    Write-Host "  FAIL  setup  ($($_.Exception.Message))"
}
finally {
    $left = 0
    foreach ($link in $script:Links) {
        try { if ([IO.Directory]::Exists($link)) { [IO.Directory]::Delete($link, $false) } } catch { $left++ }
    }
    if ($left -eq 0) {
        try { [IO.Directory]::Delete($Tmp, $true) } catch { Write-Host "Temp folder left behind: $Tmp" }
    } else {
        Write-Host "Junctions could not be unlinked, temp folder left in place: $Tmp"
    }
}

Write-Host ''
Write-Host ("{0} passed, {1} failed" -f $script:Passed, $script:Failed)
exit $script:Failed
