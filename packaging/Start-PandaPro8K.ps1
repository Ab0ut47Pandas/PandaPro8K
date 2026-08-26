$ErrorActionPreference = "Stop"

Add-Type -AssemblyName PresentationFramework

function Show-PandaMessage {
    param(
        [string]$Message,
        [string]$Title = "Panda Pro 8K Bridge",
        [System.Windows.MessageBoxImage]$Icon = [System.Windows.MessageBoxImage]::Information
    )
    [System.Windows.MessageBox]::Show(
        $Message,
        $Title,
        [System.Windows.MessageBoxButton]::OK,
        $Icon
    ) | Out-Null
}

function Confirm-PandaAction {
    param(
        [string]$Message,
        [string]$Title = "Panda Pro 8K Bridge"
    )
    $choice = [System.Windows.MessageBox]::Show(
        $Message,
        $Title,
        [System.Windows.MessageBoxButton]::YesNo,
        [System.Windows.MessageBoxImage]::Warning
    )
    return $choice -eq [System.Windows.MessageBoxResult]::Yes
}

function Get-SteamPath {
    $path = (Get-ItemProperty -LiteralPath "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamExe
    if (-not $path -or -not (Test-Path -LiteralPath $path)) {
        $path = Join-Path ${env:ProgramFiles(x86)} "Steam\steam.exe"
    }
    if (Test-Path -LiteralPath $path) { return $path }
    return $null
}

function Get-SteamControllerInterval {
    $rateHz = 1000
    $settingsPath = Join-Path $env:LOCALAPPDATA "PandaPro8K\settings.json"
    if (Test-Path -LiteralPath $settingsPath) {
        try {
            $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
            $savedRate = [int]$settings.VirtualOutputRateHz
            if ($savedRate -eq 0 -or ($savedRate -ge 125 -and $savedRate -le 8000)) {
                $rateHz = $savedRate
            }
        }
        catch {
            $rateHz = 1000
        }
    }

    if ($rateHz -eq 0) { return 0 }
    return [Math]::Max(1, [int][Math]::Round(1000000.0 / $rateHz))
}

function Wait-ForGameSirController {
    while ($true) {
        $device = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
            Where-Object { $_.InstanceId -match "VID_3537&PID_10C[56]" } |
            Select-Object -First 1
        if ($device) { return $true }

        $choice = [System.Windows.MessageBox]::Show(
            "The GameSir controller is not detected.`n`nTurn it on in normal XInput / GameSir Connect mode, then choose OK to try again. Choose Cancel to stop.",
            "Turn on the controller",
            [System.Windows.MessageBoxButton]::OKCancel,
            [System.Windows.MessageBoxImage]::Warning
        )
        if ($choice -eq [System.Windows.MessageBoxResult]::Cancel) { return $false }
    }
}

$packageDirectory = Split-Path -Parent $PSCommandPath
$bridgePath = Join-Path $packageDirectory "PandaPro8K.exe"

if (-not (Test-Path -LiteralPath $bridgePath)) {
    Show-PandaMessage "PandaPro8K.exe is missing from the package folder." "Panda Pro 8K" ([System.Windows.MessageBoxImage]::Error)
    exit 1
}

$alreadyRunning = Get-Process -Name "PandaPro8K" -ErrorAction SilentlyContinue | Select-Object -First 1
$bridgeProcess = $alreadyRunning
$launchTime = Get-Date
$steamPath = Get-SteamPath
$steamControllerInterval = Get-SteamControllerInterval

if (-not $alreadyRunning) {
    if (-not (Wait-ForGameSirController)) { exit 7 }
}

if (Get-Process -Name "steam" -ErrorAction SilentlyContinue) {
    $restartSteam = Confirm-PandaAction "Steam is currently open.`n`nPanda Pro 8K must restart Steam once so Steam inherits the controller filter and sees only the combined virtual controller.`n`nChoose Yes to close Steam and continue. Choose No to leave Steam open and cancel."
    if (-not $restartSteam) { exit 2 }
    if (-not $steamPath) {
        Show-PandaMessage "Steam could not be located automatically. Fully exit Steam from Steam > Exit or its system-tray icon, then run Panda Pro 8K again." "Steam could not be restarted" ([System.Windows.MessageBoxImage]::Warning)
        exit 5
    }

    Start-Process -FilePath $steamPath -ArgumentList "-shutdown" -WindowStyle Hidden
    for ($attempt = 0; $attempt -lt 300; $attempt++) {
        if (-not (Get-Process -Name "steam" -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 100
    }
    if (Get-Process -Name "steam" -ErrorAction SilentlyContinue) {
        Show-PandaMessage "Steam did not finish closing.`n`nUse Steam > Exit or right-click its system-tray icon and choose Exit, then run Panda Pro 8K again. The bridge did not force-close Steam." "Steam is still running" ([System.Windows.MessageBoxImage]::Warning)
        exit 2
    }
}

if (-not $bridgeProcess) {
    try {
        # The executable manifest requests elevation, producing the normal UAC prompt.
        $bridgeProcess = Start-Process -FilePath $bridgePath -ArgumentList "--steam-filter-ready" -WorkingDirectory $packageDirectory -PassThru
    }
    catch {
        Show-PandaMessage "The bridge could not start. If you cancelled the UAC prompt, run the shortcut again and choose Yes.`n`n$($_.Exception.Message)" "Bridge did not start" ([System.Windows.MessageBoxImage]::Error)
        exit 3
    }

    $ready = $false
    $logDirectory = Join-Path $packageDirectory "logs"
    for ($attempt = 0; $attempt -lt 200; $attempt++) {
        Start-Sleep -Milliseconds 100
        $bridgeProcess.Refresh()
        if ($bridgeProcess.HasExited) { break }

        $latestLog = Get-ChildItem -LiteralPath $logDirectory -Filter "panda-applet-*.log" -File -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -ge $launchTime.AddSeconds(-2) } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($latestLog -and (Select-String -LiteralPath $latestLog.FullName -SimpleMatch "Enhanced reader is live" -Quiet)) {
            $ready = $true
            break
        }
    }

    if (-not $ready) {
        Show-PandaMessage "The virtual controller did not become ready. Leave the bridge window open and check the newest file in the logs folder." "Bridge startup problem" ([System.Windows.MessageBoxImage]::Error)
        exit 4
    }
}

if (-not $steamPath) {
    Show-PandaMessage "The bridge is running, but Steam could not be found. Start Steam manually only after setting SDL_GAMECONTROLLER_IGNORE_DEVICES to 0x3537/0x10C5,0x3537/0x10C6." "Steam was not found" ([System.Windows.MessageBoxImage]::Warning)
    exit 5
}

$steamStart = [System.Diagnostics.ProcessStartInfo]::new()
$steamStart.FileName = $steamPath
$steamStart.WorkingDirectory = Split-Path -Parent $steamPath
$steamStart.UseShellExecute = $false
$steamStart.Arguments = "+controller_rate $steamControllerInterval"
$ignoreVariable = "SDL_GAMECONTROLLER_IGNORE_DEVICES"
$gameSirDevices = @("0x3537/0x10C5", "0x3537/0x10C6")
$currentIgnoreList = [Environment]::GetEnvironmentVariable($ignoreVariable, "Process")
if ($currentIgnoreList -and $currentIgnoreList.TrimStart().StartsWith("@")) {
    Show-PandaMessage "$ignoreVariable currently points to a file. Add both GameSir IDs (0x3537/0x10C5 and 0x3537/0x10C6) to that file before launching Steam through Panda Pro 8K." "SDL ignore list needs attention" ([System.Windows.MessageBoxImage]::Warning)
    exit 6
}
$ignoreEntries = @($currentIgnoreList -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($gameSirDevice in $gameSirDevices) {
    if ($ignoreEntries -notcontains $gameSirDevice) { $ignoreEntries += $gameSirDevice }
}
$steamStart.EnvironmentVariables[$ignoreVariable] = $ignoreEntries -join ","
[System.Diagnostics.Process]::Start($steamStart) | Out-Null
