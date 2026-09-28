using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace BluetoothMauiApp;

/// <summary>
/// 新手引导遮罩 · Figma Onboarding 1~4。
/// 背景 = 画板整页渲染图（演示页，非真实页面），其上叠蒙层/聚光灯/引导卡；
/// 聚光灯用画板绝对坐标（SpotX/SpotY，屏幕坐标系），全屏点洞 = Next。
/// </summary>
public partial class CoachMark : ContentView
{
    private int _step = 1;
    private double _cardY = 340;
    private string _title = "";
    private string _desc = "";
    private string _action = "Next";
    private string _illustration = "guide1.png";
    private string _demoImage = "";
    private double _spotX = -1, _spotY = -1, _spotW, _spotH;

    public CoachMark()
    {
        InitializeComponent();
        Loaded += (_, _) => { Onboarding.Changed -= OnStepChanged; Onboarding.Changed += OnStepChanged; OnStepChanged(); };
        Unloaded += (_, _) => Onboarding.Changed -= OnStepChanged;
        SizeChanged += (_, _) => { ApplyContent(); ScheduleOverlay(); };
        ApplyContent();
    }

    public int Step { get => _step; set { _step = value; ApplyContent(); } }
    public double CardY { get => _cardY; set { _cardY = value; ApplyContent(); } }
    public string Title { get => _title; set { _title = value; ApplyContent(); } }
    public string Description { get => _desc; set { _desc = value; ApplyContent(); } }
    public string ActionText { get => _action; set { _action = value; ApplyContent(); } }
    public string Illustration { get => _illustration; set { _illustration = value; ApplyContent(); } }
    public string DemoImage { get => _demoImage; set { _demoImage = value; ApplyContent(); } }

    /// <summary>聚光灯矩形（设计稿绝对坐标，含状态栏的屏幕坐标系）。四项都给定时不再依赖 Target。</summary>
    public double SpotX { get => _spotX; set { _spotX = value; ScheduleOverlay(); } }
    public double SpotY { get => _spotY; set { _spotY = value; ScheduleOverlay(); } }
    public double SpotWidth { get => _spotW; set { _spotW = value; ScheduleOverlay(); } }
    public double SpotHeight { get => _spotH; set { _spotH = value; ScheduleOverlay(); } }

    public event Action? ActionTapped;
    public event Action? SkipTapped;

    private void OnStepChanged()
    {
        bool show = Onboarding.Step == _step;
        IsVisible = show;
        if (show)
        {
            ApplyContent();
            ScheduleOverlay();
        }
    }

    private void ApplyContent()
    {
        if (Card is null || StepLabel is null)
            return;

        StepLabel.Text = $"{_step} / 4";
        // 设计稿坐标是 375 宽画板系：按屏宽等比换算到 Root
        double sc = Root is { Width: > 0 } ? Root.Width / 375.0 : 1;
        Card.Margin = new Thickness(20 * sc, Math.Max(0, (_cardY - 44) * sc), 20 * sc, 0);

        if (!string.IsNullOrEmpty(_title)) TitleLabel.Text = _title;
        if (!string.IsNullOrEmpty(_desc)) DescLabel.Text = _desc;
        NextLabel.Text = _action;
        IllustrationImg.Source = _illustration;
        if (!string.IsNullOrEmpty(_demoImage))
            DemoImg.Source = _demoImage;

        Border[] dots = { Dot1, Dot2, Dot3, Dot4 };
        for (int i = 0; i < dots.Length; i++)
        {
            bool active = (i + 1) == _step;
            dots[i].WidthRequest = active ? 18 : 6;
            dots[i].BackgroundColor = Color.FromHex(active ? "#16c66a" : "#c6d5cd");
        }

        // 演示图 750x1536（已裁状态栏带）：按宽度等比设高
        if (Root is not null && Root.Width > 0)
            DemoImg.HeightRequest = Root.Width * 1536.0 / 750.0;
    }

    private void ScheduleOverlay()
    {
        UpdateOverlay();
        _ = Task.Run(async () =>
        {
            await Task.Delay(180);
            await MainThread.InvokeOnMainThreadAsync(UpdateOverlay);
        });
    }

    /// <summary>按固定聚光灯矩形（设计稿绝对坐标 - 页面原点）用 4 块矩形围出洞。</summary>
    private void UpdateOverlay()
    {
        if (DimTop is null || Root is null || !IsVisible)
            return;
        if (Root.Width <= 0 || Root.Height <= 0)
            return;
        if (_spotX < 0 || _spotY < 0 || _spotW <= 0 || _spotH <= 0)
            return;

        double sc = Root.Width / 375.0;
        double x = Math.Max(0, _spotX * sc);
        double y = Math.Max(0, (_spotY - 44) * sc);   // 演示图已裁44dp状态栏带
        double w = Math.Min(_spotW * sc, Root.Width - x);
        double h = Math.Min(_spotH * sc, Root.Height - y);
        if (w <= 0 || h <= 0) return;

        double W = Root.Width, H = Root.Height;

        Place(DimTop, 0, 0, W, y);
        Place(DimBottom, 0, y + h, W, H - (y + h));
        Place(DimLeft, 0, y, x, h);
        Place(DimRight, x + w, y, W - (x + w), h);
        const double t = 2;   // 描边 2dp
        Place(StrokeTop, x, y, w, t);
        Place(StrokeBottom, x, y + h - t, w, t);
        Place(StrokeLeft, x, y, t, h);
        Place(StrokeRight, x + w - t, y, t, h);
        Place(SpotCatcher, x, y, w, h);
    }

    private static void Place(VisualElement v, double x, double y, double w, double h)
    {
        v.IsVisible = w > 0.5 && h > 0.5;
        if (!v.IsVisible) return;
        v.WidthRequest = w;
        v.HeightRequest = h;
        v.TranslationX = x;
        v.TranslationY = y;
    }

    private void OnNextTapped(object sender, TappedEventArgs e) => ActionTapped?.Invoke();
    private void OnSkipTapped(object sender, TappedEventArgs e) => SkipTapped?.Invoke();
}
