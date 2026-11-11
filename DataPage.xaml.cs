using BluetoothMauiApp;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BluetoothMauiApp;

/// <summary>顶层网关状态模型（协议回填用，未改动）。</summary>
public class GatewayState : INotifyPropertyChanged
{
    public int Id { get; set; }
    // 仅显示 Gateway X，不再生成BA编码
    public string Name => $"Gateway {Id}";

    private bool _isOnline;
    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            _isOnline = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
        }
    }
    public string StatusText => IsOnline ? "Online" : "Offline";
    public Color StatusColor => IsOnline ? ThemeManager.Color("DgGreenText") : ThemeManager.Color("DgOffline");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class DataPage : ContentPage
{
    /// <summary>轮询循环代际：离开页面时递增以结束旧循环，回来时重启。</summary>
    private int _timerGen;
    private int _scanGeneration;

    public ObservableCollection<GatewayState> GatewayList { get; set; } = new();

    private enum PickerState { Scanning, List, Empty, Connecting, Failure, Connected }

    /// <summary>连接超时。与原版一致，给设备足够时间完成连接；仅在完全无响应时兜底。</summary>
    private const int ConnectTimeoutMs = 30000;

    private PickerState _pickerState = PickerState.Scanning;
    private StartPage.DeviceItem? _pendingItem;

    /// <summary>连接进行中（不再占用 Connecting 状态页，列表行内展示连接中）。</summary>
    private bool _isConnecting;

    public DataPage()
    {
        InitializeComponent();
        BindingContext = this;

        // 初始化1~100个网关
        for (int i = 1; i <= 100; i++)
        {
            GatewayList.Add(new GatewayState { Id = i, IsOnline = false });
        }

        StartPage.EnsureEngine();
        DeviceList.ItemsSource = StartPage.Devices;

        // 空态插画：暗色下卡片变深，换暗稿导出的插画
        ThemeManager.BindIcon(imgEmpty, "empty_gateway.png", "empty_gateway_dark.png");

        StartPage.OnDataReceivedAction = OnDataReceived;

        ThemeManager.Changed -= OnThemeChanged;
        ThemeManager.Changed += OnThemeChanged;

        RefreshViewState();
    }

    /// <summary>主题切换后重刷网关状态色（绑定值不会自动重算）。</summary>
    private void OnThemeChanged()
    {
        foreach (var g in GatewayList)
            g.IsOnline = g.IsOnline;
        RefreshViewState();
    }

    // ============================== 状态轮询（协议未改动） ==============================

    /// <summary>启动状态轮询。进入页面时调用，离开页面时由 _timerGen 递增停掉。</summary>
    private void StartStatusTimer()
    {
        int gen = ++_timerGen;
        _ = StatusTimerLoopAsync(gen);
    }

    private async Task StatusTimerLoopAsync(int gen)
    {
        while (gen == _timerGen)
        {
            if (MauiProgram.蓝牙连接)
                QueryDeviceStatus();
            await Task.Delay(1000);
        }
    }

    private void QueryDeviceStatus()
    {
        try
        {
            byte[] sendDatas = new byte[3];
            sendDatas[0] = 0xAA;
            sendDatas[1] = 0x01;
            sendDatas[2] = 0xBB;
            _ = StartPage.BlueSendData(sendDatas);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("失败", $"操作异常：{ex.Message}", "确定");
            });
        }
    }

    private void OnDataReceived(byte[] datas)
    {
        int startIndex = -1;
        for (int i = 0; i < datas.Length; i++)
        {
            if (datas[i] == 0xAA) startIndex = i;
            if (startIndex >= 0 && datas[i] == 0xBB) break;
        }
        if (startIndex < 0) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            for (int i = 0; i < 13; i++)
            {
                int dataPos = startIndex + i + 1;
                if (dataPos >= datas.Length) break;
                byte dataByte = datas[dataPos];
                for (int bit = 0; bit < 8; bit++)
                {
                    int gatewayId = i * 8 + bit + 1;
                    if (gatewayId > 100) break;
                    bool online = (dataByte & (0x01 << bit)) != 0;

                    var item = GatewayList[gatewayId - 1];
                    item.IsOnline = online;
                    MauiProgram.Device[gatewayId - 1] = online;
                }
            }
            RefreshViewState();
        });
    }

    /// <summary>有在线网关显示网格，否则显示设计稿的空态插画。</summary>
    private void RefreshViewState()
    {
        bool anyOnline = GatewayList.Any(g => g.IsOnline);
        GridView.IsVisible = anyOnline;
        EmptyView.IsVisible = !anyOnline;
    }

    // ------------------------- 网关跳转 -------------------------

    private void OpenGateway(int id)
    {
        MauiProgram.net编号 = id;
        _ = Navigation.PushAsync(new Board());
    }

    /// <summary>仅在线网关允许跳转Board页面，离线点击弹窗提示</summary>
    private async void CardTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is int id)
        {
            var targetGateway = GatewayList[id - 1];
            if (!targetGateway.IsOnline)
            {
                await DisplayAlert("提示", $"网关{id}当前离线，无法进入设置页面", "确定");
                return;
            }

            OpenGateway(id);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // 先减后加，避免页面被重复构造时重复订阅
        StartPage.DevicesChanged -= OnDevicesChanged;
        StartPage.DevicesChanged += OnDevicesChanged;
        // Board 构造时会把 OnDataReceivedAction 指向自己，退出时 -= 置空。
        // 不在这里重新接管，从 Board 返回后网关网格就再也收不到状态回包。
        StartPage.OnDataReceivedAction = OnDataReceived;
        ApplySafeArea();
        StartStatusTimer();
        RefreshViewState();
    }

    /// <summary>iOS 安全区由系统处理（页面底色与氛围底同色，安全区无断层）；
    /// 这里只把蓝牙弹窗遮罩向安全区外扩保证蒙层铺满状态栏与 Home 区（Android 为 0 无影响）。</summary>
    private void ApplySafeArea()
    {
        var insets = SafeArea.Insets(this);
        PickerOverlay.Margin = new Thickness(0, -insets.Top, 0, -insets.Bottom);
    }

    protected override void OnDisappearing()
    {
        // 停掉轮询，避免与 Board 页的查询指令打架
        _timerGen++;
        // 退出页面关闭弹窗并停止扫描
        if (PickerOverlay.IsVisible)
        {
            PickerOverlay.IsVisible = false;
            _scanGeneration++;
        }
        _ = StartPage.StopScanAsync();
        StartPage.DevicesChanged -= OnDevicesChanged;
        base.OnDisappearing();
    }

    // ============================== 蓝牙设备选择弹窗（Figma 191:240） ==============================

    /// <summary>点击 Scan with Bluetooth：打开弹窗并开始扫描。</summary>
    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        PickerOverlay.IsVisible = true;
        SetPickerState(PickerState.Scanning);
        await StartPage.StartScanAsync();
        WatchScanTimeout(++_scanGeneration);
    }

    /// <summary>Scan again / Try again / View devices：按当前状态路由（Figma 六态）。</summary>
    private async void OnScanAgainTapped(object sender, TappedEventArgs e)
    {
        if (_pickerState == PickerState.Scanning || _isConnecting)
            return;                                   // 扫描中/连接中按钮不响应

        if (_pickerState == PickerState.Failure)
        {
            await ConnectToPendingAsync();            // Try again → 重试同一设备
            return;
        }

        if (_pickerState == PickerState.Connected)
        {
            ClosePicker();                            // View devices → 返回 Devices
            return;
        }

        // 先结束旧扫描再重新开始，避免重复扫描会话
        await StartPage.StopScanAsync();
        SetPickerState(PickerState.Scanning);
        await StartPage.StartScanAsync();
        WatchScanTimeout(++_scanGeneration);
    }

    /// <summary>扫描超时后按结果切换 列表 / 无结果。</summary>
    private async void WatchScanTimeout(int generation)
    {
        await Task.Delay(StartPage.ScanTimeoutMs);
        // 只有仍停留在扫描态时才允许切列表：
        // 防止扫描超时定时器把 连接中/已连接/失败 页面抢回设备列表
        if (generation != _scanGeneration || !PickerOverlay.IsVisible
            || _pickerState != PickerState.Scanning)
            return;
        MainThread.BeginInvokeOnMainThread(() =>
            SetPickerState(StartPage.Devices.Count > 0 ? PickerState.List : PickerState.Empty));
    }

    private void OnPickerCancel(object sender, TappedEventArgs e) => ClosePicker();

    private void OnPickerBackdrop(object sender, TappedEventArgs e) => ClosePicker();

    private async void ClosePicker()
    {
        _scanGeneration++;                              // 作废在途扫描/连接回调
        PickerOverlay.IsVisible = false;
        await StartPage.StopScanAsync();
        RefreshViewState();
    }

    private async void OnDeviceRowTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not StartPage.DeviceItem item)
            return;
        if (_isConnecting)
            return;                                    // 连接中禁用重复连接

        // 已连接的是这一台 → 再次点击断开
        if (StartPage._connectedDevice != null && StartPage._connectedDevice.Id == item.Id)
        {
            await StartPage.DisconnectAsync();
            SetPickerState(StartPage.Devices.Count > 0 ? PickerState.List : PickerState.Empty);
            return;
        }

        _pendingItem = item;
        await ConnectToPendingAsync();
    }

    /// <summary>
    /// 连接选中设备：不切 Connecting 状态页，停在设备列表里（该行显示 Connecting…）；
    /// 连接的瞬间直接跳到 Connected 并自动收起。
    /// </summary>
    private async Task ConnectToPendingAsync()
    {
        if (_pendingItem == null || _isConnecting)
            return;

        var item = _pendingItem;
        _isConnecting = true;
        int gen = ++_scanGeneration;                   // 作废旧扫描超时与在途回调

        item.ConnectButtonText = "Connecting...";      // 列表行内展示连接中

        var connectTask = StartPage.ConnectAsync(item.NativeDevice);
        var finished = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs));
        await StartPage.StopScanAsync();               // 连接结果出来后再停扫描
        bool cancelled = gen != _scanGeneration || !PickerOverlay.IsVisible;
        _isConnecting = false;

        if (finished != connectTask)
        {
            // 超时 → 失败态；之后若实际连上，忽略迟到结果并断开
            _ = connectTask.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion && t.Result)
                    _ = StartPage.DisconnectAsync();
            }, TaskScheduler.Default);
            item.ConnectButtonText = "Connect";
            if (!cancelled)
                SetPickerState(PickerState.Failure);
            return;
        }

        bool ok = await connectTask;
        if (cancelled)
        {
            // 取消后的迟到回调：忽略结果；已连上则主动断开
            if (ok)
                _ = StartPage.DisconnectAsync();
            item.ConnectButtonText = "Connect";
            return;
        }

        if (!ok)
        {
            item.ConnectButtonText = "Connect";
            SetPickerState(PickerState.Failure);        // 失败走状态页，不再弹 Alert
            return;
        }

        item.ConnectButtonText = "Connected";
        SetPickerState(PickerState.Connected);
        // 连上即完成本次操作：短暂展示 Connected 后自动收起弹窗，无需再点 View devices / Done
        _ = AutoCloseAfterConnectedAsync();
    }

    /// <summary>连接成功后短暂展示 Connected 状态，再自动收起弹窗回到 Devices。</summary>
    private async Task AutoCloseAfterConnectedAsync()
    {
        int gen = _scanGeneration;
        await Task.Delay(1200);
        if (gen != _scanGeneration || !PickerOverlay.IsVisible || _pickerState != PickerState.Connected)
            return;
        ClosePicker();
    }

    /// <summary>扫描结果/连接状态变化时刷新弹窗与列表（仅扫描中才自动切到列表，避免打断连接流程）。</summary>
    private void OnDevicesChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (PickerOverlay.IsVisible && _pickerState == PickerState.Scanning && StartPage.Devices.Count > 0)
                SetPickerState(PickerState.List);
            RefreshViewState();
        });
    }

    /// <summary>六态切换：区块显隐与主/副按钮文案。</summary>
    private void SetPickerState(PickerState state)
    {
        _pickerState = state;
        bool scanning = state == PickerState.Scanning;
        bool list = state == PickerState.List;
        bool empty = state == PickerState.Empty;
        bool connecting = state == PickerState.Connecting;
        bool failure = state == PickerState.Failure;
        bool connected = state == PickerState.Connected;

        PickerScanning.IsVisible = scanning;
        DeviceList.IsVisible = list;
        PickerEmpty.IsVisible = empty;
        PickerConnecting.IsVisible = connecting;
        PickerFailure.IsVisible = failure;
        PickerConnected.IsVisible = connected;

        ScanAgainBtn.Opacity = (scanning || connecting) ? 0.45 : 1.0;
        ScanAgainLabel.Text = state switch
        {
            PickerState.Scanning => "Scanning…",
            PickerState.Connecting => "Connecting…",
            PickerState.Failure => "Try again",
            PickerState.Connected => "View devices",
            _ => "Scan again",
        };
        CancelLabel.Text = connected ? "Done" : "Cancel";

        FoundLabel.Text = state switch
        {
            PickerState.List => $"{StartPage.Devices.Count} devices found",
            PickerState.Scanning => "Only BoxArborMG devices will appear",
            PickerState.Empty => "Only BoxArborMG devices appear here",
            PickerState.Connecting => "Please wait",
            PickerState.Failure => "Connection failed · Retry available",
            PickerState.Connected => "Your gateway is ready",
            _ => "",
        };
    }
}
