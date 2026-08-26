$ErrorActionPreference = "Stop"

Add-Type -AssemblyName PresentationFramework

function Show-InstallMessage {
    param(
        [string]$Message,
        [System.Windows.MessageBoxImage]$Icon = [System.Windows.MessageBoxImage]::Information
    )

    [System.Windows.MessageBox]::Show(
        $Message,
        "Panda Pro 8K",
        [System.Windows.MessageBoxButton]::OK,
        $Icon
    ) | Out-Null
}

try {
    $packageDirectory = Split-Path -Parent $PSCommandPath
    $requiredFiles = @("PandaPro8K.exe", "panda_enhanced_stream.exe", "Start-PandaPro8K.ps1")
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory $file))) {
            throw "$file is missing. Extract the complete release ZIP before installing."
        }
    }

    $installDirectory = Join-Path $env:LOCALAPPDATA "Programs\Panda Pro 8K"
    New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

    Get-ChildItem -LiteralPath $packageDirectory -File |
        Where-Object { $_.Name -ne "Install.ps1" } |
        Copy-Item -Destination $installDirectory -Force

    $desktop = [Environment]::GetFolderPath("DesktopDirectory")
    $shortcutPath = Join-Path $desktop "Panda Pro 8K.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = "powershell.exe"
    $shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $installDirectory 'Start-PandaPro8K.ps1')`""
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$(Join-Path $installDirectory 'PandaPro8K.exe'),0"
    $shortcut.Description = "Start Panda Pro 8K and prepare Steam Input"
    $shortcut.Save()

    Show-InstallMessage "Panda Pro 8K was installed for this Windows account.`n`nUse the Panda Pro 8K shortcut on the Desktop to start it."
}
catch {
    Show-InstallMessage $_.Exception.Message ([System.Windows.MessageBoxImage]::Error)
    exit 1
}
