using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace BluetoothMauiApp;

/// <summary>
/// 新手引导遮罩 · Figma Onboarding 1~4。
/// 背景 = 画板整页渲染图（演示页，非真实页面），蒙层/聚光灯/引导卡都烘在图里；
/// 坐标按 375 画板系随屏宽等比换算（图已裁 44dp 状态栏带）。
/// 全屏点按 = Next，Skip 区域点按结束。
/// </summary>
public partial class CoachMark : ContentView
{
    private int _step = 1;
    private string _demoImage = "";
    private double _skipX = 302, _skipY = 358;

    public CoachMark()
    {
        InitializeComponent();
        Loaded += (_, _) => { Onboarding.Changed -= OnStepChanged; Onboarding.Changed += OnStepChanged; OnStepChanged(); };
        Unloaded += (_, _) => Onboarding.Changed -= OnStepChanged;
        SizeChanged += (_, _) => ApplyLayout();
        ApplyContent();
    }

    public int Step { get => _step; set { _step = value; ApplyContent(); } }
    public string DemoImage { get => _demoImage; set { _demoImage = value; ApplyContent(); } }

    /// <summary>Skip 热区（画板绝对坐标，含状态栏的画板系）。</summary>
    public double SkipX { get => _skipX; set { _skipX = value; ApplyContent(); } }
    public double SkipY { get => _skipY; set { _skipY = value; ApplyContent(); } }

    public event Action? ActionTapped;
    public event Action? SkipTapped;

    private void OnStepChanged()
    {
        bool show = Onboarding.Step == _step;
        IsVisible = show;
        if (show)
            ApplyContent();
    }

    private void ApplyContent()
    {
        if (DemoImg is null)
            return;
        if (!string.IsNullOrEmpty(_demoImage))
            DemoImg.Source = _demoImage;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (Root is null || DemoImg is null || SkipZone is null)
            return;
        if (Root.Width <= 0)
            return;

        double sc = Root.Width / 375.0;

        // 渲染图 750x1536（= 画板 375x768，已裁 44dp 状态栏带）：按宽度等比设高
        DemoImg.HeightRequest = Root.Width * 1536.0 / 750.0;

        SkipZone.TranslationX = _skipX * sc;
        SkipZone.TranslationY = (_skipY - 44) * sc;
        SkipZone.WidthRequest = 60 * sc;
        SkipZone.HeightRequest = 30 * sc;
    }

    private void OnNextTapped(object sender, TappedEventArgs e) => ActionTapped?.Invoke();
    private void OnSkipTapped(object sender, TappedEventArgs e) => SkipTapped?.Invoke();
}
