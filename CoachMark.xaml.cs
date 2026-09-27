using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace BluetoothMauiApp;

/// <summary>
/// 新手引导遮罩（Figma Onboarding 1~4）。压暗层按 Target 的实际位置挖洞，
/// 卡片内容按设计稿固定间距（12dp）堆叠，激活的进度点是 18x6 胶囊。
/// 显隐由 <see cref="Onboarding.Step"/> 驱动。
/// </summary>
public partial class CoachMark : ContentView
{
    private int _step = 1;
    private double _cardY = 340;
    private string _title = "";
    private string _desc = "";
    private string _action = "Next";
    private string _illustration = "guide1.png";
    private VisualElement? _target;
    private double _spotL = 6, _spotT = 6, _spotR = 6, _spotB = 6;
    private double _spotW, _spotH;
    private double _spotX = -1, _spotY = -1, _spotYFromBottom = -1;
    private bool _spotCenter;
    private double _spotOffX, _spotOffY;


    public CoachMark()
    {
        InitializeComponent();
        // 用 Loaded/Unloaded 管理订阅，避免页面重建时旧实例一直挂着
        Loaded += (_, _) => { Onboarding.Changed -= OnStepChanged; Onboarding.Changed += OnStepChanged; OnStepChanged(); };
        Unloaded += (_, _) => Onboarding.Changed -= OnStepChanged;
        SizeChanged += (_, _) => { ApplyContent(); ScheduleOverlay(); };
        ApplyContent();
    }

    // ---- XAML 属性（非绑定属性，setter 只存值，等布局后再应用） ----

    public int Step { get => _step; set { _step = value; ApplyContent(); } }
    public double CardY { get => _cardY; set { _cardY = value; ApplyContent(); } }
    public string Title { get => _title; set { _title = value; ApplyContent(); } }
    public string Description { get => _desc; set { _desc = value; ApplyContent(); } }
    public string ActionText { get => _action; set { _action = value; ApplyContent(); } }
    public string Illustration { get => _illustration; set { _illustration = value; ApplyContent(); } }

    /// <summary>聚光灯要高亮的目标元素（页面内）。</summary>
    public VisualElement? Target { get => _target; set { _target = value; ScheduleOverlay(); } }

    /// <summary>聚光灯相对目标四边的外扩（设计稿约 5~9dp）。</summary>
    public double SpotLeft { get => _spotL; set { _spotL = value; ScheduleOverlay(); } }
    public double SpotTop { get => _spotT; set { _spotT = value; ScheduleOverlay(); } }
    public double SpotRight { get => _spotR; set { _spotR = value; ScheduleOverlay(); } }
    public double SpotBottom { get => _spotB; set { _spotB = value; ScheduleOverlay(); } }

    /// <summary>聚光灯固定尺寸；0 表示按目标尺寸 + 外扩。</summary>
    public double SpotWidth { get => _spotW; set { _spotW = value; ScheduleOverlay(); } }
    public double SpotHeight { get => _spotH; set { _spotH = value; ScheduleOverlay(); } }

    /// <summary>聚光灯绝对 X；-1 表示按目标推算。设计稿第 1 步用绝对值。</summary>
    public double SpotX { get => _spotX; set { _spotX = value; ScheduleOverlay(); } }

    /// <summary>聚光灯绝对 Y；-1 表示按目标推算。</summary>
    public double SpotY { get => _spotY; set { _spotY = value; ScheduleOverlay(); } }

    /// <summary>聚光灯距屏幕底部的距离（y = 屏高 - 该值）。第 1 步高亮底部 Tab 用。</summary>
    public double SpotYFromBottom { get => _spotYFromBottom; set { _spotYFromBottom = value; ScheduleOverlay(); } }

    /// <summary>聚光灯是否以目标为中心（设计稿第 2/3/4 步都是居中）。</summary>
    public bool SpotCenter { get => _spotCenter; set { _spotCenter = value; ScheduleOverlay(); } }

    /// <summary>聚光灯相对目标的偏移（设计稿四步的洞都是对高亮元素的固定偏移，换屏自适应）。</summary>
    public double SpotOffsetX { get => _spotOffX; set { _spotOffX = value; ScheduleOverlay(); } }
    public double SpotOffsetY { get => _spotOffY; set { _spotOffY = value; ScheduleOverlay(); } }

    /// <summary>点击引导动作（Next / Start using）。</summary>
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
        var (ox, oy) = RootOrigin();
        // 设计稿坐标是屏幕绝对值，Root 是内容区坐标，先换算成 Root 坐标系
        Card.Margin = new Thickness(20 - ox, Math.Max(0, _cardY - oy), 20, 0);

        if (!string.IsNullOrEmpty(_title)) TitleLabel.Text = _title;
        if (!string.IsNullOrEmpty(_desc)) DescLabel.Text = _desc;
        NextLabel.Text = _action;
        IllustrationImg.Source = _illustration;

        // 进度点：当前步是 18x6 胶囊，其余 6x6
        Border[] dots = { Dot1, Dot2, Dot3, Dot4 };
        for (int i = 0; i < dots.Length; i++)
        {
            bool active = (i + 1) == _step;
            dots[i].WidthRequest = active ? 18 : 6;
            dots[i].BackgroundColor = Color.FromHex(active ? "#16c66a" : "#c6d5cd");
        }
    }

    private void ScheduleOverlay()
    {
        UpdateOverlay();
        // 目标元素可能还没完成布局，延后再校一次
        _ = Task.Run(async () =>
        {
            await Task.Delay(180);
            await MainThread.InvokeOnMainThreadAsync(UpdateOverlay);
        });
    }

    /// <summary>把 Target 的实际位置换算成聚光灯矩形，再用 4 块矩形围出洞。</summary>
    private void UpdateOverlay()
    {
        if (DimTop is null || Root is null || !IsVisible)
            return;
        if (Root.Width <= 0 || Root.Height <= 0)
            return;

        var (ox, oy) = RootOrigin();
        Rect raw = GetTargetBounds();
        if (raw.Width <= 0 || raw.Height <= 0)
            return;
        // 屏幕绝对坐标 -> Root 坐标系
        Rect b = new Rect(raw.X - ox, raw.Y - oy, raw.Width, raw.Height);

        double w = _spotW > 0 ? _spotW : b.Width + _spotL + _spotR;
        double h = _spotH > 0 ? _spotH : b.Height + _spotT + _spotB;

        double x;
        if (_spotX >= 0) x = _spotX;
        else if (_spotCenter) x = b.X + b.Width / 2 - w / 2;
        else x = b.X - _spotL;

        double y;
        if (_spotYFromBottom >= 0) y = Root.Height - _spotYFromBottom;
        else if (_spotY >= 0) y = _spotY;
        else if (_spotCenter) y = b.Y + b.Height / 2 - h / 2;
        else y = b.Y - _spotT;

        // 相对偏移模式：优先级最高（覆盖上面按目标算出来的值）
        if (_spotOffX != 0) x = b.X + _spotOffX;
        if (_spotOffY != 0) y = b.Y + _spotOffY;

        x = Math.Max(0, x); y = Math.Max(0, y);
        w = Math.Min(w, Root.Width - x);
        h = Math.Min(h, Root.Height - y);
        if (w <= 0 || h <= 0) return;

        double W = Root.Width, H = Root.Height;

        Place(DimTop, 0, 0, W, y);
        Place(DimBottom, 0, y + h, W, H - (y + h));
        Place(DimLeft, 0, y, x, h);
        Place(DimRight, x + w, y, W - (x + w), h);

    }

    private static void Place(BoxView v, double x, double y, double w, double h)
    {
        v.IsVisible = w > 0.5 && h > 0.5;
        if (!v.IsVisible) return;
        v.WidthRequest = w;
        v.HeightRequest = h;
        v.TranslationX = x;
        v.TranslationY = y;
    }

    /// <summary>Root 在屏幕上的原点。GetTargetBounds 累加出来是屏幕绝对坐标，
    /// 而压暗层/卡片都画在 Root 里，必须先减掉这个原点。</summary>
    private (double x, double y) RootOrigin()
    {
        if (Root is null) return (0, 0);
        double x = 0, y = 0;
        Element? e = Root;
        while (e is View v)
        {
            x += v.X;
            y += v.Y;
            e = v.Parent;
        }
        return (x, y);
    }

    /// <summary>把目标元素的位置累加成屏幕绝对坐标（跨 ScrollView 时为布局值）。</summary>
    private Rect GetTargetBounds()
    {
        if (_target is null) return Rect.Zero;
        double x = 0, y = 0;
        Element? e = _target;
        while (e is View v && !ReferenceEquals(v, Root))
        {
            x += v.X;
            y += v.Y;
            e = v.Parent;
        }
        return new Rect(x, y, _target.Width, _target.Height);
    }

    private void OnNextTapped(object sender, TappedEventArgs e) => ActionTapped?.Invoke();
    private void OnSkipTapped(object sender, TappedEventArgs e) => SkipTapped?.Invoke();
}
