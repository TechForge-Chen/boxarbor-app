using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;

namespace BluetoothMauiApp;

public partial class Board : ContentPage
{
    private bool _isConnect = false;
    private bool _sceneSwitchFlag = false;
    private bool _initFlag = false;
    private bool _isAutoRefreshingUI = false;

    public int[] Para = new int[550];
    private int _queryFlag = 0;
    private readonly List<Frame> _allGateFrames = new();
    private int _currentGate = 1;

    /// <summary>网关显示名（设计稿标题位），默认 Gateway N，可被 Rename 改写。</summary>
    private string _gatewayName = "";

    /// <summary>滑杆 → 百分比标签（设计稿右侧的 35% / 80%）</summary>
    private readonly Dictionary<Slider, Label> _sliderLabels = new();

    private const byte FRAME_HEAD = 0xAA;
    private const byte FRAME_END = 0xBB;
    private const int SINGLE_GATE_DATA_LEN = 28;
    private const int MaxGates = 18;
    private int _displayedGateCount = 0;

    public Board()
    {
        InitializeComponent();
        // 构造期间 ModePicker/ColorPicker.SelectedIndex 赋值会触发 SelectedIndexChanged，
        // 未连接时会直接弹 "Bluetooth not connected" 把页面盖住。
        // _initFlag 表示“初始化已完成”，构造结束前置 false，由下面的守卫拦掉这些回填。
        _initFlag = false;
        StartPage.OnDataReceivedAction = OnDataReceived;
        _isConnect = MauiProgram.蓝牙连接;

        _gatewayName = $"Gateway {MauiProgram.net编号}";
        MapSliderLabels();

        RefreshNetLabel();

        string[] modeList = { "Scene1", "Scene2", "Scene3" };
        foreach (var m in modeList)
            ModePicker.Items.Add(m);

        ModePicker.SelectedIndex = 0;

        string[] colorList = { "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "Black" };
        foreach (var c in colorList)
        {
            DDColorPicker1.Items.Add(c);
            DDColorPicker2.Items.Add(c);
            DDColorPicker3.Items.Add(c);
        }
        DDColorPicker1.SelectedIndex = 0;
        DDColorPicker2.SelectedIndex = 0;
        DDColorPicker3.SelectedIndex = 0;

        RebuildGateList(GetBoardCount());
        RefreshAllSliderLabels();

        _initFlag = true;   // 初始化完成，之后的控件回填才允许下发
    }

    private void MapSliderLabels()
    {
        _sliderLabels[DSHzSet1] = HzVal1;
        _sliderLabels[DSBrightSet1] = BrVal1;
        _sliderLabels[DSHzSet2] = HzVal2;
        _sliderLabels[DSBrightSet2] = BrVal2;
        _sliderLabels[DSHzSet3] = HzVal3;
        _sliderLabels[DSBrightSet3] = BrVal3;
        _sliderLabels[DDHzSet1] = DDHzVal1;
        _sliderLabels[DDBrightSet1] = DDBrVal1;
        _sliderLabels[DDHzSet2] = DDHzVal2;
        _sliderLabels[DDBrightSet2] = DDBrVal2;
        _sliderLabels[DDHzSet3] = DDHzVal3;
        _sliderLabels[DDBrightSet3] = DDBrVal3;
    }

    private void RefreshAllSliderLabels()
    {
        foreach (var kv in _sliderLabels)
            SetSliderLabel(kv.Key, kv.Value);
    }

    private static void SetSliderLabel(Slider slider, Label label)
    {
        if (slider is null || label is null)
            return;
        label.Text = $"{Math.Round(slider.Value)}%";
    }

    private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (sender is Slider s && _sliderLabels.TryGetValue(s, out var label))
            label.Text = $"{Math.Round(e.NewValue)}%";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isConnect = MauiProgram.蓝牙连接;
        RebuildGateList(GetBoardCount());
        RefreshNetLabel();
        // 打开界面自动查询当前状态（不弹未连接提示）
        GetNetPara(false);
    }

    protected override void OnDisappearing()
    {
        StartPage.OnDataReceivedAction -= OnDataReceived;
        // 页面退出强制清零，防止常驻锁死
        _queryFlag = 0;
    }


    private void RefreshNetLabel()
    {
        LabCurrentNet.Text = string.IsNullOrEmpty(_gatewayName)
            ? $"Gateway {MauiProgram.net编号}"
            : _gatewayName;
    }

    private void CollectGateFrames()
    {
        _allGateFrames.Clear();
        foreach (var child in GateListLayout.Children)
        {
            if (child is Frame frame)
                _allGateFrames.Add(frame);
        }
    }

    private void OnSelectGate(object sender, TappedEventArgs e)
    {
        string paramStr = e.Parameter?.ToString();
        if (int.TryParse(paramStr, out int num))
        {
            _currentGate = num;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RefreshGateStyle();
            });
            GetNetPara();
        }
    }

    private void RefreshGateStyle()
    {
        // 设计稿：选中 = 绿底白字 #12bd63，未选中 = 白底 #4c6858
        foreach (var frame in _allGateFrames)
        {
            var tapGesture = frame.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault();
            if (tapGesture == null)
                continue;

            string gateParam = tapGesture.CommandParameter?.ToString();
            if (!int.TryParse(gateParam, out int gateNum))
                continue;

            var label = frame.Content as Label;
            if (label == null)
                continue;

            bool isCurrent = gateNum == _currentGate;
            frame.BackgroundColor = isCurrent ? Color.FromHex("#12bd63") : Colors.White;
            label.TextColor = isCurrent ? Colors.White : Color.FromHex("#4c6858");
        }
    }

    private int GetBoardCount()
    {
        if (int.TryParse(DBTNum.Text, out int c) && c >= 1)
            return Math.Min(c, MaxGates);
        return 1;
    }

    // 根据Board Count动态生成左侧 Box 选择轨（Figma：＋ 在最上，其下按 43x39 / r18 递减排列）
    private void RebuildGateList(int count)
    {
        if (count < 1) count = 1;
        if (count > MaxGates) count = MaxGates;

        // 当前选中的通道若超出范围则回退
        if (_currentGate > count) _currentGate = count;
        if (_currentGate < 1) _currentGate = 1;

        // 数量没变就只刷新选中态，避免每次查询都重建闪烁
        if (count == _displayedGateCount)
        {
            RefreshGateStyle();
            return;
        }

        GateListLayout.Children.Clear();

        // 设计稿最上方是绿色 ＋（添加 Box）：本轮仅视觉，不接协议
        var addFrame = new Frame
        {
            BackgroundColor = Color.FromHex("#12bd63"),
            BorderColor = Colors.White,
            HasShadow = false,
            CornerRadius = 18,
            Padding = 0,
            HeightRequest = 39,
            WidthRequest = 43
        };
        addFrame.Content = new Image
        {
            Source = "ic_plus.png",
            WidthRequest = 18,
            HeightRequest = 18,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        GateListLayout.Children.Add(addFrame);

        for (int i = count; i >= 1; i--)
        {
            bool isCurrent = (i == _currentGate);

            var frame = new Frame
            {
                BackgroundColor = isCurrent ? Color.FromHex("#12bd63") : Colors.White,
                BorderColor = Colors.White,
                HasShadow = false,
                CornerRadius = 18,
                Padding = 0,
                HeightRequest = 39,
                WidthRequest = 43
            };

            var label = new Label
            {
                Text = i.ToString(),
                FontSize = 12,
                FontFamily = "PJSM",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TextColor = isCurrent ? Colors.White : Color.FromHex("#4c6858"),
                InputTransparent = true
            };
            frame.Content = label;

            var tap = new TapGestureRecognizer();
            tap.CommandParameter = i;
            tap.Tapped += OnSelectGate;
            frame.GestureRecognizers.Add(tap);

            GateListLayout.Children.Add(frame);
        }

        _displayedGateCount = count;
        CollectGateFrames();
        RefreshGateStyle();
    }

    private void DBTNum_Unfocused(object sender, EventArgs e)
    {
        ApplyBoardCountChange();
    }

    private void DBTNum_Completed(object sender, EventArgs e)
    {
        ApplyBoardCountChange();
    }

    private async void ApplyBoardCountChange()
    {
        // 自动刷新（设备回查回填）时，不重复发送，避免乒乓
        if (_isAutoRefreshingUI)
            return;
        _isConnect = MauiProgram.蓝牙连接;
        int newCount = GetBoardCount();
        // 先立即重建左侧通道列表（视觉立刻更新，不等设备回传）
        RebuildGateList(newCount);
        if (!_isConnect)
            return;

        // 等待前一次查询/发送完成，避免冲突（最多等500ms）
        int wait = 0;
        while (_queryFlag > 0 && wait < 500)
        {
            await Task.Delay(50);
            wait += 50;
        }
        _queryFlag = 0;

        SendParaToDevice();
        // 设置完成后延迟回查，确保显示的是设备上最新的值
        await Task.Delay(400);
        GetNetPara(false);
    }

    private void GetNetPara(bool showAlert = true)
    {
        _isConnect = MauiProgram.蓝牙连接;
        if (!_isConnect)
        {
            if (showAlert)
                DisplayAlert("Notice", "Bluetooth not connected", "OK");
            return;
        }
        try
        {
            byte[] sendBuf = new byte[5];
            sendBuf[0] = FRAME_HEAD;
            sendBuf[1] = 0x02;
            sendBuf[2] = (byte)MauiProgram.net编号;
            sendBuf[3] = (byte)_currentGate;
            sendBuf[4] = FRAME_END;
            _queryFlag = 1;
            _ = StartPage.BlueSendData(sendBuf);

            // 新增：2秒超时强制清零，收不到回复自动解锁，永久卡死兜底
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(2000);
                if (_queryFlag == 1)
                {
                    _queryFlag = 0;
                }
            });
        }
        catch
        {
            DisplayAlert("Query Failed", "Bluetooth send error", "OK");
            // 发送异常直接清零，避免卡死
            _queryFlag = 0;
        }
    }

    private void OnDataReceived(byte[] datas)
    {
        if (_queryFlag <= 0)
            return;

        int startIndex = -1;
        int dataLen = 0;
        bool findHead = false;

        for (int i = 0; i < datas.Length; i++)
        {
            if (datas[i] == FRAME_HEAD)
            {
                startIndex = i;
                findHead = true;
            }
            dataLen++;
            if (findHead && datas[i] == FRAME_END)
                break;
        }

        // 放宽长度限制：取消硬性400字节要求，只要完整帧头尾就解析，避免半包直接放弃清零
        if (startIndex < 0 || dataLen < 10)
            return;

        Array.Clear(Para, 0, Para.Length);
        int copyCount = Math.Min(dataLen - 1, Para.Length);
        for (int i = 0; i < copyCount; i++)
            Para[i] = datas[startIndex + 1 + i];

        UpdateUi(_currentGate);
        // 合法收到帧，立刻清零
        _queryFlag = 0;
    }

    private async void UpdateUi(int gate)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            _isAutoRefreshingUI = true;

            int k = 0;
            int offset = (gate - 1) * SINGLE_GATE_DATA_LEN;

            if (Para[1] < 3) ModePicker.SelectedIndex = Para[1];
            if (Para[2] < 18) DBTNum.Text = Para[3].ToString();
            RebuildGateList(GetBoardCount());

            k = 7;
            DSHzSet1.Value = Para[offset + k++];
            DSBrightSet1.Value = Para[offset + k++];
            DSHzSet2.Value = Para[offset + k++];
            DSBrightSet2.Value = Para[offset + k++];
            DSHzSet3.Value = Para[offset + k++];
            DSBrightSet3.Value = Para[offset + k++];
            DDNum1.Text = Para[offset + k++].ToString();
            DDColorPicker1.SelectedIndex = Para[offset + k++];
            DDHzSet1.Value = Para[offset + k++];
            DDBrightSet1.Value = Para[offset + k++];
            DDNum2.Text = Para[offset + k++].ToString();
            DDColorPicker2.SelectedIndex = Para[offset + k++];
            DDHzSet2.Value = Para[offset + k++];
            DDBrightSet2.Value = Para[offset + k++];
            DDNum3.Text = Para[offset + k++].ToString();
            DDColorPicker3.SelectedIndex = Para[offset + k++];
            DDHzSet3.Value = Para[offset + k++];
            DDBrightSet3.Value = Para[offset + k++];
            k++;
            //LabTemp.Text = Para[offset + k++].ToString();
            //LabHumi.Text = Para[offset + k++].ToString();

            await Task.Delay(80);
            _isAutoRefreshingUI = false;
            RefreshAllSliderLabels();
        });
    }

    private void SendParaToDevice()
    {
        _isConnect = MauiProgram.蓝牙连接;
        if (!_isConnect)
        {
            DisplayAlert("Error", "Bluetooth not connected", "OK");
            return;
        }

        try
        {
            int gateOffset = (_currentGate - 1) * SINGLE_GATE_DATA_LEN;
            Para[0] = 0;
            Para[1] = ModePicker.SelectedIndex;

            if (!int.TryParse(DBTNum.Text, out int dbNum))
            {
                DisplayAlert("Input Error", "Board count must be a number", "OK");
                return;
            }
            Para[3] = dbNum;
            int k = 7;

            Para[gateOffset + k++] = (int)DSHzSet1.Value;
            Para[gateOffset + k++] = (int)DSBrightSet1.Value;
            Para[gateOffset + k++] = (int)DSHzSet2.Value;
            Para[gateOffset + k++] = (int)DSBrightSet2.Value;
            Para[gateOffset + k++] = (int)DSHzSet3.Value;
            Para[gateOffset + k++] = (int)DSBrightSet3.Value;

            if (!int.TryParse(DDNum1.Text, out int d1))
            {
                DisplayAlert("Input Error", "Strip 1 count invalid", "OK");
                return;
            }
            Para[gateOffset + k++] = d1;
            Para[gateOffset + k++] = DDColorPicker1.SelectedIndex;
            Para[gateOffset + k++] = (int)DDHzSet1.Value;
            Para[gateOffset + k++] = (int)DDBrightSet1.Value;

            if (!int.TryParse(DDNum2.Text, out int d2))
            {
                DisplayAlert("Input Error", "Strip 2 count invalid", "OK");
                return;
            }
            Para[gateOffset + k++] = d2;
            Para[gateOffset + k++] = DDColorPicker2.SelectedIndex;
            Para[gateOffset + k++] = (int)DDHzSet2.Value;
            Para[gateOffset + k++] = (int)DDBrightSet2.Value;

            if (!int.TryParse(DDNum3.Text, out int d3))
            {
                DisplayAlert("Input Error", "Strip 3 count invalid", "OK");
                return;
            }
            Para[gateOffset + k++] = d3;
            Para[gateOffset + k++] = DDColorPicker3.SelectedIndex;
            Para[gateOffset + k++] = (int)DDHzSet3.Value;
            Para[gateOffset + k++] = (int)DDBrightSet3.Value;

            byte[] sendBuf = new byte[550];
            Array.Fill(sendBuf, (byte)0x00);
            sendBuf[0] = FRAME_HEAD;
            sendBuf[1] = 0x03;
            sendBuf[2] = (byte)MauiProgram.net编号;
            sendBuf[3] = (byte)_currentGate;

            int pos = 4, i = 0;
            int totalSendLen = SINGLE_GATE_DATA_LEN * 18;
            for (i = 0; i < totalSendLen && pos < 549; i++)
                sendBuf[pos++] = (byte)Para[i];
            sendBuf[pos++] = FRAME_END;

            _ = StartPage.BlueSendData(sendBuf);
        }
        catch (Exception e)
        {
            DisplayAlert("Parameter Send Error", e.Message, "OK");
        }
    }

    private void OnButtonSearch_Clicked(object sender, EventArgs e) => GetNetPara();

    // ============================== 设计稿新增的 UI 交互 ==============================

    /// <summary>页头返回：回到 Devices 网格。</summary>
    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        try
        {
            await Navigation.PopAsync();
        }
        catch
        {
            // 已经在栈底时忽略
        }
    }

    /// <summary>页头铅笔：重命名当前网关（仅改显示名，不动硬件 ID 与过滤规则）。</summary>
    private void OnGatewayRename(object sender, TappedEventArgs e)
    {
        RenameDlg.Confirmed -= OnGatewayNameConfirmed;
        RenameDlg.Confirmed += OnGatewayNameConfirmed;
        RenameDlg.Open(_gatewayName);
    }

    private void OnGatewayNameConfirmed(string newName)
    {
        _gatewayName = newName;
        RefreshNetLabel();
    }

    /// <summary>Save to scene：打开场景选择弹窗（Figma 84:452）。</summary>
    private void OnSaveToScene(object sender, TappedEventArgs e)
    {
        int current = ModePicker.SelectedIndex >= 0 ? ModePicker.SelectedIndex + 1 : 1;
        HighlightSaveScene(current);
        SaveOverlay.IsVisible = true;
    }

    private void OnSaveBackdrop(object sender, TappedEventArgs e) => SaveOverlay.IsVisible = false;

    private void OnSaveScenePicked1(object sender, TappedEventArgs e) => PickSaveScene(1);
    private void OnSaveScenePicked2(object sender, TappedEventArgs e) => PickSaveScene(2);
    private void OnSaveScenePicked3(object sender, TappedEventArgs e) => PickSaveScene(3);

    /// <summary>设计稿弹窗没有 Save/Cancel，按「点击即保存」实现：选中后立即下发并关闭。</summary>
    private void PickSaveScene(int scene)
    {
        HighlightSaveScene(scene);

        int index = scene - 1;
        if (ModePicker.Items.Count > index)
        {
            if (ModePicker.SelectedIndex != index)
                ModePicker.SelectedIndex = index;   // 变更时由 SelectedIndexChanged 下发
            else
            {
                // 选中当前场景同样要保存，不能静默跳过
                _sceneSwitchFlag = true;
                Para[1] = index;
                SendParaToDevice();
            }
        }

        SaveOverlay.IsVisible = false;
        StartPage.TapHaptic();
    }

    private void HighlightSaveScene(int scene)
    {
        Color green = Color.FromHex("#12bd63");
        Color greenFaint = Color.FromHex("#e0faeb");
        Color border = Color.FromHex("#dbe8e0");

        SaveOpt1.BackgroundColor = scene == 1 ? greenFaint : Colors.White;
        SaveOpt1.Stroke = new SolidColorBrush(scene == 1 ? green : border);
        SaveDot1.Fill = new SolidColorBrush(scene == 1 ? green : Colors.White);

        SaveOpt2.BackgroundColor = scene == 2 ? greenFaint : Colors.White;
        SaveOpt2.Stroke = new SolidColorBrush(scene == 2 ? green : border);
        SaveDot2.Fill = new SolidColorBrush(scene == 2 ? green : Colors.White);

        SaveOpt3.BackgroundColor = scene == 3 ? greenFaint : Colors.White;
        SaveOpt3.Stroke = new SolidColorBrush(scene == 3 ? green : border);
        SaveDot3.Fill = new SolidColorBrush(scene == 3 ? green : Colors.White);
    }


    private void MySlider_DragCompleted(object sender, EventArgs e)
    {
        if (_queryFlag > 0)
            return;
        SendParaToDevice();
    }

    private void MyEntry_Unfocused(object sender, EventArgs e)
    {
        if (_queryFlag > 0)
            return;
        SendParaToDevice();
    }

    private void ColorPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (!_initFlag || _queryFlag > 0)
            return;
        SendParaToDevice();
    }

    private void ModePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        _sceneSwitchFlag = true;
        if (!_initFlag || _queryFlag > 0 || _isAutoRefreshingUI)
            return;

        Para[1] = ModePicker.SelectedIndex;
        SendParaToDevice();
        UpdateUi(_currentGate);
    }
}