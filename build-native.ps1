$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$vsDevCmd = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\VsDevCmd.bat'
$output = Join-Path $root 'build\native'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = Join-Path $root 'src\bridge\panda_probe.cpp'
$command = "call `"$vsDevCmd`" -arch=x64 -host_arch=x64 && cl /nologo /std:c++20 /O2 /EHsc /W4 /DUNICODE /D_UNICODE `"$source`" /Fe:`"$output\panda_probe.exe`""
cmd.exe /d /s /c $command
if ($LASTEXITCODE -ne 0) { throw "Native build failed with exit code $LASTEXITCODE" }
Write-Host "Built $output\panda_probe.exe"
