param(
    [string]$OutputDirectory = "$PSScriptRoot\hub-probes-elevated"
)

$ErrorActionPreference = 'Continue'
$captureExe = 'C:\Program Files\USBPcap\USBPcapCMD.exe'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$log = Join-Path $OutputDirectory 'probe.log'
"Started $(Get-Date -Format o)" | Set-Content -LiteralPath $log

foreach ($number in 1..12) {
    $capture = Join-Path $OutputDirectory ("hub{0}.pcap" -f $number)
    $stdout = Join-Path $OutputDirectory ("hub{0}.stdout.txt" -f $number)
    $stderr = Join-Path $OutputDirectory ("hub{0}.stderr.txt" -f $number)
    $device = "\\.\USBPcap$number"
    $process = Start-Process -FilePath $captureExe `
        -ArgumentList @('-d', $device, '-o', $capture, '-A', '--inject-descriptors', '-s', '256') `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr `
        -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 800
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }
    $size = if (Test-Path -LiteralPath $capture) { (Get-Item -LiteralPath $capture).Length } else { 0 }
    "USBPcap$number bytes=$size exit=$($process.ExitCode)" | Add-Content -LiteralPath $log
}

"Finished $(Get-Date -Format o)" | Add-Content -LiteralPath $log
