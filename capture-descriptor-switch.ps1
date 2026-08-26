$ErrorActionPreference = 'Stop'
$captureExe = 'C:\Program Files\USBPcap\USBPcapCMD.exe'
$directory = 'C:\USBPcapG7'
$capture = Join-Path $directory 'descriptor-switch.pcap'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
Remove-Item -LiteralPath $capture -Force -ErrorAction SilentlyContinue
$before = @(Get-Process USBPcapCMD -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
Start-Process -FilePath $captureExe -ArgumentList @('-d','\\.\USBPcap2','-o',$capture,'-A','-s','512') -WindowStyle Hidden | Out-Null
Start-Sleep -Seconds 32
Get-Process USBPcapCMD -ErrorAction SilentlyContinue | Where-Object { $_.Id -notin $before } | Stop-Process -Force
Start-Sleep -Seconds 1
$size = if (Test-Path -LiteralPath $capture) { (Get-Item -LiteralPath $capture).Length } else { 0 }
"descriptor-switch bytes=$size finished=$(Get-Date -Format o)" | Add-Content -LiteralPath (Join-Path $directory 'capture.log')
