$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\hidmaestro\PandaControlPanel\PandaControlPanel.csproj'
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { throw 'The application version is missing from PandaControlPanel.csproj.' }

$artifactRoot = Join-Path $root 'artifacts'
$packageName = "PandaPro8K-research-captures-v$version-sanitized"
$publishDirectory = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$captures = Get-ChildItem -LiteralPath $root -File -Filter 'capture-*.json' | Sort-Object Name
if (-not $captures) { throw 'No local capture-*.json files were found.' }

foreach ($capture in $captures) {
    $destination = Join-Path $publishDirectory $capture.Name
    & py -3 (Join-Path $root 'g7diag.py') sanitize $capture.FullName $destination
    if ($LASTEXITCODE -ne 0) { throw "Sanitizing $($capture.Name) failed." }
}

Copy-Item -LiteralPath (Join-Path $root 'CAPTURES.md') -Destination (Join-Path $publishDirectory 'README.md') -Force
@(
    'PROTOCOL.md',
    'TEST_RESULTS.md',
    'RESEARCH_LOG.md',
    'RATE_TESTING.md',
    'STEAM_INPUT_RATE.md'
) | ForEach-Object {
    Copy-Item -LiteralPath (Join-Path $root $_) -Destination $publishDirectory -Force
}

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -LiteralPath $publishDirectory -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Built $zipPath"
Write-Host "SHA256 $hash"
