$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$hidMaestroRoot = Join-Path $root "vendor\HIDMaestro"
$project = Join-Path $root "src\hidmaestro\PandaControlPanel\PandaControlPanel.csproj"
$nativeSource = Join-Path $root "src\bridge\panda_enhanced_stream.cpp"
$artifactRoot = Join-Path $root "artifacts"

if (-not (Test-Path -LiteralPath (Join-Path $hidMaestroRoot "scripts\build_all.cmd"))) {
    throw "HIDMaestro is missing. Clone with --recurse-submodules or run 'git submodule update --init --recursive'."
}

$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { throw "The application version is missing from PandaControlPanel.csproj." }

$packageName = "PandaPro8K-v$version-win-x64"
$publishDirectory = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "Visual Studio 2022 Build Tools were not found."
}

$installationPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $installationPath) { throw "The Visual C++ x64 tools are not installed." }
$vsDevCmd = Join-Path $installationPath "Common7\Tools\VsDevCmd.bat"

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Write-Host "Building HIDMaestro..."
& cmd.exe /d /c "`"$(Join-Path $hidMaestroRoot 'scripts\build_all.cmd')`""
if ($LASTEXITCODE -ne 0) { throw "HIDMaestro build failed with exit code $LASTEXITCODE." }

Write-Host "Building the enhanced GameSir reader..."
$nativeOutput = Join-Path $publishDirectory "panda_enhanced_stream.exe"
$nativeCommand = "call `"$vsDevCmd`" -arch=x64 -host_arch=x64 >nul && cl /nologo /std:c++20 /O2 /EHsc /W4 /DUNICODE /D_UNICODE `"$nativeSource`" /Fe:`"$nativeOutput`""
& cmd.exe /d /s /c $nativeCommand
if ($LASTEXITCODE -ne 0) { throw "Native reader build failed with exit code $LASTEXITCODE." }

Write-Host "Publishing Panda Pro 8K..."
dotnet publish $project -t:Rebuild -c Release -r win-x64 --self-contained false -o $publishDirectory -nologo -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Application publish failed with exit code $LASTEXITCODE." }

Get-ChildItem -LiteralPath $publishDirectory -File -Filter "*.pdb" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Copy-Item -LiteralPath (Join-Path $root "packaging\Start-PandaPro8K.ps1") -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $root "packaging\Install.ps1") -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $root "packaging\README - First Use.txt") -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $root "INSTALL.md") -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $hidMaestroRoot "LICENSE") -Destination (Join-Path $publishDirectory "HIDMaestro.LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $root "THIRD_PARTY_NOTICES.md") -Destination $publishDirectory -Force

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -LiteralPath $publishDirectory -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Built $zipPath"
Write-Host "SHA256 $hash"
