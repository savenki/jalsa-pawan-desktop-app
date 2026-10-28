$ErrorActionPreference = 'Stop'
$taskExe = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'JalsaPawan.exe')).ProviderPath
$taskPrograms = [Environment]::GetFolderPath('Programs')
$taskShortcutFolder = Join-Path $taskPrograms 'Jalsa Pawan'
New-Item -ItemType Directory -Force -Path $taskShortcutFolder | Out-Null
$taskShell = New-Object -ComObject WScript.Shell
$taskOldSettings=Join-Path $taskShortcutFolder 'Jalsa Pawan - Settings.lnk'
if(Test-Path -LiteralPath $taskOldSettings){Remove-Item -LiteralPath $taskOldSettings}
foreach ($taskEntry in @(
    @{Name='Jalsa Pawan - Start or Stop'; Args='--toggle'; Description='Click to start the desktop pet. Click again to stop it.'},
    @{Name='Jalsa Pawan - Stop'; Args='--quit'; Description='Stop the desktop companion and all its reminders.'},
    @{Name='Jalsa Pawan - Control Panel'; Args='--settings'; Description='Latest build: focus timer, reminders, mouse katana and animation controls.'}
)) {
    $taskShortcut = $taskShell.CreateShortcut((Join-Path $taskShortcutFolder ($taskEntry.Name + '.lnk')))
    $taskShortcut.TargetPath = $taskExe
    $taskShortcut.Arguments = $taskEntry.Args
    $taskShortcut.WorkingDirectory = $PSScriptRoot
    $taskShortcut.IconLocation = $taskExe + ',0'
    $taskShortcut.Description = $taskEntry.Description
    $taskShortcut.Save()
}
Write-Output ('Start menu shortcuts created in: ' + $taskShortcutFolder)
