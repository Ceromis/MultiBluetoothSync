using NAudio.CoreAudioApi;

namespace MultiBluetoothSync;

public class AudioDeviceInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsBluetooth { get; set; }

    public override string ToString() => Name;
}

public class DeviceManager
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public List<AudioDeviceInfo> GetOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(new AudioDeviceInfo
            {
                Id = device.ID,
                Name = device.FriendlyName,
                IsBluetooth = IsBluetoothDevice(device)
            });
        }
        return devices;
    }

    public List<AudioDeviceInfo> GetBluetoothOutputDevices()
    {
        return GetOutputDevices().Where(d => d.IsBluetooth).ToList();
    }

    public MMDevice? GetDeviceById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try { return _enumerator.GetDevice(id); }
        catch { return null; }
    }

    public bool IsVBInstalled()
    {
        try
        {
            foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (device.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase) ||
                    device.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                if (device.FriendlyName.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase) ||
                    device.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static bool IsBluetoothDevice(MMDevice device)
    {
        var name = device.FriendlyName.ToLowerInvariant();
        string[] btKeywords = ["bluetooth", "bt", "airpods", "galaxy buds", "earbuds",
            "headphone", "headset", "wf-", "wh-", "freebuds", "earphone"];
        return btKeywords.Any(k => name.Contains(k));
    }
}
