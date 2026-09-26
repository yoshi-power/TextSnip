$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process -FilePath powershell.exe -Verb RunAs -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"')
    exit
}
try {
    Write-Host 'TextSnip: Installing Korean, English and Japanese Windows OCR.'
    Write-Host 'Internet access is required. This may take several minutes.'
    foreach ($language in @('ko-KR', 'en-US', 'ja-JP')) {
        $name = 'Language.OCR~~~' + $language + '~0.0.1.0'
        $capability = Get-WindowsCapability -Online -Name $name
        if ($capability.State -eq 'Installed') { Write-Host ($language + ': already installed'); continue }
        $result = Add-WindowsCapability -Online -Name $name
        Write-Host ($language + ': installed. Restart needed: ' + $result.RestartNeeded)
    }
    Write-Host 'Done. Exit TextSnip from its tray menu, then start it again.'
} catch {
    Write-Host ('Installation failed: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'You can also install OCR from Windows Settings > Time & language > Language options.'
}
Read-Host 'Press Enter to close'
