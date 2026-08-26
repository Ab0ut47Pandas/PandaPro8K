$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\hidmaestro\PandaControlPanel\PandaControlPanel.csproj'
$artifactRoot = Join-Path $root 'artifacts'

$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { throw 'The application version is missing from PandaControlPanel.csproj.' }

$packageName = "PandaPro8K-tools-v$version-win-x64"
$publishDirectory = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$projects = @(
    'AuxInputChangeCapture',
    'EdgeWireCapture',
    'GamepadRawCapture',
    'PandaMappingCheck',
    'PhysicalXInputChangeCapture',
    'XInputChangeCapture'
)

foreach ($name in $projects) {
    $source = Join-Path $root "tools\$name\$name.csproj"
    $destination = Join-Path $publishDirectory "bin\$name"
    Write-Host "Publishing $name..."
    dotnet publish $source -c Release -r win-x64 --self-contained false -o $destination `
        -nologo -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "$name failed with exit code $LASTEXITCODE." }
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $installationPath = & $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($installationPath) {
        $vsDevCmd = Join-Path $installationPath 'Common7\Tools\VsDevCmd.bat'
        $nativeSource = Join-Path $root 'src\bridge\panda_probe.cpp'
        $nativeDestination = Join-Path $publishDirectory 'bin\panda_probe.exe'
        $nativeCommand = "call `"$vsDevCmd`" -arch=x64 -host_arch=x64 >nul && cl /nologo /std:c++20 /O2 /EHsc /W4 /DUNICODE /D_UNICODE `"$nativeSource`" /Fe:`"$nativeDestination`""
        Write-Host 'Building panda_probe...'
        & cmd.exe /d /s /c $nativeCommand
        if ($LASTEXITCODE -ne 0) { throw "panda_probe failed with exit code $LASTEXITCODE." }
    }
}

$pythonDirectory = Join-Path $publishDirectory 'python'
$powershellDirectory = Join-Path $publishDirectory 'powershell'
New-Item -ItemType Directory -Path $pythonDirectory, $powershellDirectory -Force | Out-Null

@(
    'g7diag.py',
    'g7enhanced.py',
    'g7monitor.py',
    'requirements.txt',
    'tools\live_input_trace.py',
    'tools\logo_a_state_capture.py',
    'tools\trigger_probe.py'
) | ForEach-Object {
    Copy-Item -LiteralPath (Join-Path $root $_) -Destination $pythonDirectory -Force
}

@(
    'capture-descriptor-switch.ps1',
    'capture-usbpcap.ps1',
    'probe-usbpcap.ps1',
    'tools\run-live-capture.ps1'
) | ForEach-Object {
    Copy-Item -LiteralPath (Join-Path $root $_) -Destination $powershellDirectory -Force
}

Copy-Item -LiteralPath (Join-Path $root 'tools\README.md') -Destination (Join-Path $publishDirectory 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'CAPTURES.md') -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $publishDirectory -Force

Get-ChildItem -LiteralPath $publishDirectory -Recurse -File -Filter '*.pdb' -ErrorAction SilentlyContinue |
    Remove-Item -Force

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -LiteralPath $publishDirectory -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Built $zipPath"
Write-Host "SHA256 $hash"
