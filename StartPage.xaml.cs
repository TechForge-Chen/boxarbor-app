using BluetoothMauiApp;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace BluetoothMauiApp;

/// <summary>
/// HOME 页（Figma 67:1214）。同时是整个 App 的 BLE 引擎持有者：
/// 设备列表、扫描、连接、收发都在这里以静态成员提供，DataPage 的扫描入口复用。
/// 协议与分片逻辑本轮未改动。
/// </summary>
public partial class StartPage : ContentPage
{
    private int _selectedSceneId = 1;
    private readonly Color GreenMain = Color.FromHex("#16c95c");

    // 本页的 UI 更新回调（静态 BLE 回调需要回到实例上刷 lblConnState）
    private static StartPage? _current;

    // ============================== BLE 引擎（静态） ==============================

    static readonly Guid SppServiceUuid = Guid.Parse("0000ffe0-0000-1000-8000-00805f9b34fb");
    static readonly Guid SppTxRxUuid = Guid.Parse("0000ffe1-0000-1000-8000-00805f9b34fb");
    static readonly Guid NusServiceUuid = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
    static readonly Guid NusTxUuid = Guid.Parse("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");
    static readonly Guid NusRxUuid = Guid.Parse("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");

    public static IDevice? _connectedDevice;
    public static ICharacteristic? _writeCharacteristic;
    public static ICharacteristic? _notifyCharacteristic;

    /// <summary>全局存储完整接收数据，最大容纳 550 字节完整帧</summary>
    public static byte[] datas = Array.Empty<byte>();
    /// <summary>分片接收缓冲区，用于拼接多包 BLE 数据</summary>
    private static readonly List<byte> _recvBuffer = new List<byte>(550);
    private const int MaxFrameLength = 550;
    /// <summary>BLE 单包最大有效载荷：MTU247 - 3 协议头 = 244 字节</summary>
    private const int SinglePackageMaxSize = 244;
    public const int ScanTimeoutMs = 10000;

    public static bool 数据接收标识 = false;
    public static Action<byte[]>? OnDataReceivedAction { get; set; }

    /// <summary>扫描结果集合，DataPage 直接绑定</summary>
    public static ObservableCollection<DeviceItem> Devices { get; } = new();

    /// <summary>设备列表或连接状态变化，DataPage 订阅后刷新</summary>
    public static event Action? DevicesChanged;

    private static IBluetoothLE? _bluetooth;
    private static IAdapter? _adapter;
    private static bool _engineReady;
    private static readonly object _engineLock = new();

    /// <summary>惰性初始化 BLE 适配器与事件订阅（DataPage 也可能先于本页构造）。</summary>
    public static void EnsureEngine()
    {
        if (_engineReady)
            return;
        lock (_engineLock)
        {
            if (_engineReady)
                return;
            _bluetooth = CrossBluetoothLE.Current;
            _adapter = CrossBluetoothLE.Current.Adapter;
            _adapter.ScanTimeout = ScanTimeoutMs;
            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            _adapter.DeviceConnected += OnDeviceConnected;
            _adapter.DeviceDisconnected += OnDeviceDisconnected;
            _engineReady = true;
        }
    }

    /// <summary>在当前可见页面上弹提示（静态回调没有 Page 上下文）。带 cancel 时返回是否点了 ok。</summary>
    private static async Task<bool> AlertAsync(string title, string message, string ok = "OK", string? cancel = null)
    {
        try
        {
            if (Shell.Current?.CurrentPage is ContentPage page)
                return await page.DisplayAlert(title, message, ok, cancel);
        }
        catch
        {
            // 页面尚未就绪时静默忽略
        }
        return false;
    }

    /// <summary>会话内只弹一次系统授权框；被拒后改为提示原因并引导去系统设置。</summary>
    private static bool _btPermissionRequested;

    private static async Task<bool> EnsureBluetoothPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
            if (status == PermissionStatus.Granted)
                return true;

            if (!_btPermissionRequested)
            {
                _btPermissionRequested = true;
                status = await Permissions.RequestAsync<Permissions.Bluetooth>();
                if (status == PermissionStatus.Granted)
                    return true;
            }

            // 拒绝过：不再反复弹系统授权框，只提示原因并引导到设置页
            bool openSettings = await AlertAsync(
                "Bluetooth permission needed",
                "Scanning needs Bluetooth permission. Grant it in system settings, then tap Scan again.",
                "Open Settings", "Not now");
            if (openSettings)
                AppInfo.ShowSettingsUI();
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>开始扫描。由 DataPage 的 "Scan with Bluetooth" 调用。</summary>
    public static async Task StartScanAsync()
    {
        EnsureEngine();
        try
        {
            if (!_bluetooth!.IsAvailable)
            {
                await AlertAsync("Error", "Device does not support Bluetooth BLE");
                return;
            }
            if (_bluetooth.State != BluetoothState.On)
            {
                await AlertAsync("Notice", "Please turn on Bluetooth and retry");
                return;
            }
            if (!await EnsureBluetoothPermissionAsync())
                return;
            Devices.Clear();
            DevicesChanged?.Invoke();
            await _adapter!.StartScanningForDevicesAsync((Guid[])null);
        }
        catch (PermissionException)
        {
            await EnsureBluetoothPermissionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"扫描启动异常：{ex.Message}");
            await AlertAsync("Bluetooth", "Scan could not start. Check Bluetooth permission and retry.");
        }
    }

    public static async Task StopScanAsync()
    {
        EnsureEngine();
        try
        {
            if (_adapter != null && _adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();
        }
        catch
        {
            // 停止扫描失败不影响主流程
        }
    }

    /// <summary>连接指定设备；成功返回 true。超时由调用方控制。</summary>
    public static async Task<bool> ConnectAsync(IDevice device)
    {
        EnsureEngine();
        try
        {
            if (!await EnsureBluetoothPermissionAsync())
                return false;
            if (_connectedDevice != null)
                await _adapter!.DisconnectDeviceAsync(_connectedDevice);
            await _adapter!.ConnectToDeviceAsync(device);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>断开当前设备并清空收发状态。</summary>
    public static async Task DisconnectAsync()
    {
        EnsureEngine();
        try
        {
            if (_notifyCharacteristic != null)
            {
                if (_notifyCharacteristic.CanUpdate)
                    await _notifyCharacteristic.StopUpdatesAsync();
                _notifyCharacteristic.ValueUpdated -= OnCharacteristicValueUpdated;
            }
            await _adapter!.StopScanningForDevicesAsync();
            if (_connectedDevice != null)
                await _adapter.DisconnectDeviceAsync(_connectedDevice);

            _connectedDevice = null;
            _writeCharacteristic = null;
            _notifyCharacteristic = null;
            _recvBuffer.Clear();
            datas = Array.Empty<byte>();
            MauiProgram.蓝牙连接 = false;

            foreach (var d in Devices)
                d.ConnectButtonText = "Connect";
            DevicesChanged?.Invoke();
            _current?.UpdateStatus();
        }
        catch
        {
            // 断开异常静默处理，避免打断 UI
        }
    }

    // ============================== 页面 ==============================

    public StartPage()
    {
        InitializeComponent();

        EnsureEngine();
        _current = this;

        switchAll.Toggled += OnAllDevicesToggled;

        // 新手引导：View guide 入口 + 第1步高亮底部 Devices
        Guide1.Target = TabBar.DevicesTarget;
        Guide1.ActionTapped += OnGuideAction;
        Guide1.SkipTapped += OnGuideSkip;

        UpdateSceneUI();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _current = this;
        // 先减后加，避免页面被重复构造时重复订阅
        DevicesChanged -= OnDevicesChanged;
        DevicesChanged += OnDevicesChanged;
        UpdateStatus();
    }

    protected override void OnDisappearing()
    {
        DevicesChanged -= OnDevicesChanged;
        base.OnDisappearing();
    }

    private void OnDevicesChanged()
    {
        // 连接状态可能随扫描/连接变化，统一切回主线程刷新一次
        MainThread.BeginInvokeOnMainThread(UpdateStatus);
    }

    // ------------------------- 扫描结果 -------------------------

    private static void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        if (e.Device == null || (string.IsNullOrEmpty(e.Device.Name) && e.Device.Rssi == 0))
            return;

        string deviceName = e.Device.Name;
        if (string.IsNullOrEmpty(deviceName) || !deviceName.StartsWith("BoxArborMG", StringComparison.Ordinal))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = Devices.FirstOrDefault(d => d.Id == e.Device.Id);
            if (existing == null)
                Devices.Add(new DeviceItem(e.Device));
            else
                existing.Rssi = e.Device.Rssi;
            DevicesChanged?.Invoke();
        });
    }

    private static void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _connectedDevice = e.Device;
            _current?.UpdateStatus();
            _writeCharacteristic = null;
            _notifyCharacteristic = null;
            // 连接成功清空接收缓存
            _recvBuffer.Clear();

            try
            {
                // MTU协商容错处理，硬件上限251，无法直接500，依靠软件拼接实现500字节接收
                if (_connectedDevice != null)
                {
                    try
                    {
                        await _connectedDevice.RequestMtuAsync(247);
                    }
                    catch (Exception mtuErr)
                    {
                        await AlertAsync("MTU Notice", $"Device does not support large packets, will auto-fragment: {mtuErr.Message}");
                    }
                }

                var services = await _connectedDevice.GetServicesAsync();
                var sppService = services.FirstOrDefault(s => s.Id == SppServiceUuid);
                if (sppService != null)
                {
                    var sppCharList = await sppService.GetCharacteristicsAsync();
                    var sppTxRxChar = sppCharList.FirstOrDefault(c => c.Id == SppTxRxUuid);
                    if (sppTxRxChar != null)
                    {
                        _writeCharacteristic = sppTxRxChar;
                        _notifyCharacteristic = sppTxRxChar;
                    }
                }
                if (_writeCharacteristic == null || _notifyCharacteristic == null)
                {
                    var nusService = services.FirstOrDefault(s => s.Id == NusServiceUuid);
                    if (nusService != null)
                    {
                        var nusChars = await nusService.GetCharacteristicsAsync();
                        _notifyCharacteristic = nusChars.FirstOrDefault(c => c.Id == NusTxUuid);
                        _writeCharacteristic = nusChars.FirstOrDefault(c => c.Id == NusRxUuid);
                    }
                }
                if (_writeCharacteristic == null || _notifyCharacteristic == null)
                {
                    foreach (var svc in services)
                    {
                        var chars = await svc.GetCharacteristicsAsync();
                        if (_writeCharacteristic == null)
                            _writeCharacteristic = chars.FirstOrDefault(c => c.CanWrite);
                        if (_notifyCharacteristic == null)
                            _notifyCharacteristic = chars.FirstOrDefault(c => c.CanUpdate);
                        if (_writeCharacteristic != null && _notifyCharacteristic != null)
                            break;
                    }
                }
                if (_notifyCharacteristic != null)
                {
                    _notifyCharacteristic.ValueUpdated -= OnCharacteristicValueUpdated;
                    _notifyCharacteristic.ValueUpdated += OnCharacteristicValueUpdated;
                    if (_notifyCharacteristic.CanUpdate)
                    {
                        await _notifyCharacteristic.StartUpdatesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                await AlertAsync("Init Error", $"Characteristic parsing failed: {ex.Message}");
            }

            var found = Devices.FirstOrDefault(d => _connectedDevice != null && d.Id == _connectedDevice.Id);
            if (found != null)
            {
                found.ConnectButtonText = "Connected";
                MauiProgram.蓝牙连接 = true;
            }
            DevicesChanged?.Invoke();
            _current?.UpdateStatus();
        });
    }

    private static void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_connectedDevice != null && e.Device.Id == _connectedDevice.Id)
            {
                _connectedDevice = null;
                _writeCharacteristic = null;
                if (_notifyCharacteristic != null)
                {
                    _notifyCharacteristic.ValueUpdated -= OnCharacteristicValueUpdated;
                    _notifyCharacteristic = null;
                }
                MauiProgram.蓝牙连接 = false;
                // 外设离线清空接收缓存
                _recvBuffer.Clear();
                datas = Array.Empty<byte>();
            }
            _current?.UpdateStatus();
            DevicesChanged?.Invoke();
        });
    }

    /// <summary>BLE分片接收回调，自动拼接，完整帧存入全局datas数组</summary>
    private static void OnCharacteristicValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
    {
        try
        {
            byte[] recvPacket = e.Characteristic.Value;
            // 写入分片缓存，超过最大550字节直接清空防溢出
            if (_recvBuffer.Count + recvPacket.Length > MaxFrameLength)
                _recvBuffer.Clear();
            _recvBuffer.AddRange(recvPacket);

            // 循环查找完整帧（帧头0xAA + 帧尾0xBB）
            while (true)
            {
                int frameStart = _recvBuffer.IndexOf((byte)0xAA);
                if (frameStart < 0) break;

                int frameEnd = _recvBuffer.IndexOf((byte)0xBB, frameStart + 1);
                if (frameEnd < 0) break;

                // 截取完整帧
                int frameLen = frameEnd - frameStart + 1;
                byte[] fullFrame = new byte[frameLen];
                _recvBuffer.CopyTo(frameStart, fullFrame, 0, frameLen);

                // 全局静态datas存储完整550以内数据
                datas = fullFrame;
                数据接收标识 = true;

                // 对外分发完整帧
                OnDataReceivedAction?.Invoke(fullFrame);

                // 移除已解析数据，保留剩余半包
                _recvBuffer.RemoveRange(0, frameEnd + 1);
            }
        }
        catch
        {
            // 异常清空缓存避免脏数据堆积
            _recvBuffer.Clear();
        }
    }

    /// <summary>
    /// 支持最大550字节自动分片发送，适配BLE MTU限制，可靠带应答传输。
    /// 协议未改动。
    /// </summary>
    public static async Task BlueSendData(byte[] data)
    {
        if (_connectedDevice == null)
        {
            Console.WriteLine("发送失败：蓝牙设备未连接");
            return;
        }
        if (_writeCharacteristic == null || !_writeCharacteristic.CanWrite)
        {
            Console.WriteLine("发送失败：写入特征不可写");
            return;
        }
        if (data == null || data.Length == 0)
        {
            Console.WriteLine("发送失败：空数据");
            return;
        }
        if (data.Length > MaxFrameLength)
        {
            Console.WriteLine($"发送失败：数据长度{data.Length}超出上限{MaxFrameLength}");
            return;
        }

        int sendOffset = 0;
        int totalLength = data.Length;

        while (sendOffset < totalLength)
        {
            int currentChunkSize = Math.Min(SinglePackageMaxSize, totalLength - sendOffset);
            byte[] chunk = new byte[currentChunkSize];
            Array.Copy(data, sendOffset, chunk, 0, currentChunkSize);

            try
            {
                await _writeCharacteristic.WriteAsync(chunk);
                sendOffset += currentChunkSize;
                // 短暂延时，适配单片机接收缓冲，防止溢出丢包
                await Task.Delay(4);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"分片发送异常，已发送字节：{sendOffset}，错误：{ex.Message}");
                return;
            }
        }
        Console.WriteLine($"✅ 数据发送完成，总长度：{totalLength} 字节");
    }

    // 兼容旧静态void调用封装（原有代码调用不用改，内部转Task）
    public static async void BlueSendDataVoid(byte[] data)
    {
        await BlueSendData(data);
    }

    // ============================== 连接状态（只读展示） ==============================

    private void UpdateStatus()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (lblConnState == null)
                return;

            if (_connectedDevice == null)
            {
                lblConnState.Text = "Offline";
                lblConnState.TextColor = Color.FromHex("#f2402e");
            }
            else
            {
                lblConnState.Text = "Online";
                lblConnState.TextColor = Color.FromHex("#20914d");
            }
        });
    }

    // ============================== 场景选择 ==============================

    /// <summary>场景发送在途标记，应用中忽略重复点击。</summary>
    private bool _sceneSendInFlight;

    private void UpdateSceneUI()
    {
        ApplySceneStyles();
        if (_sceneSendInFlight)
            return;
        _sceneSendInFlight = true;
        _ = SendSceneStateAsync();
    }

    private async Task SendSceneStateAsync()
    {
        try
        {
            byte[] sendDatas = new byte[5];
            sendDatas[0] = 0xAA;
            sendDatas[1] = 0x04;
            sendDatas[2] = switchAll.IsOn ? (byte)2 : (byte)1;
            sendDatas[3] = (byte)_selectedSceneId;
            sendDatas[4] = 0xBB;
            await BlueSendData(sendDatas);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => DisplayAlert("Failed", $"Operation error: {ex.Message}", "OK"));
        }
        finally
        {
            _sceneSendInFlight = false;
        }
    }

    /// <summary>按设计稿刷新三张场景卡：选中 = 绿描边 + 浅绿底 + 白图标底 + 实心单选圈 + Active scene。</summary>
    private void ApplySceneStyles()
    {
        SetSceneStyle(1, _selectedSceneId == 1);
        SetSceneStyle(2, _selectedSceneId == 2);
        SetSceneStyle(3, _selectedSceneId == 3);
    }

    private void SetSceneStyle(int index, bool selected)
    {
        Color cardBg = selected ? Color.FromHex("#fafbfd") : Colors.White;
        Color cardStroke = selected ? GreenMain : Colors.White;
        Color iconBg = selected ? Colors.White : Color.FromHex("#f0f2ef");
        Color ringStroke = selected ? GreenMain : Color.FromHex("#bcc5be");
        string sub = selected ? "Active scene" : "Standby";

        switch (index)
        {
            case 1:
                frameScene1.BackgroundColor = cardBg;
                frameScene1.Stroke = new SolidColorBrush(cardStroke);
                frameScene1.StrokeThickness = selected ? 1.5 : 1;
                iconBg1.BackgroundColor = iconBg;
                ring1.Stroke = new SolidColorBrush(ringStroke);
                ring1.Fill = new SolidColorBrush(selected ? Colors.White : Colors.Transparent);
                ringCore1.IsVisible = selected;
                sceneSub1.Text = sub;
                break;
            case 2:
                frameScene2.BackgroundColor = cardBg;
                frameScene2.Stroke = new SolidColorBrush(cardStroke);
                frameScene2.StrokeThickness = selected ? 1.5 : 1;
                iconBg2.BackgroundColor = iconBg;
                ring2.Stroke = new SolidColorBrush(ringStroke);
                ring2.Fill = new SolidColorBrush(selected ? Colors.White : Colors.Transparent);
                ringCore2.IsVisible = selected;
                sceneSub2.Text = sub;
                break;
            case 3:
                frameScene3.BackgroundColor = cardBg;
                frameScene3.Stroke = new SolidColorBrush(cardStroke);
                frameScene3.StrokeThickness = selected ? 1.5 : 1;
                iconBg3.BackgroundColor = iconBg;
                ring3.Stroke = new SolidColorBrush(ringStroke);
                ring3.Fill = new SolidColorBrush(selected ? Colors.White : Colors.Transparent);
                ringCore3.IsVisible = selected;
                sceneSub3.Text = sub;
                break;
        }
    }

    // ============================== 交互 ==============================

    private void TapScene1(object sender, TappedEventArgs e) => SelectScene(1);
    private void TapScene2(object sender, TappedEventArgs e) => SelectScene(2);
    private void TapScene3(object sender, TappedEventArgs e) => SelectScene(3);

    private void SelectScene(int id)
    {
        if (_sceneSendInFlight)
            return;
        _selectedSceneId = id;
        UpdateSceneUI();
        TapHaptic();
    }

    /// <summary>受 Settings 的 Haptic feedback 开关控制。</summary>
    internal static void TapHaptic()
    {
        try
        {
            if (Microsoft.Maui.Storage.Preferences.Get("haptic_feedback", true))
                Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click);
        }
        catch
        {
            // 设备不支持触感时忽略
        }
    }

    // ------------------------- 新手引导 -------------------------

    /// <summary>View guide → 开始引导（Figma: HOME → Onboarding 1，DISSOLVE）。</summary>
    private void OnViewGuideTapped(object sender, TappedEventArgs e)
    {
        Console.WriteLine("[Onb] View guide tapped");
        Onboarding.Start();
    }

    /// <summary>第1步 Next → 第2步并跳到 Devices 页。</summary>
    private void OnGuideAction()
    {
        Onboarding.Next();
        if (Shell.Current is AppShell shell)
            shell.SelectTab(AppTabBar.DevicesRoute);
    }

    private void OnGuideSkip() => Onboarding.Finish();

    private void OnScene1Rename(object sender, TappedEventArgs e) => OpenRename(1);
    private void OnScene2Rename(object sender, TappedEventArgs e) => OpenRename(2);
    private void OnScene3Rename(object sender, TappedEventArgs e) => OpenRename(3);

    private void OpenRename(int scene)
    {
        Label title = scene switch
        {
            1 => sceneTitle1,
            2 => sceneTitle2,
            _ => sceneTitle3,
        };
        RenameDlg.Confirmed -= OnRenameConfirmed;
        RenameDlg.Cancelled -= OnRenameCancelled;
        _renameTarget = scene;
        RenameDlg.Confirmed += OnRenameConfirmed;
        RenameDlg.Cancelled += OnRenameCancelled;
        RenameDlg.Open(title.Text ?? $"Scene {scene}");
    }

    private int _renameTarget = 1;

    private void OnRenameConfirmed(string newName)
    {
        switch (_renameTarget)
        {
            case 1: sceneTitle1.Text = newName; break;
            case 2: sceneTitle2.Text = newName; break;
            default: sceneTitle3.Text = newName; break;
        }
    }

    private void OnRenameCancelled()
    {
        // 关闭不改名，无需额外处理
    }

    /// <summary>All Devices 总开关：设计稿的绿色大卡，联动 0x04 场景指令。</summary>
    private void OnAllDevicesToggled(object? sender, bool e)
    {
        try
        {
            byte[] sendDatas = new byte[5];
            sendDatas[0] = 0xAA;
            sendDatas[1] = 0x04;
            sendDatas[2] = e ? (byte)2 : (byte)1;
            sendDatas[3] = (byte)_selectedSceneId;
            sendDatas[4] = 0xBB;
            _ = BlueSendData(sendDatas);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => DisplayAlert("Failed", $"Operation error: {ex.Message}", "OK"));
        }
    }

    // ============================== 设备项模型 ==============================

    public class DeviceItem : INotifyPropertyChanged
    {
        private string _connectButtonText;
        private int _rssi;

        public DeviceItem(IDevice device)
        {
            NativeDevice = device;
            Id = device.Id;
            Name = string.IsNullOrEmpty(device.Name) ? "Unknown Device" : device.Name;
            Rssi = device.Rssi;
            ConnectButtonText = "Connect";
        }

        public Guid Id { get; }
        public string Name { get; set; }
        public IDevice NativeDevice { get; }

        public int Rssi
        {
            get => _rssi;
            set
            {
                _rssi = value;
                OnPropertyChanged(nameof(Rssi));
            }
        }

        /// <summary>设计稿列表行的副标题：默认提示可点击，连接中/已连接时显示状态。</summary>
        public string RowHint =>
            _connectButtonText switch
            {
                "Connect" => "Available · Tap to connect",
                "Connecting..." => "Connecting…",
                _ => _connectButtonText
            };

        public string ConnectButtonText
        {
            get => _connectButtonText;
            set
            {
                _connectButtonText = value;
                OnPropertyChanged(nameof(ConnectButtonText));
                OnPropertyChanged(nameof(RowHint));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
