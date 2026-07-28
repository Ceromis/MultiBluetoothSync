using System.Diagnostics;
using Microsoft.Win32;

namespace MultiBluetoothSync;

public static class AdminHelper
{
    private const string AppName = "MultiBluetoothSync";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsRunningAsAdmin()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public static bool SetAutoStart(bool enable)
    {
        if (!IsRunningAsAdmin())
        {
            // Re-launch as admin to set auto-start
            return RequestAdminSetAutoStart(enable);
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RunKeyPath, true);
            if (key == null) return false;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue(AppName, $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            // Fallback: check HKCU
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }
    }

    private static bool RequestAdminSetAutoStart(bool enable)
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = enable ? "--set-autostart-on" : "--set-autostart-off",
                Verb = "runas",
                UseShellExecute = true
            };
            var process = Process.Start(startInfo);
            process?.WaitForExit(10000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handle command-line arguments for auto-start setup (called when launched as admin)
    /// </summary>
    public static int HandleAutoStartArgs(string[] args)
    {
        if (args.Length == 0) return -1;

        if (args[0] == "--set-autostart-on")
        {
            return SetAutoStartDirect(true) ? 0 : 1;
        }
        else if (args[0] == "--set-autostart-off")
        {
            return SetAutoStartDirect(false) ? 0 : 1;
        }
        return -1;
    }

    private static bool SetAutoStartDirect(bool enable)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RunKeyPath, true);
            if (key == null) return false;

            if (enable)
            {
                // Get the original exe path (not the elevated one)
                var exePath = Environment.GetCommandLineArgs().Length > 0
                    ? Environment.ProcessPath ?? ""
                    : "";
                key.SetValue(AppName, $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
            return true;
        }
        catch { return false; }
    }
}
