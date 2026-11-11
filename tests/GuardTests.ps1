#Requires -Version 5.1
# ============================================================================
#  CacheSweep guard tests
#  Compiles CacheSweep into a private temp folder and calls Guard directly.
#
#  Only files this script created inside that temp folder are ever deleted.
#  Junctions to real folders (Documents, Windows) are only passed to the guard
#  to read its answer; they are unlinked at the end, never followed.
#
#  Exit code: 0 when every test passes, otherwise the number of failures.
# ============================================================================

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
# TEMP can be written with 8.3 short names (GitHub runners use C:\Users\RUNNER~1\...). The guard
# reports real paths in long form, so the fixtures are written in long form too. GetFullPath on
# .NET Framework expands short names, the same way the guard's own Normalize does.
$Tmp  = Join-Path ([IO.Path]::GetFullPath($env:TEMP)) ('devsuite-guardtests-' + [guid]::NewGuid().ToString('N'))
$Work = Join-Path $Tmp 'work'
$script:Links = New-Object System.Collections.ArrayList
$script:Failed = 0
$script:Passed = 0
$script:Skipped = 0

function New-Folder($path) { [void][IO.Directory]::CreateDirectory($path); return $path }

function New-Junction($link, $target) {
    & cmd /c mklink /J "$link" "$target" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create junction $link" }
    [void]$script:Links.Add($link)
    return $link
}

function New-Fixture($name) {
    $dir = New-Folder (Join-Path $Work $name)
    [IO.File]::WriteAllText((Join-Path $dir 'keep.txt'), 'fixture')
    return $dir
}

function Test-Case($name, [scriptblock]$body) {
    try {
        $result = & $body
        # Type checks first: PowerShell's -eq would treat $true and 'skip' as equal.
        if ($result -is [string] -and $result -eq 'skip') {
            $script:Skipped++
            Write-Host "  skip  $name"
        } elseif ($result -is [bool] -and $result) {
            $script:Passed++
            Write-Host "  pass  $name"
        } else {
            $script:Failed++
            Write-Host "  FAIL  $name"
        }
    } catch {
        $script:Failed++
        Write-Host "  FAIL  $name  ($($_.Exception.Message))"
    }
}

$Flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'

function Invoke-Guard($method, [object[]]$arguments) {
    $m = $script:Guard.GetMethod($method, $Flags)
    if (-not $m) { throw "Guard.$method does not exist" }
    try { return $m.Invoke($null, $arguments) }
    catch { throw $_.Exception.InnerException }
}

function Test-Refused($path)  { return ($null -ne (Invoke-Guard 'Reject' @(,[string]$path))) }
function Test-Accepted($path) { return ($null -eq (Invoke-Guard 'Reject' @(,[string]$path))) }

function Add-ProtectedRoot($path) {
    [void]$script:Guard.GetField('ProtectedRoots', $Flags).GetValue($null).Add($path)
}

function Invoke-DeleteEntry($path, $isFolder, $parent) {
    $m = $script:Window.GetType().GetMethod('DeleteEntry', $Flags)
    # recycle = false: the fixtures live in temp and must not reach the Recycle Bin.
    return $m.Invoke($script:Window, @([string]$path, [bool]$isFolder, $false, [string]$parent))
}

try {
    New-Folder $Work | Out-Null

    # --- compile -------------------------------------------------------------
    $csc = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $csc) { throw 'csc.exe not found. .NET Framework 4 is required.' }

    $manifest = Get-Content (Join-Path $Root 'apps\CacheSweep\app.json') -Raw -Encoding UTF8
    $listOf = {
        param($key)
        $block = [regex]::Match($manifest, '(?s)"' + $key + '"\s*:\s*\[(.*?)\]').Groups[1].Value
        [regex]::Matches($block, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    }
    $sources = & $listOf 'sources' | ForEach-Object { Join-Path $Root ($_ -replace '/', '\') }
    $refs    = & $listOf 'references' | ForEach-Object { '/reference:' + $_ }
    $exe     = Join-Path $Tmp 'CacheSweep.exe'

    $compileOutput = & $csc /nologo /target:winexe /optimize+ /utf8output "/out:$exe" `
        /reference:System.dll /reference:System.Core.dll `
        /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
        $refs $sources 2>&1
    if (-not (Test-Path $exe)) { throw ("Compile failed:`n" + ($compileOutput -join "`n")) }

    # Loaded from bytes so the exe in temp stays deletable.
    $asm = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
    $script:Guard = $asm.GetType('CacheSweep.Guard')
    $script:Sweeper = $asm.GetType('CacheSweep.Sweeper')

    # A window object without its constructor: DeleteEntry only needs the two report lists.
    $windowType = $asm.GetType('CacheSweep.MainWindow')
    $script:Window = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($windowType)
    [GC]::SuppressFinalize($script:Window)
    foreach ($field in '_blocked', '_skipped') {
        $f = $windowType.GetField($field, $Flags)
        $f.SetValue($script:Window, [Activator]::CreateInstance($f.FieldType))
    }

    $documents = [Environment]::GetFolderPath('MyDocuments')

    # --- Reject: the path as written ------------------------------------------
    Write-Host 'Reject, direct paths'
    Test-Case 'a drive root is refused' { Test-Refused ($env:SystemDrive + '\') }
    Test-Case 'Windows is refused' { Test-Refused $env:WINDIR }
    Test-Case 'Documents is refused' {
        if (-not (Test-Path $documents)) { return 'skip' }
        Test-Refused $documents
    }
    Test-Case 'a plain folder is accepted' { Test-Accepted (New-Fixture 'plain') }

    # --- Reject: where the path really leads ----------------------------------
    Write-Host 'Reject, through junctions'
    Test-Case 'a junction to Documents is refused' {
        if (-not (Test-Path $documents)) { return 'skip' }
        Test-Refused (New-Junction (Join-Path $Work 'to-documents') $documents)
    }
    Test-Case 'a junction to Windows is refused' {
        Test-Refused (New-Junction (Join-Path $Work 'to-windows') $env:WINDIR)
    }
    Test-Case 'a junction to a protected root is refused' {
        $target = New-Fixture 'protected-a'
        Add-ProtectedRoot $target
        Test-Refused (New-Junction (Join-Path $Work 'to-protected') $target)
    }
    Test-Case 'a junction higher up the path is refused' {
        $target = New-Fixture 'protected-b'
        [void](New-Folder (Join-Path $target 'inner'))
        Add-ProtectedRoot $target
        $link = New-Junction (Join-Path $Work 'to-parent') $Work
        Test-Refused (Join-Path $link 'protected-b\inner')
    }
    Test-Case 'a protected root moved behind a junction stays protected' {
        $moved = New-Fixture 'moved-data'
        Add-ProtectedRoot (New-Junction (Join-Path $Work 'data-link') $moved)
        Test-Refused $moved
    }
    Test-Case 'a dangling junction is refused' {
        $gone = New-Fixture 'gone'
        $link = New-Junction (Join-Path $Work 'to-gone') $gone
        [IO.Directory]::Delete($gone, $true)
        Test-Refused $link
    }
    Test-Case 'a junction to a harmless folder is still accepted' {
        Test-Accepted (New-Junction (Join-Path $Work 'to-plain') (New-Fixture 'plain-target'))
    }

    # --- Chrome ----------------------------------------------------------------
    Write-Host 'Chrome channels'
    foreach ($channel in 'Chrome', 'Chrome Beta', 'Chrome Dev', 'Chrome SxS') {
        $data = Join-Path $env:LOCALAPPDATA "Google\$channel\User Data"
        Test-Case "$channel User Data is protected" { Invoke-Guard 'TouchesProtected' @(,[string]$data) }
        Test-Case "$channel profile folder is protected" {
            Invoke-Guard 'IsProtected' @(,[string](Join-Path $data 'Default'))
        }
    }

    # --- the last check before a target's contents go ---------------------------
    Write-Host 'CheckBeforeDelete'
    Test-Case 'a built-in target moved to a protected root is blocked' {
        $target = New-Fixture 'protected-c'
        Add-ProtectedRoot $target
        $link = New-Junction (Join-Path $Work 'cache-to-protected') $target
        $null -ne (Invoke-Guard 'CheckBeforeDelete' @([string]$link, $false))
    }
    Test-Case 'a built-in target moved to Documents is blocked' {
        if (-not (Test-Path $documents)) { return 'skip' }
        $link = New-Junction (Join-Path $Work 'cache-to-documents') $documents
        $null -ne (Invoke-Guard 'CheckBeforeDelete' @([string]$link, $false))
    }
    Test-Case 'a built-in target relocated to a harmless folder passes' {
        $link = New-Junction (Join-Path $Work 'cache-relocated') (New-Fixture 'relocated')
        $null -eq (Invoke-Guard 'CheckBeforeDelete' @([string]$link, $false))
    }
    Test-Case 'a custom target through a junction to a protected root is blocked' {
        $target = New-Fixture 'protected-d'
        Add-ProtectedRoot $target
        $link = New-Junction (Join-Path $Work 'custom-to-protected') $target
        $null -ne (Invoke-Guard 'CheckBeforeDelete' @([string]$link, $true))
    }

    # --- what the confirmation dialog shows --------------------------------------
    Write-Host 'Describe'
    Test-Case 'a junction shows where it really leads' {
        $target = New-Fixture 'shown-target'
        $link = New-Junction (Join-Path $Work 'shown-link') $target
        (Invoke-Guard 'Describe' @(,[string]$link)).Contains($target)
    }
    Test-Case 'a plain folder shows only its own path' {
        $plain = New-Fixture 'shown-plain'
        (Invoke-Guard 'Describe' @(,[string]$plain)) -eq $plain
    }

    # --- real deletes, fixtures only ---------------------------------------------
    Write-Host 'DeleteEntry, on temp fixtures'
    Test-Case 'control: an ordinary file really is deleted' {
        $dir = New-Fixture 'cache-plain'
        $file = Join-Path $dir 'keep.txt'
        (Invoke-DeleteEntry $file $false $dir) -and -not (Test-Path $file)
    }
    Test-Case 'a file reached through a junction to a protected root survives' {
        $target = New-Fixture 'protected-e'
        Add-ProtectedRoot $target
        $link = New-Junction (Join-Path $Work 'entry-to-protected') $target
        [void](Invoke-DeleteEntry (Join-Path $link 'keep.txt') $false $link)
        Test-Path (Join-Path $target 'keep.txt')
    }
    Test-Case 'a folder reached through a junction to a protected root survives' {
        $target = New-Fixture 'protected-f'
        $inner = New-Folder (Join-Path $target 'inner')
        [IO.File]::WriteAllText((Join-Path $inner 'keep.txt'), 'fixture')
        Add-ProtectedRoot $target
        $link = New-Junction (Join-Path $Work 'folder-to-protected') $target
        [void](Invoke-DeleteEntry (Join-Path $link 'inner') $true $link)
        Test-Path (Join-Path $inner 'keep.txt')
    }
    Test-Case 'a junction inside a cache pointing at a protected root is left alone' {
        $dir = New-Fixture 'cache-with-protected-link'
        $target = New-Fixture 'protected-g'
        Add-ProtectedRoot $target
        $link = New-Junction (Join-Path $dir 'link') $target
        [void](Invoke-DeleteEntry $link $true $dir)
        (Test-Path $link) -and (Test-Path (Join-Path $target 'keep.txt'))
    }
    Test-Case 'a junction inside a cache is unlinked and its target survives' {
        $dir = New-Fixture 'cache-with-link'
        $target = New-Fixture 'link-target'
        $link = New-Junction (Join-Path $dir 'link') $target
        (Invoke-DeleteEntry $link $true $dir) -and -not (Test-Path $link) -and (Test-Path (Join-Path $target 'keep.txt'))
    }
    Test-Case 'a read-only file deep in a tree is deleted' {
        $dir = New-Fixture 'cache-readonly'
        $inner = New-Folder (Join-Path $dir 'mod\pkg')
        $file = Join-Path $inner 'locked.txt'
        [IO.File]::WriteAllText($file, 'fixture')
        [IO.File]::SetAttributes($file, 'ReadOnly')
        (Invoke-DeleteEntry (Join-Path $dir 'mod') $true $dir) -and -not (Test-Path (Join-Path $dir 'mod'))
    }
    Test-Case 'a junction deep in a tree is unlinked and its target survives' {
        $dir = New-Fixture 'cache-deep-link'
        $target = New-Fixture 'deep-target'
        $inner = New-Folder (Join-Path $dir 'a\b')
        [void](New-Junction (Join-Path $inner 'link') $target)
        (Invoke-DeleteEntry (Join-Path $dir 'a') $true $dir) -and -not (Test-Path (Join-Path $dir 'a')) -and (Test-Path (Join-Path $target 'keep.txt'))
    }
    Test-Case 'a cancelled delete stops and reports the folder as not gone' {
        $dir = New-Fixture 'cache-cancel'
        $m = $script:Sweeper.GetMethod('DeleteTree', $Flags)
        $cancelled = New-Object Threading.CancellationToken($true)
        $callArgs = [object[]]@([string]$dir, $cancelled, [long]0)
        $gone = $m.Invoke($null, $callArgs)
        (-not $gone) -and (Test-Path (Join-Path $dir 'keep.txt'))
    }

    # --- what gets measured and deleted --------------------------------------------
    Write-Host 'Target rules'
    function New-Target { [Activator]::CreateInstance($asm.GetType('CacheSweep.Target')) }
    function Get-EntryNames($target, $root) {
        $m = $script:Sweeper.GetMethod('EntriesOf', $Flags)
        @($m.Invoke($null, @($target, [string]$root)) | ForEach-Object { Split-Path -Leaf $_.GetType().GetField('Path').GetValue($_) })
    }
    function Get-Catalog($advanced) {
        $settings = [Activator]::CreateInstance($asm.GetType('CacheSweep.Settings'))
        $settings.GetType().GetField('ShowAdvanced').SetValue($settings, [bool]$advanced)
        $asm.GetType('CacheSweep.Catalog').GetMethod('Build', $Flags).Invoke($null, @(,$settings))
    }
    function Find-Target($list, $name) { @($list | Where-Object { $_.GetType().GetField('Name').GetValue($_) -eq $name })[0] }
    function Get-Value($target, $field) { $target.GetType().GetField($field).GetValue($target) }

    Test-Case 'kept names and prefixes are not listed for deletion' {
        $dir = New-Fixture 'rules-keep'
        [void](New-Folder (Join-Path $dir '_npx'))
        [void](New-Folder (Join-Path $dir 'mcp-chrome-profile'))
        [void](New-Folder (Join-Path $dir 'chromium-1'))
        $t = New-Target
        $t.GetType().GetField('Exclude').SetValue($t, [string[]]@('_npx', 'mcp-*'))
        $names = Get-EntryNames $t $dir
        ($names -contains 'chromium-1') -and ($names -contains 'keep.txt') -and ($names -notcontains '_npx') -and ($names -notcontains 'mcp-chrome-profile')
    }
    Test-Case 'entries newer than the age limit are not listed for deletion' {
        $dir = New-Fixture 'rules-age'
        $old = Join-Path $dir 'old.txt'
        [IO.File]::WriteAllText($old, 'fixture')
        [IO.File]::SetLastWriteTimeUtc($old, [DateTime]::UtcNow.AddDays(-30))
        $t = New-Target
        $t.GetType().GetField('MinAgeDays').SetValue($t, 7)
        $names = Get-EntryNames $t $dir
        ($names -contains 'old.txt') -and ($names -notcontains 'keep.txt')
    }
    Test-Case 'a target is held while its tool runs, and free when it does not' {
        $t = New-Target
        $find = $asm.GetType('CacheSweep.RunningTools').GetMethod('Find', $Flags)
        $t.GetType().GetField('Tools').SetValue($t, [string[]]@((Get-Process -Id $PID).ProcessName))
        $running = @($find.Invoke($null, @(,$t))).Count
        $t.GetType().GetField('Tools').SetValue($t, [string[]]@('no-such-process-devsuite'))
        ($running -eq 1) -and (@($find.Invoke($null, @(,$t))).Count -eq 0)
    }
    Test-Case 'a ProgramData subfolder is refused' {
        $sub = Join-Path $env:ProgramData 'Microsoft'
        if (-not (Test-Path $sub)) { return 'skip' }
        Test-Refused $sub
    }

    Write-Host 'Catalog'
    $plain = Get-Catalog $false
    $full  = Get-Catalog $true
    Test-Case 'Windows Update is not offered at all' {
        ($null -eq (Find-Target $full 'Windows Update'))
    }
    Test-Case 'advanced items are hidden by default' {
        @($plain | Where-Object { Get-Value $_ 'Advanced' }).Count -eq 0
    }
    Test-Case 'installed SDK components and shared stores are advanced' {
        $ok = $true
        foreach ($n in 'Android NDK', 'Android emulator', 'Android build-tools', 'pnpm store', 'conda packages', 'Unity Accelerator') {
            $t = Find-Target $full $n
            if ($null -eq $t -or -not (Get-Value $t 'Advanced')) { $ok = $false }
        }
        $ok
    }
    Test-Case 'npm keeps npx packages, which have their own row' {
        (Get-Value (Find-Target $plain 'npm cache') 'Exclude') -contains '_npx'
    }
    Test-Case 'Playwright keeps MCP browser profiles' {
        (Get-Value (Find-Target $plain 'Playwright browsers') 'Exclude') -contains 'mcp-*'
    }
    Test-Case 'User Temp only offers entries older than a week' {
        (Get-Value (Find-Target $plain 'User Temp') 'MinAgeDays') -eq 7
    }
    function Get-Remaining($targets) {
        $type = $asm.GetType('CacheSweep.Target')
        $list = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($type))
        foreach ($t in $targets) { [void]$list.GetType().GetMethod('Add').Invoke($list, @($t)) }
        $kept = $asm.GetType('CacheSweep.Catalog').GetMethod('WithoutNested', $Flags).Invoke($null, @(,$list))
        @($kept | ForEach-Object { Get-Value $_ 'Name' })
    }
    function New-Selected($name, $path, $keep) {
        $t = New-Target
        $t.GetType().GetField('Name').SetValue($t, $name)
        $t.GetType().GetField('Exclude').SetValue($t, [string[]]@($keep))
        [void]$t.GetType().GetField('Found').GetValue($t).Add((Join-Path $Tmp $path))
        $t
    }
    Test-Case 'a nested target the parent keeps stays selected' {
        $left = Get-Remaining @((New-Selected 'parent' 'cache' '_npx'), (New-Selected 'child' 'cache\_npx' @()))
        ($left -contains 'parent') -and ($left -contains 'child')
    }
    Test-Case 'a nested target the parent empties goes with it' {
        $left = Get-Remaining @((New-Selected 'parent' 'cache' @()), (New-Selected 'child' 'cache\inner' @()))
        ($left.Count -eq 1) -and ($left -contains 'parent')
    }
    Test-Case 'no description promises more than the tool does' {
        $bad = @($full | Where-Object { (Get-Value $_ 'Description') -match 'Pure junk|Safe to|when needed|Unneeded' })
        $bad.Count -eq 0
    }
}
catch {
    $script:Failed++
    Write-Host "  FAIL  setup  ($($_.Exception.Message))"
}
finally {
    # Unlink every junction first, so the temp folder is plain files when it goes.
    $linksLeft = 0
    foreach ($link in $script:Links) {
        try { if ([IO.Directory]::Exists($link)) { [IO.Directory]::Delete($link, $false) } }
        catch { $linksLeft++ }
    }
    if ($linksLeft -eq 0) {
        try { [IO.Directory]::Delete($Tmp, $true) } catch { Write-Host "Temp folder left behind: $Tmp" }
    } else {
        Write-Host "Junctions could not be unlinked, temp folder left in place: $Tmp"
    }
}

Write-Host ''
Write-Host ("{0} passed, {1} failed, {2} skipped" -f $script:Passed, $script:Failed, $script:Skipped)
exit $script:Failed
