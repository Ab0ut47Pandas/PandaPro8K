param(
    [Parameter(Mandatory)]
    [string]$Executable,

    [Parameter(Mandatory)]
    [int]$Seconds,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, '')
& $Executable $Seconds 2>&1 | ForEach-Object {
    [System.IO.File]::AppendAllText($OutputPath, "$_$([Environment]::NewLine)")
}
