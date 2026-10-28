$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskSpeech = Join-Path $env:WINDIR 'Microsoft.NET/assembly/GAC_MSIL/System.Speech/v4.0_4.0.0.0__31bf3856ad364e35/System.Speech.dll'
$taskArgs = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', ('/out:' + (Join-Path $PSScriptRoot 'JalsaPawan.exe')), '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.Linq.dll', ('/r:' + $taskSpeech), ('/win32manifest:' + (Join-Path $PSScriptRoot 'source/app.manifest')), ('/win32icon:' + (Join-Path $PSScriptRoot 'assets/app.ico')))
foreach ($taskAssembly in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')){$taskArgs += '/r:'+(Join-Path $env:WINDIR ('Microsoft.NET/Framework64/v4.0.30319/WPF/'+$taskAssembly))}
foreach ($taskAsset in @('spritesheet.png','water.png','bamboo.png','sleep.png','finisher.png','bamboo-post.png','reading.png','tree.png','aim.png')) {
    $taskArgs += '/resource:' + (Join-Path $PSScriptRoot ('assets/' + $taskAsset)) + ',' + $taskAsset
}
$taskArgs += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' | ForEach-Object {$_.FullName})
& $taskCompiler @taskArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output 'Built JalsaPawan.exe'
