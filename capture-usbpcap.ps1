param(
    [string]$Name = 'capture',
    [int]$Seconds = 5,
    [int]$Hub = 2
)

$ErrorActionPreference = 'Stop'
$captureExe = 'C:\Program Files\USBPcap\USBPcapCMD.exe'
$captureDirectory = 'C:\USBPcapG7'
New-Item -ItemType Directory -Path $captureDirectory -Force | Out-Null
$capture = Join-Path $captureDirectory ($Name + '.pcap')
$stdout = Join-Path $captureDirectory ($Name + '.stdout.txt')
$stderr = Join-Path $captureDirectory ($Name + '.stderr.txt')
$device = "\\.\USBPcap$Hub"

Remove-Item -LiteralPath $capture, $stdout, $stderr -Force -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $captureExe `
    -ArgumentList @('-d', $device, '-o', $capture, '-A', '--inject-descriptors', '-s', '512') `
    -RedirectStandardOutput $stdout -RedirectStandardError $stderr `
    -WindowStyle Hidden -PassThru
Start-Sleep -Seconds $Seconds
if (-not $process.HasExited) {
    Stop-Process -Id $process.Id
    $process.WaitForExit()
}
$size = if (Test-Path -LiteralPath $capture) { (Get-Item -LiteralPath $capture).Length } else { 0 }
"name=$Name hub=$Hub seconds=$Seconds bytes=$size finished=$(Get-Date -Format o)" | Add-Content -LiteralPath (Join-Path $captureDirectory 'capture.log')
