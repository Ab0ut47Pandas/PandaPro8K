using System.Diagnostics;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaPro8K;

internal sealed partial class HidHideManager
{
    private const string CliPath = @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe";
    private static readonly string[] GameSirPrefixes =
    {
        @"HID\VID_3537&PID_10C5&", // wired
        @"HID\VID_3537&PID_10C6&", // 2.4 GHz dongle
    };

    private readonly Action<string> _log;
    private readonly List<string> _addedApplications = new();
    private readonly List<string> _addedDevices = new();
    private string? _appPath;
    private string? _helperPath;
    private bool _wasCloakingEnabled;

    public HidHideManager(Action<string> log)
    {
        _log = log;
    }

    public bool IsActive { get; private set; }
    public string Status { get; private set; } = "Not configured";

    public void Enable(string appPath, string helperPath)
    {
        if (!File.Exists(CliPath))
        {
            Status = "HidHide is not installed";
            return;
        }

        _appPath = appPath;
        _helperPath = helperPath;
        _wasCloakingEnabled = Run("--cloak-state").Contains("--cloak-on", StringComparison.Ordinal);
        RefreshApplications();
        HashSet<string> hiddenDevices = ReadQuotedList("--dev-list", DeviceLine());
        (List<string> gameSirDevices, List<string> virtualDevices) = FindPresentTargets();
        if (gameSirDevices.Count == 0)
        {
            Status = "GameSir XInput interface not found";
            return;
        }
        if (virtualDevices.Count == 0)
        {
            Status = "Virtual DualSense Edge interface not found";
            return;
        }

        foreach (string device in gameSirDevices.Concat(virtualDevices))
        {
            if (hiddenDevices.Contains(device)) continue;
            Run("--dev-hide", device);
            _addedDevices.Add(device);
        }
        Run("--cloak-on");
        IsActive = Run("--cloak-state").Contains("--cloak-on", StringComparison.Ordinal);
        Status = IsActive
            ? $"GameSir + virtual Edge isolated ({gameSirDevices.Count}+{virtualDevices.Count})"
            : "Cloaking did not activate";
        _log($"Input isolation: {Status}.");
    }

    public void RefreshApplications()
    {
        if (!File.Exists(CliPath) || string.IsNullOrWhiteSpace(_appPath) || string.IsNullOrWhiteSpace(_helperPath))
            return;

        HashSet<string> registeredApps = ReadQuotedList("--app-list", AppLine());
        foreach (string application in FindAllowedApplications(_appPath, _helperPath))
        {
            if (registeredApps.Contains(application)) continue;
            Run("--app-reg", application);
            if (!_addedApplications.Contains(application, StringComparer.OrdinalIgnoreCase))
                _addedApplications.Add(application);
        }
    }

    public void RefreshIsolation()
    {
        if (!File.Exists(CliPath)) return;

        RefreshApplications();
        HashSet<string> hiddenDevices = ReadQuotedList("--dev-list", DeviceLine());
        (List<string> gameSirDevices, List<string> virtualDevices) = FindPresentTargets();
        foreach (string device in gameSirDevices.Concat(virtualDevices))
        {
            if (hiddenDevices.Contains(device)) continue;
            Run("--dev-hide", device);
            if (!_addedDevices.Contains(device, StringComparer.OrdinalIgnoreCase))
                _addedDevices.Add(device);
        }
        Run("--cloak-on");
        IsActive = Run("--cloak-state").Contains("--cloak-on", StringComparison.Ordinal);
        Status = IsActive
            ? $"GameSir + virtual Edge isolated ({gameSirDevices.Count}+{virtualDevices.Count})"
            : "Cloaking did not activate";
        _log($"Input isolation refreshed: {Status}.");
    }

    public void Restore()
    {
        if (!File.Exists(CliPath)) return;
        try
        {
            foreach (string device in _addedDevices)
                Run("--dev-unhide", device);
            foreach (string application in _addedApplications)
                Run("--app-unreg", application);
            if (!_wasCloakingEnabled) Run("--cloak-off");
        }
        catch (Exception ex)
        {
            _log($"Input isolation cleanup failed: {ex.Message}");
        }
        finally
        {
            IsActive = false;
            Status = "Stopped";
        }
    }

    private (List<string> GameSir, List<string> Virtual) FindPresentTargets()
    {
        string json = Run("--dev-gaming");
        using JsonDocument document = JsonDocument.Parse(json);
        var gameSirDevices = new List<string>();
        var virtualDevices = new List<string>();
        foreach (JsonElement group in document.RootElement.EnumerateArray())
        {
            if (!group.TryGetProperty("devices", out JsonElement entries)) continue;
            string groupName = ReadString(group, "friendlyName");
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("present", out JsonElement present) || !present.GetBoolean()) continue;
                if (!entry.TryGetProperty("deviceInstancePath", out JsonElement pathElement)) continue;
                string? path = pathElement.GetString();
                if (path is not null && GameSirPrefixes.Any(
                        prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    gameSirDevices.Add(path!);
                    continue;
                }

                string identity = string.Join(' ',
                    groupName,
                    ReadString(entry, "vendor"),
                    ReadString(entry, "product"),
                    ReadString(entry, "description"));
                bool isPandaVirtualEdge =
                    ReadString(entry, "serialNumber").Equals("HM-CTL-0000", StringComparison.OrdinalIgnoreCase) &&
                    identity.Contains("DualSense Edge", StringComparison.OrdinalIgnoreCase);
                if (isPandaVirtualEdge && !string.IsNullOrWhiteSpace(path)) virtualDevices.Add(path);
            }
        }
        return (
            gameSirDevices.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            virtualDevices.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? FindSteamPath()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string? configured = key?.GetValue("SteamExe")?.ToString();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string normalized = configured.Replace('/', Path.DirectorySeparatorChar);
            if (File.Exists(normalized)) return Path.GetFullPath(normalized);
        }

        string fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steam.exe");
        return File.Exists(fallback) ? fallback : null;
    }

    private static IEnumerable<string> FindSteamApplications()
    {
        string? steamPath = FindSteamPath();
        if (steamPath is null) yield break;

        yield return steamPath;
        string steamRoot = Path.GetDirectoryName(steamPath)!;
        string webHelper = Path.Combine(steamRoot, "bin", "cef", "cef.win64", "steamwebhelper.exe");
        if (File.Exists(webHelper)) yield return webHelper;
        string steamService = Path.Combine(steamRoot, "bin", "steamservice.exe");
        if (File.Exists(steamService)) yield return steamService;
    }

    private static IEnumerable<string> FindAllowedApplications(string appPath, string helperPath)
    {
        yield return appPath;
        yield return helperPath;

        foreach (string steamApplication in FindSteamApplications())
            yield return steamApplication;

        string connectPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "GameSir Connect", "GameSir Connect.exe");
        if (File.Exists(connectPath)) yield return connectPath;
    }

    private HashSet<string> ReadQuotedList(string command, Regex linePattern)
    {
        string output = Run(command);
        return linePattern.Matches(output)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private string Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(CliPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start HidHideCLI.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
        return output;
    }

    [GeneratedRegex("--app-reg \\\"([^\\\"]+)\\\"")]
    private static partial Regex AppLine();

    [GeneratedRegex("--dev-hide \\\"([^\\\"]+)\\\"")]
    private static partial Regex DeviceLine();

}
