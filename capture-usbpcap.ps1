param(
    [string]$Name = 'capture',
    [int]$Seconds = 5,
    [int]$Hub = 2,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'captures\local'),
    [string]$CaptureExe = 'C:\Program Files\USBPcap\USBPcapCMD.exe'
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CaptureExe)) {
    throw "USBPcapCMD.exe was not found at $CaptureExe"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$capture = Join-Path $OutputDirectory ($Name + '.pcap')
$stdout = Join-Path $OutputDirectory ($Name + '.stdout.txt')
$stderr = Join-Path $OutputDirectory ($Name + '.stderr.txt')
$device = "\\.\USBPcap$Hub"

Remove-Item -LiteralPath $capture, $stdout, $stderr -Force -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $CaptureExe `
    -ArgumentList @('-d', $device, '-o', $capture, '-A', '--inject-descriptors', '-s', '512') `
    -RedirectStandardOutput $stdout -RedirectStandardError $stderr `
    -WindowStyle Hidden -PassThru
Start-Sleep -Seconds $Seconds
if (-not $process.HasExited) {
    Stop-Process -Id $process.Id
    $process.WaitForExit()
}
$size = if (Test-Path -LiteralPath $capture) { (Get-Item -LiteralPath $capture).Length } else { 0 }
"name=$Name hub=$Hub seconds=$Seconds bytes=$size finished=$(Get-Date -Format o)" | Add-Content -LiteralPath (Join-Path $OutputDirectory 'capture.log')
