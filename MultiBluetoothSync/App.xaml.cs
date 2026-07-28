using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MultiBluetoothSync;

public partial class App : Application
{
    private SystemTray? _tray;
    private static Mutex? _mutex;
    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MultiBluetoothSync", "crash.log");

    private static void Log(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("=== OnStartup begin ===");

        // Single instance check
        try
        {
            _mutex = new Mutex(true, "MultiBluetoothSync_SingleInstance", out bool isNew);
            if (!isNew)
            {
                Log("Another instance detected, shutting down");
                MessageBox.Show("MultiBluetoothSync 已在运行中，请查看系统托盘。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }
            Log("Mutex acquired, single instance OK");
        }
        catch (Exception ex)
        {
            Log($"Mutex error: {ex.Message}");
        }

        // Global error handler
        DispatcherUnhandledException += (s, ex) =>
        {
            Log($"UNHANDLED: {ex.Exception}");
            MessageBox.Show($"发生错误：{ex.Exception.Message}\n\n日志：{LogPath}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            Log($"FATAL: {ex.ExceptionObject}");
        };

        base.OnStartup(e);
        Log("base.OnStartup done");

        // Handle auto-start admin args
        try
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            Log($"Args: [{string.Join(", ", args)}]");
            int exitCode = AdminHelper.HandleAutoStartArgs(args);
            Log($"HandleAutoStartArgs returned: {exitCode}");
            if (exitCode >= 0)
            {
                Shutdown(exitCode);
                return;
            }
        }
        catch (Exception ex)
        {
            Log($"HandleAutoStartArgs error: {ex.Message}");
        }

        try
        {
            _tray = new SystemTray();
            Log("SystemTray created OK");
        }
        catch (Exception ex)
        {
            Log($"SystemTray error: {ex.Message}");
        }

        Log("=== OnStartup end ===");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("=== OnExit ===");
        _tray?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
