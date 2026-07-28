using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MultiBluetoothSync;

public partial class MainWindow : Window
{
    private readonly DeviceManager _deviceManager = new();
    private readonly AudioRouter _audioRouter = new();
    private AppConfig _config;
    private bool _isRouting;

    public MainWindow()
    {
        InitializeComponent();
        _config = AppConfig.Load();

        TxtInstructions.Text = "1. 安装 VB-Audio Virtual Cable 并重启电脑\n" +
            "2. 在 Windows 声音设置中将播放设备设为 \"CABLE Input\"\n" +
            "3. 在下方选择左耳和右耳蓝牙设备\n" +
            "4. 点击 \"开始路由\" 按钮";

        if (Environment.GetCommandLineArgs().Contains("--minimized"))
        {
            Loaded += (s, e) => { Hide(); };
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        CheckVBAudio();
        LoadDevices();
        LoadSettings();

        _audioRouter.StatusChanged += status =>
        {
            Dispatcher.BeginInvoke(() => TxtStatus.Text = status);
        };

        // System volume is monitored via COM notification in AudioRouter
    }

    private void CheckVBAudio()
    {
        if (_deviceManager.IsVBInstalled())
        {
            VbStatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(34, 197, 94));
            VbStatusText.Text = "VB-Audio Virtual Cable 已安装";
            BtnInstallVB.Visibility = Visibility.Collapsed;
        }
        else
        {
            VbStatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(239, 68, 68));
            VbStatusText.Text = "未检测到 VB-Audio Virtual Cable（需要虚拟音频设备）";
            BtnInstallVB.Visibility = Visibility.Visible;
        }
    }

    private void LoadDevices()
    {
        var devices = _deviceManager.GetOutputDevices();

        CmbLeftDevice.Items.Clear();
        CmbRightDevice.Items.Clear();

        CmbLeftDevice.Items.Add(new ComboBoxItem { Content = "-- 请选择左耳设备 --", Tag = "" });
        CmbRightDevice.Items.Add(new ComboBoxItem { Content = "-- 请选择右耳设备 --", Tag = "" });

        foreach (var d in devices)
        {
            var suffix = d.IsBluetooth ? " [蓝牙]" : "";
            CmbLeftDevice.Items.Add(new ComboBoxItem { Content = d.Name + suffix, Tag = d.Id });
            CmbRightDevice.Items.Add(new ComboBoxItem { Content = d.Name + suffix, Tag = d.Id });
        }

        CmbLeftDevice.SelectedIndex = 0;
        CmbRightDevice.SelectedIndex = 0;

        RestoreComboSelection(CmbLeftDevice, _config.LeftDeviceId);
        RestoreComboSelection(CmbRightDevice, _config.RightDeviceId);
    }

    private static void RestoreComboSelection(ComboBox combo, string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return;
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if ((combo.Items[i] as ComboBoxItem)?.Tag?.ToString() == deviceId)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private void LoadSettings()
    {
        SliderLeftVolume.Value = _config.LeftVolume;
        SliderRightVolume.Value = _config.RightVolume;
        SliderSyncOffset.Value = _config.SyncOffsetMs;
        ChkAutoStart.IsChecked = AdminHelper.IsAutoStartEnabled();
    }

    private string? GetSelectedDeviceId(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
    }

    private void BtnRoute_Click(object sender, RoutedEventArgs e)
    {
        if (_isRouting)
            StopRouting();
        else
            StartRouting();
    }

    private void StartRouting()
    {
        var leftId = GetSelectedDeviceId(CmbLeftDevice);
        var rightId = GetSelectedDeviceId(CmbRightDevice);

        if (string.IsNullOrEmpty(leftId) || string.IsNullOrEmpty(rightId))
        {
            MessageBox.Show("请先选择左耳和右耳蓝牙设备！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (leftId == rightId)
        {
            MessageBox.Show("左耳和右耳不能选择同一个设备！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            float leftVol = (float)SliderLeftVolume.Value;
            float rightVol = (float)SliderRightVolume.Value;

            _audioRouter.LevelUpdated += OnLevelUpdated;
            _audioRouter.Start(leftId, rightId, leftVol, rightVol, _config.BufferMs);
            _audioRouter.SetSyncOffset(_config.SyncOffsetMs);

            _isRouting = true;
            BtnRoute.Content = "停止路由";
            CmbLeftDevice.IsEnabled = false;
            CmbRightDevice.IsEnabled = false;

            _config.LeftDeviceId = leftId;
            _config.RightDeviceId = rightId;
            _config.LeftVolume = leftVol;
            _config.RightVolume = rightVol;
            _config.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"启动路由失败：{ex.Message}\n\n请确认：\n1. VB-Audio Virtual Cable 已安装\n2. 播放设备已设为 CABLE Input\n3. 蓝牙设备已连接",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopRouting()
    {
        _audioRouter.LevelUpdated -= OnLevelUpdated;
        _audioRouter.Stop();

        _isRouting = false;
        BtnRoute.Content = "开始路由";
        CmbLeftDevice.IsEnabled = true;
        CmbRightDevice.IsEnabled = true;

        LevelLeft.Value = 0;
        LevelRight.Value = 0;
    }

    private void OnLevelUpdated(float left, float right)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LevelLeft.Value = Math.Min(left * 10, 1);
            LevelRight.Value = Math.Min(right * 10, 1);
        });
    }

    private void CmbLeftDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbLeftDevice.SelectedItem is ComboBoxItem item && item.Tag is string id && !string.IsNullOrEmpty(id))
        {
            _config.LeftDeviceId = id;
            _config.Save();
        }
    }

    private void CmbRightDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbRightDevice.SelectedItem is ComboBoxItem item && item.Tag is string id && !string.IsNullOrEmpty(id))
        {
            _config.RightDeviceId = id;
            _config.Save();
        }
    }

    private void SliderLeftVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtLeftVolume == null) return;
        TxtLeftVolume.Text = $"{(int)(SliderLeftVolume.Value * 100)}%";
        if (_config == null) return;
        _config.LeftVolume = (float)SliderLeftVolume.Value;
        if (_isRouting) _audioRouter.UpdateVolumes(_config.LeftVolume, _config.RightVolume);
    }

    private void SliderRightVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtRightVolume == null) return;
        TxtRightVolume.Text = $"{(int)(SliderRightVolume.Value * 100)}%";
        if (_config == null) return;
        _config.RightVolume = (float)SliderRightVolume.Value;
        if (_isRouting) _audioRouter.UpdateVolumes(_config.LeftVolume, _config.RightVolume);
    }

    private void SliderSyncOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSyncOffset == null) return;
        int val = (int)SliderSyncOffset.Value;
        TxtSyncOffset.Text = val > 0 ? $"+{val}ms" : $"{val}ms";
        if (_config == null) return;
        _config.SyncOffsetMs = val;
        _config.Save();
        if (_isRouting) _audioRouter.SetSyncOffset(val);
    }

    private void ChkAutoStart_Changed(object sender, RoutedEventArgs e)
    {
        bool enable = ChkAutoStart.IsChecked == true;
        bool success = AdminHelper.SetAutoStart(enable);

        if (!success)
        {
            ChkAutoStart.IsChecked = !enable;
            if (enable)
            {
                MessageBox.Show("设置开机自启动失败。\n需要管理员权限，请允许 UAC 提示。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadDevices();
        CheckVBAudio();
    }

    private void BtnInstallVB_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "将打开浏览器下载 VB-Audio Virtual Cable。\n安装后请重启电脑，然后重新打开本软件。\n\n是否继续？",
            "下载 VB-Audio", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://vb-audio.com/Cable/",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch { }
        e.Handled = true;
    }
}
