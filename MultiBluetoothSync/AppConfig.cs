using System.IO;
using System.Text.Json;

namespace MultiBluetoothSync;

public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MultiBluetoothSync");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public string? LeftDeviceId { get; set; }
    public string? RightDeviceId { get; set; }
    public float LeftVolume { get; set; } = 1.0f;
    public float RightVolume { get; set; } = 1.0f;
    public bool AutoStart { get; set; }
    public int BufferMs { get; set; } = 200;
    public float SyncOffsetMs { get; set; }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }
}
