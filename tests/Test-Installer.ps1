[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$OriginalGame,
    [Parameter(Mandatory=$true)][string]$WorkDirectory
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$setup=Join-Path $projectRoot 'artifacts\CossacksII-Unofficial-Restoration-0.1.1-preview-setup.exe'
$original=[IO.Path]::GetFullPath($OriginalGame).TrimEnd('\')
$work=[IO.Path]::GetFullPath($WorkDirectory).TrimEnd('\')
if(Test-Path -LiteralPath $work) { throw 'Use a new empty work-directory path. Tests never delete existing directories.' }
if($work.StartsWith($original+'\',[StringComparison]::OrdinalIgnoreCase) -or $original.StartsWith($work+'\',[StringComparison]::OrdinalIgnoreCase) -or $work -eq $original) { throw 'Test path must be separate from the original game.' }
if((Get-FileHash -LiteralPath (Join-Path $original 'engine.exe')).Hash -ne 'DB52B8A6926C63EF2B7BBF921C01B8F95A4ECD9F2B63BDA18F0EDD18E45F7517') { throw 'Test input is not the supported original.' }
New-Item -ItemType Directory -Path $work -Force | Out-Null
$env:C2_RESTORATION_FORBID_GAME_LAUNCH='1'
$env:C2_RESTORATION_TEST_DESKTOP=Join-Path $work 'Isolated Desktop'
New-Item -ItemType Directory -Path $env:C2_RESTORATION_TEST_DESKTOP -Force | Out-Null
$checks=New-Object Collections.Generic.List[object]
$script:sequence=0
function Assert([bool]$Condition,[string]$Name) {
    if(!$Condition) { throw "FAILED: $Name" }
    $checks.Add([pscustomobject]@{name=$Name;result='PASS'})
}
function Snapshot([string]$Directory) {
    $map=@{}
    foreach($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Force) {
        $map[$file.FullName.Substring($Directory.Length+1)]=(Get-FileHash -LiteralPath $file.FullName).Hash
    }
    return $map
}
function Same([hashtable]$First,[hashtable]$Second) {
    if($First.Count -ne $Second.Count) { return $false }
    foreach($key in $First.Keys) { if($Second[$key] -ne $First[$key]) { return $false } }
    return $true
}
function Fixture([string]$Name) {
    $dir=Join-Path $work $Name
    New-Item -ItemType Directory -Path $dir,(Join-Path $dir 'Save'),(Join-Path $dir 'Players') -Force | Out-Null
    foreach($name in @('engine.exe','settings.xml','settings.cfg','X3D8.DLL','d3d8.dll')) {
        if(Test-Path -LiteralPath (Join-Path $original $name)) { Copy-Item -LiteralPath (Join-Path $original $name) -Destination (Join-Path $dir $name) }
    }
    [IO.File]::WriteAllText((Join-Path $dir 'Save\user-save.sav'),'Synthetic saved-game preservation sentinel')
    [IO.File]::WriteAllText((Join-Path $dir 'Players\user-profile.dat'),'Synthetic player preservation sentinel')
    [IO.File]::WriteAllText((Join-Path $dir 'log.txt'),'Original writable log')
    [IO.File]::WriteAllText((Join-Path $dir 'dxvk.conf'),'# Original user configuration')
    return $dir
}
function InvokeSetup([string]$Executable,[string[]]$Arguments) {
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$Executable; $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.WindowStyle='Hidden'
    $start.Arguments=(($Arguments | ForEach-Object { if($_.Contains('"')) { throw 'Unsupported quote in test argument' }; '"'+$_+'"' }) -join ' ')
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(120000)) { $process.Kill(); throw 'Installer test timed out.' }
    return $process.ExitCode
}
function Run([string]$Game,[string]$Action,[bool]$Success=$true,[string[]]$Extra=@(),[string]$Executable=$setup) {
    $script:sequence++
    $report=Join-Path $work ('command-'+$script:sequence+'.json')
    $argsList=@('--silent',$Action,'--game',$Game,'--resolution','2560x1440','--report',$report)+$Extra
    $exitCode=InvokeSetup $Executable $argsList
    $result=Get-Content -Encoding UTF8 -LiteralPath $report -Raw | ConvertFrom-Json
    Assert (($exitCode -eq 0) -eq $Success) ('CLI exit '+$script:sequence)
    Assert ($result.success -eq $Success) ('CLI result '+$script:sequence)
    Assert (!$result.gameLaunched) ('No game launch '+$script:sequence)
    return $result
}
$protected=Snapshot $original
$game=Fixture 'Game with spaces — проверка'
$before=Snapshot $game
$shortcut=Join-Path $env:C2_RESTORATION_TEST_DESKTOP 'Cossacks II — Unofficial Restoration.lnk'
[IO.File]::WriteAllText($shortcut,'Existing unrelated shortcut sentinel')
$shortcutBefore=(Get-FileHash -LiteralPath $shortcut).Hash
Run $game '--validate' | Out-Null
Run $game '--install' $false @('--dry-run') | Out-Null
Assert (Same $before (Snapshot $game)) 'Unsupported install dry-run rejected before mutation'
Run $game '--install' | Out-Null
Assert ((Get-FileHash -LiteralPath (Join-Path $game 'engine.exe')).Hash -eq 'EC5B4E1EB1410E66223AD86C5F69B97B2646552A5B2AEDE2B4072111D85C9605') 'Patched output matches previously validated engine'
Assert ((Get-Content -Encoding UTF8 -LiteralPath (Join-Path $game 'settings.xml') -Raw) -match '<ScreenSizeX>2560</ScreenSizeX>') '1440p settings applied'
Assert ((Get-FileHash -LiteralPath $shortcut).Hash -eq $shortcutBefore) 'Existing desktop shortcut untouched'
$statePath=Join-Path $game '.c2-restoration\state.json'
$state=Get-Content -Encoding UTF8 -LiteralPath $statePath -Raw | ConvertFrom-Json
Assert ((Split-Path $state.shortcut -Leaf) -eq 'Cossacks II — Unofficial Restoration (2).lnk') 'Collision creates separate shortcut'
$installed=Snapshot $game
Run $game '--uninstall' $false @('--dry-run') | Out-Null
Assert (Same $installed (Snapshot $game)) 'Unsupported uninstall dry-run rejected before mutation'
Run $game '--install' | Out-Null
Assert (Same $installed (Snapshot $game)) 'Repeated install is byte-idempotent'
# Silent requests cannot enter play paths, even with a valid installed game.
Run $game '--play' $false | Out-Null
Run $game '--diagnostic' $false | Out-Null
Run $game '--play' $true @('--dry-run') | Out-Null
Run $game '--diagnostic' $true @('--dry-run') | Out-Null
Assert (Same $installed (Snapshot $game)) 'Dry-run modes leave installed files and log attributes unchanged'
$launchPlan=Join-Path $game '.c2-restoration\dxwnd\launch.ini'
Assert ((Get-Content -Encoding UTF8 -LiteralPath $launchPlan -Raw) -match '(?m)^flagg0=1208221704\r?$') 'MenuFill / borderless flags preserved'
Assert ((Get-Content -Encoding UTF8 -LiteralPath $launchPlan -Raw) -match '(?m)^flagx0=0\r?$') 'SETRESOLUTION remains disabled'
Assert ((Get-FileHash -LiteralPath (Join-Path $game 'Save\user-save.sav')).Hash -eq $before['Save\user-save.sav']) 'Save preserved through install'
# Preserve a later user change separately while restoring the exact original.
$changed=Join-Path $game 'settings.cfg'
[IO.File]::AppendAllText($changed,"`r`n# User changed this after installing")
$changedHash=(Get-FileHash -LiteralPath $changed).Hash
[IO.File]::WriteAllText((Join-Path $game 'Save\new-after-install.sav'),'New user save created after installing')
[IO.File]::WriteAllText((Join-Path $game '.c2-restoration\user-notes.txt'),'Unowned recovery-folder content')
Run $game '--install' $false | Out-Null
Run $game '--uninstall' | Out-Null
foreach($name in $before.Keys) { Assert ((Get-FileHash -LiteralPath (Join-Path $game $name)).Hash -eq $before[$name]) ('Restored original '+$name) }
$state=Get-Content -Encoding UTF8 -LiteralPath $statePath -Raw | ConvertFrom-Json
Assert (@($state.conflicts | Where-Object { (Get-FileHash -LiteralPath $_).Hash -eq $changedHash }).Count -eq 1) 'Later configuration preserved in conflicts'
Assert (!(Test-Path -LiteralPath $state.shortcut)) 'Own shortcut removed'
Assert ((Get-FileHash -LiteralPath $shortcut).Hash -eq $shortcutBefore) 'Preexisting shortcut survives uninstall'
Assert ((Get-Content -Encoding UTF8 -LiteralPath (Join-Path $game 'Save\new-after-install.sav') -Raw) -eq 'New user save created after installing') 'New user saves survive uninstall'
Assert ((Get-Content -Encoding UTF8 -LiteralPath (Join-Path $game '.c2-restoration\user-notes.txt') -Raw) -eq 'Unowned recovery-folder content') 'Unowned recovery content preserved'
Run $game '--uninstall' | Out-Null
Run $game '--install' | Out-Null
Assert (@(Get-ChildItem -LiteralPath $game -Directory -Filter '.c2-restoration-history-*').Count -eq 1) 'Reinstall after removal archives previous history'
$state=Get-Content -Encoding UTF8 -LiteralPath $statePath -Raw | ConvertFrom-Json
[IO.File]::AppendAllText($state.shortcut,'User edited this shortcut')
$editedShortcutHash=(Get-FileHash -LiteralPath $state.shortcut).Hash
$backup=Join-Path $game '.c2-restoration\backups\engine.exe'
[IO.File]::AppendAllText($backup,'Corrupted backup test')
$corruptBefore=Snapshot $game
Run $game '--uninstall' $false | Out-Null
Assert (Same $corruptBefore (Snapshot $game)) 'Corrupted backup stops uninstall before mutations'
Copy-Item -LiteralPath (Join-Path $original 'engine.exe') -Destination $backup -Force
[IO.File]::AppendAllText((Join-Path $game 'engine.exe'),'User modified installed engine')
$editedEngineHash=(Get-FileHash -LiteralPath (Join-Path $game 'engine.exe')).Hash
Run $game '--uninstall' | Out-Null
$state=Get-Content -Encoding UTF8 -LiteralPath $statePath -Raw | ConvertFrom-Json
Assert ((Get-FileHash -LiteralPath $state.shortcut).Hash -eq $editedShortcutHash) 'Edited owned shortcut preserved'
Assert (@($state.conflicts | Where-Object { (Get-FileHash -LiteralPath $_).Hash -eq $editedEngineHash }).Count -eq 1) 'Later engine modification preserved in conflicts'
Assert ((Get-FileHash -LiteralPath (Join-Path $game 'engine.exe')).Hash -eq $before['engine.exe']) 'Original engine restored after conflicting edit'
# The normal GUI removal runs from the installed, locked maintenance executable.
$self=Fixture 'Self removal and reinstall'
$selfBefore=Snapshot $self
Run $self '--install' $true @('--no-shortcut') | Out-Null
Run $self '--uninstall' $true @() (Join-Path $self '.c2-restoration\Restoration.exe') | Out-Null
Assert (Test-Path -LiteralPath (Join-Path $self '.c2-restoration\Restoration.exe')) 'Running maintenance EXE retained on self-removal'
$oldHistory=Snapshot (Join-Path $self '.c2-restoration')
Run $self '--install' $true @('--no-shortcut') | Out-Null
$archive=@(Get-ChildItem -LiteralPath $self -Directory -Filter '.c2-restoration-history-*')
Assert ($archive.Count -eq 1 -and (Same $oldHistory (Snapshot $archive[0].FullName))) 'Self-removal history archived byte-for-byte before successful reinstall'
Run $self '--uninstall' | Out-Null
foreach($name in $selfBefore.Keys) { Assert ((Get-FileHash -LiteralPath (Join-Path $self $name)).Hash -eq $selfBefore[$name]) ('Self-removal cycle restores '+$name) }
# Reports must never overwrite existing data or create files inside the game.
$reportSentinel=Join-Path $work 'existing-report.txt'
[IO.File]::WriteAllText($reportSentinel,'Keep this existing file')
$reportHash=(Get-FileHash -LiteralPath $reportSentinel).Hash
$exitCode=InvokeSetup $setup @('--silent','--install','--game',$self,'--report',$reportSentinel)
Assert ($exitCode -eq 1 -and (Get-FileHash -LiteralPath $reportSentinel).Hash -eq $reportHash) 'Existing report destination preserved before install'
Assert ((Get-FileHash -LiteralPath (Join-Path $self 'engine.exe')).Hash -eq $selfBefore['engine.exe']) 'Report rejection prevents installation'
$exitCode=InvokeSetup $setup @('--silent','--validate','--game',(Join-Path $work 'missing-game'),'--report',$reportSentinel)
Assert ($exitCode -eq 1 -and (Get-FileHash -LiteralPath $reportSentinel).Hash -eq $reportHash) 'Error report cannot overwrite existing data'
$inside=Join-Path $self 'new-report.json'
$exitCode=InvokeSetup $setup @('--silent','--install','--game',$self,'--report',$inside)
Assert ($exitCode -eq 1 -and !(Test-Path -LiteralPath $inside)) 'Report inside game rejected before mutation'
# A journal is recovery metadata, not authority to remove arbitrary user files.
$malformed=Fixture 'Malformed journal'
Run $malformed '--install' $true @('--no-shortcut') | Out-Null
$manifestPath=Join-Path $malformed '.c2-restoration\state.json'
$manifestBytes=[IO.File]::ReadAllBytes($manifestPath)
foreach($kind in @('unowned-save','duplicate-name','backup-redirect','invalid-hash','null-files')) {
    $manifest=[Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json
    switch($kind) {
        'unowned-save' { $manifest.files[0].name='Save\user-save.sav'; $manifest.files[0].before=$null; $manifest.files[0].after=(Get-FileHash -LiteralPath (Join-Path $malformed 'Save\user-save.sav')).Hash; $manifest.files[0].backup='.c2-restoration\backups\Save\user-save.sav' }
        'duplicate-name' { $manifest.files[1]=$manifest.files[0] }
        'backup-redirect' { $manifest.files[0].backup='Save\user-save.sav' }
        'invalid-hash' { $manifest.files[0].after='not a hash' }
        'null-files' { $manifest.files=$null }
    }
    [IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 8))
    $invalidBefore=Snapshot $malformed
    Run $malformed '--uninstall' $false | Out-Null
    Assert (Same $invalidBefore (Snapshot $malformed)) ('Malformed journal rejected without mutation: '+$kind)
}
[IO.File]::WriteAllBytes($manifestPath,$manifestBytes)
$foreign=Join-Path $work 'unrelated-file.txt'; [IO.File]::WriteAllText($foreign,'Unrelated external file')
$manifest=Get-Content -Encoding UTF8 -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.shortcut=$foreign; $manifest.shortcutHash=(Get-FileHash -LiteralPath $foreign).Hash
[IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 8))
Run $malformed '--uninstall' | Out-Null
Assert ((Get-FileHash -LiteralPath $foreign).Hash -eq $manifest.shortcutHash) 'Foreign shortcut record cannot delete unrelated external file'
# Wrong files must be rejected before creating a restoration folder.
$unsupported=Fixture 'Unsupported game'
[IO.File]::AppendAllText((Join-Path $unsupported 'engine.exe'),'unsupported')
$unsupportedBefore=Snapshot $unsupported
Run $unsupported '--install' $false | Out-Null
Assert (Same $unsupportedBefore (Snapshot $unsupported)) 'Unsupported EXE rejected without mutation'
$junctionTarget=Fixture 'Junction target'
$junctionBefore=Snapshot $junctionTarget
$junction=Join-Path $work 'Junction game'
New-Item -ItemType Junction -Path $junction -Target $junctionTarget | Out-Null
Run $junction '--install' $false | Out-Null
Assert (Same $junctionBefore (Snapshot $junctionTarget)) 'Junction target rejected before changes'
$readonly=Fixture 'Read only test'
$cfg=Get-Item -LiteralPath (Join-Path $readonly 'settings.cfg'); $cfg.IsReadOnly=$true
$readonlyBefore=Snapshot $readonly
Run $readonly '--install' $false | Out-Null
Assert (Same $readonlyBefore (Snapshot $readonly)) 'Read-only output rejected without mutation'
# A sharing violation on a late file forces rollback after earlier replacements.
$failure=Fixture 'Interrupted transaction'
$failureBefore=Snapshot $failure
$lock=[IO.File]::Open((Join-Path $failure 'dxvk.conf'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try { Run $failure '--install' $false @('--no-shortcut') | Out-Null } finally { $lock.Dispose() }
foreach($name in $failureBefore.Keys) { Assert ((Get-FileHash -LiteralPath (Join-Path $failure $name)).Hash -eq $failureBefore[$name]) ('Automatic rollback '+$name) }
Run $failure '--uninstall' | Out-Null
Assert ((Get-Content -Encoding UTF8 -LiteralPath (Join-Path $failure '.c2-restoration\state.json') -Raw | ConvertFrom-Json).status -eq 'removed') 'Recovery is repeatable after failed transaction'
# Denied write/delete rights must not leave an altered game. Restore this test ACL in finally.
$denied=Fixture 'Denied output permissions'
$deniedBefore=Snapshot $denied
$deniedFile=Join-Path $denied 'settings.cfg'
$originalAcl=Get-Acl -LiteralPath $deniedFile
$deniedAcl=Get-Acl -LiteralPath $deniedFile
$identity=[Security.Principal.WindowsIdentity]::GetCurrent().User
$rule=New-Object Security.AccessControl.FileSystemAccessRule($identity,([Security.AccessControl.FileSystemRights]::Write -bor [Security.AccessControl.FileSystemRights]::Delete),[Security.AccessControl.AccessControlType]::Deny)
$deniedAcl.AddAccessRule($rule)
try { Set-Acl -LiteralPath $deniedFile -AclObject $deniedAcl; Run $denied '--install' $false @('--no-shortcut') | Out-Null }
finally { Set-Acl -LiteralPath $deniedFile -AclObject $originalAcl }
Run $denied '--uninstall' | Out-Null
foreach($name in $deniedBefore.Keys) { Assert ((Get-FileHash -LiteralPath (Join-Path $denied $name)).Hash -eq $deniedBefore[$name]) ('Permission failure preserves '+$name) }
# Kill only our own installer after persisted write intent, then recover from disk.
$interrupted=Fixture 'Killed installer'
$interruptedBefore=Snapshot $interrupted
$arguments=@('--silent','--install','--no-shortcut','--game',$interrupted,'--resolution','2560x1440')
$process=Start-Process -FilePath $setup -ArgumentList (($arguments | ForEach-Object {'"'+$_+'"'}) -join ' ') -WindowStyle Hidden -PassThru
$watch=[Diagnostics.Stopwatch]::StartNew(); $killed=$false
$interruptedState=Join-Path $interrupted '.c2-restoration\state.json'
try {
    while(!$process.HasExited -and $watch.Elapsed.TotalSeconds -lt 20) {
        if(Test-Path -LiteralPath $interruptedState) {
            try { $journal=[IO.File]::ReadAllText($interruptedState) | ConvertFrom-Json } catch { continue }
            if($journal.status -eq 'installing' -and @($journal.files | Where-Object written).Count -ge 2) { $process.Kill(); $process.WaitForExit(); $killed=$true; break }
        }
        Start-Sleep -Milliseconds 1
    }
} finally { if(!$process.HasExited){$process.Kill(); $process.WaitForExit()} }
Assert $killed 'Installer terminated after persisted partial write intent'
Run $interrupted '--uninstall' | Out-Null
foreach($name in $interruptedBefore.Keys) { Assert ((Get-FileHash -LiteralPath (Join-Path $interrupted $name)).Hash -eq $interruptedBefore[$name]) ('Killed transaction recovered '+$name) }
Assert (Same $protected (Snapshot $original)) 'Original input tree unchanged'
Assert (@(Get-Process -Name engine,dxwnd -ErrorAction SilentlyContinue).Count -eq 0) 'No engine / DxWnd processes after tests'
[pscustomobject]@{result='PASS';checks=$checks.Count;fixture='Original supported engine / settings with synthetic user data; separate space-and-Cyrillic paths';gameLaunch='Forbidden; silent play requests rejected';tests=$checks} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $work 'test-results.json') -Encoding UTF8
Write-Output ('PASS: '+$checks.Count+' checks. '+(Join-Path $work 'test-results.json'))
