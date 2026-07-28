using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace MultiBluetoothSync;

public class SystemTray : IDisposable
{
    private NotifyIcon? _notifyIcon;

    public SystemTray()
    {
        CreateNotifyIcon();
    }

    private void CreateNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "MultiBluetoothSync",
            Visible = true
        };

        _notifyIcon.Icon = CreateTrayIcon();

        var contextMenu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("显示主窗口");
        showItem.Click += (s, e) => ShowMainWindow();
        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => ExitApp();
        contextMenu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    private static Icon CreateTrayIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
        g.FillEllipse(brush, 1, 1, 14, 14);
        using var font = new Font(new FontFamily("Arial"), 8, System.Drawing.FontStyle.Bold);
        g.DrawString("B", font, Brushes.White, 3, 1);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void ShowMainWindow()
    {
        try
        {
            var win = Application.Current.MainWindow;
            if (win == null) return;
            win.Show();
            win.WindowState = WindowState.Normal;
            win.Activate();
            win.Topmost = true;
            win.Topmost = false;
        }
        catch { }
    }

    private void ExitApp()
    {
        var result = System.Windows.MessageBox.Show(
            "确定要退出 MultiBluetoothSync 吗？\n退出后音频路由将停止。",
            "退出确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _notifyIcon!.Visible = false;
            Application.Current.Shutdown();
        }
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
