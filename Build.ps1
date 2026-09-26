param([string]$OutputName = 'TextSnip.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$wpf = Join-Path (Split-Path $compiler) 'WPF'
& $compiler "/win32icon:$PSScriptRoot\TextSnip.ico" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "/out:$PSScriptRoot\$OutputName" "/r:$wpf\WindowsBase.dll" "/r:$wpf\UIAutomationClient.dll" "/r:$wpf\UIAutomationTypes.dll" "$PSScriptRoot\TextSnip.cs" "$PSScriptRoot\TextCleanup.cs" "$PSScriptRoot\CleanupTests.cs" "$PSScriptRoot\DirectText.cs" "$PSScriptRoot\DirectTextTests.cs" "$PSScriptRoot\Design.cs" "$PSScriptRoot\DesignPreview.cs" "$PSScriptRoot\ReleaseInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
