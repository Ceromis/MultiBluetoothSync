# MultiBluetoothSync - 多蓝牙耳机协同工具

## 功能说明

将两个无法配对成双通道的蓝牙耳机组合为一副双通道立体声耳机。

### 核心功能
- **虚拟音频设备集成** - 使用 VB-Audio Virtual Cable 作为虚拟音频设备
- **左右耳自由分配** - 选择任意已连接的蓝牙设备作为左耳或右耳
- **独立音量控制** - 左耳和右耳可分别调节音量
- **实时音量指示** - 显示左右声道的实时音频电平
- **开机自启动** - 提供管理员权限的开机自启动设置
- **系统托盘运行** - 关闭窗口后最小化到托盘，托盘右键退出才真正关闭

## 系统要求

- Windows 10/11
- .NET 8.0 Desktop Runtime（如未安装，系统会提示下载）
- VB-Audio Virtual Cable（软件内可检测并引导安装）

## 安装步骤

### 1. 安装 VB-Audio Virtual Cable

1. 运行 MultiBluetoothSync.exe
2. 如果提示"未检测到 VB-Audio Virtual Cable"，点击"下载安装 VB-Audio Virtual Cable"
3. 从官网下载并安装 VB-Audio Virtual Cable
4. **重启电脑**（必须）

### 2. 配置 Windows 音频

1. 右键点击任务栏音量图标 → 声音设置
2. 将"播放设备"设置为 **CABLE Input (VB-Audio Virtual Cable)**
3. 这样所有系统音频会先输出到虚拟设备

### 3. 使用软件

1. 运行 MultiBluetoothSync.exe
2. 在"左耳设备"下拉框选择一个蓝牙耳机
3. 在"右耳设备"下拉框选择另一个蓝牙耳机
4. 调节左右耳音量（可选）
5. 点击"开始路由"

## 文件说明

```
MultiBluetoothSync/
├── App.xaml / App.xaml.cs         # 应用入口
├── MainWindow.xaml / .xaml.cs     # 主窗口 UI
├── AudioRouter.cs                 # 音频路由核心
├── DeviceManager.cs               # 音频设备管理
├── RingBuffer.cs                  # 环形缓冲区
├── SimpleVolumeProvider.cs        # 音量控制
├── SystemTray.cs                  # 系统托盘
├── AdminHelper.cs                 # 管理员权限辅助
├── AppConfig.cs                   # 配置管理
└── MultiBluetoothSync.csproj      # 项目文件
```

## 技术架构

```
[系统音频] → [CABLE Input 虚拟设备] → [WASAPI Loopback 捕获]
                                            ↓
                                      [环形缓冲区]
                                     ↙         ↘
                            [左耳输出]      [右耳输出]
                            (WASAPI)        (WASAPI)
                               ↓               ↓
                          [蓝牙耳机L]     [蓝牙耳机R]
```

## 构建说明

需要 .NET 8.0 SDK：

```bash
dotnet restore
dotnet build
dotnet publish -c Release -r win-x64 --self-contained false
```

## 注意事项

1. 两个蓝牙耳机需要分别与电脑配对连接
2. 部分蓝牙耳机可能有延迟差异，属于正常现象
3. 关闭窗口会最小化到系统托盘，不会停止音频路由
4. 要完全退出，需在系统托盘图标上右键选择"退出"
