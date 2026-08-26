using System.Text.Json;

namespace PandaPro8K;

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public int VirtualOutputRateHz { get; set; } = 1000;

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PandaPro8K");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            if (settings is null || !IsValidRate(settings.VirtualOutputRateHz)) return new AppSettings();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        if (!IsValidRate(VirtualOutputRateHz))
            throw new InvalidOperationException("The virtual output rate is outside the supported range.");

        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static bool IsValidRate(int rateHz) => rateHz == 0 || rateHz is >= 125 and <= 8000;
}
