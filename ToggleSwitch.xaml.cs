using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace BluetoothMauiApp;

public partial class ToggleSwitch : ContentView
{
    private const uint AnimationLength = 180;

    /// <summary>开关状态。开启时 Track 用 OnColor，关闭时用 OffColor。</summary>
    public static readonly BindableProperty IsOnProperty = BindableProperty.Create(
        nameof(IsOn), typeof(bool), typeof(ToggleSwitch), false,
        propertyChanged: OnIsOnChanged);

    /// <summary>开启色。未显式设置时跟随主题（DgGreen）。</summary>
    public static readonly BindableProperty OnColorProperty = BindableProperty.Create(
        nameof(OnColor), typeof(Color), typeof(ToggleSwitch), null);

    /// <summary>关闭色。未显式设置时跟随主题（DgSwitchOff）。</summary>
    public static readonly BindableProperty OffColorProperty = BindableProperty.Create(
        nameof(OffColor), typeof(Color), typeof(ToggleSwitch), null);

    /// <summary>滑块颜色，默认白色。设计稿总开关那张卡底是绿的，滑块是绿的。</summary>
    public static readonly BindableProperty KnobColorProperty = BindableProperty.Create(
        nameof(KnobColor), typeof(Color), typeof(ToggleSwitch), Colors.White);

    /// <summary>滑块直径。留空则取 高度-4。</summary>
    public static readonly BindableProperty KnobSizeProperty = BindableProperty.Create(
        nameof(KnobSize), typeof(double), typeof(ToggleSwitch), 0d,
        propertyChanged: OnGeometryChanged);

    /// <summary>状态变化时触发（UI 交互用，不接协议）。</summary>
    public event EventHandler<bool>? Toggled;

    /// <summary>滑块可移动的最大位移，由 OnGeometryChanged 算出。</summary>
    private double _travel;

    /// <summary>是否已完成首次布局；未布局前不播动画，直接落位。</summary>
    private bool _laidOut;

    public ToggleSwitch()
    {
        InitializeComponent();
        SizeChanged += (_, _) => OnGeometryChanged();
        Loaded += (_, _) => { ThemeManager.Changed -= OnTheme; ThemeManager.Changed += OnTheme; OnTheme(); };
        Unloaded += (_, _) => ThemeManager.Changed -= OnTheme;
    }

    private void OnTheme()
    {
        if (Track is null) return;
        Track.BackgroundColor = TrackColor;
    }

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public Color? OnColor
    {
        get => (Color?)GetValue(OnColorProperty);
        set => SetValue(OnColorProperty, value);
    }

    public Color? OffColor
    {
        get => (Color?)GetValue(OffColorProperty);
        set => SetValue(OffColorProperty, value);
    }

    public Color KnobColor
    {
        get => (Color)GetValue(KnobColorProperty)!;
        set => SetValue(KnobColorProperty, value);
    }

    public double KnobSize
    {
        get => (double)GetValue(KnobSizeProperty);
        set => SetValue(KnobSizeProperty, value);
    }

    private static void OnIsOnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ToggleSwitch)bindable;
        self.AnimateTo((bool)newValue);
        self.Toggled?.Invoke(self, (bool)newValue);
    }

    private static void OnGeometryChanged(BindableObject bindable, object oldValue, object newValue)
        => ((ToggleSwitch)bindable).OnGeometryChanged();

    private void OnTapped(object sender, TappedEventArgs e) => IsOn = !IsOn;

    private Color TrackColor => IsOn
        ? (OnColor ?? ThemeManager.Color("DgGreen"))
        : (OffColor ?? ThemeManager.Color("DgSwitchOff"));

    /// <summary>状态切换：滑块滑动 + 轨道变色，两个动画同步播 180ms。
    /// 首次布局前不播（位置还没算出来），直接落位。</summary>
    private void AnimateTo(bool on)
    {
        if (Track is null || Knob is null)
            return;

        Color target = on
            ? (OnColor ?? ThemeManager.Color("DgGreen"))
            : (OffColor ?? ThemeManager.Color("DgSwitchOff"));

        if (!_laidOut)
        {
            Track.BackgroundColor = target;
            Knob.Fill = KnobColor;
            Knob.TranslationX = on ? _travel : 0;
            return;
        }

        // 轨道颜色渐变
        Color from = Track.BackgroundColor ?? target;
        Track.BackgroundColor = target;
        if (!from.Equals(target))
        {
            new Animation(v => Track.BackgroundColor = Mix(from, target, v), 0, 1)
                .Commit(this, "trackColor", 16, AnimationLength, Easing.CubicOut,
                        (_, _) => Track.BackgroundColor = target);
        }

        // 滑块位移：取消上一次未完成的动画，避免连点时叠加
        Knob.CancelAnimations();
        double to = on ? _travel : 0;
        double fromX = Knob.TranslationX;
        if (Math.Abs(fromX - to) < 0.5)
        {
            Knob.TranslationX = to;
            return;
        }
        new Animation(v => Knob.TranslationX = v, fromX, to)
            .Commit(this, "knobSlide", 16, AnimationLength, Easing.CubicOut,
                    (_, _) => Knob.TranslationX = to);
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (int)Math.Round((a.Red + (b.Red - a.Red) * t) * 255),
            (int)Math.Round((a.Green + (b.Green - a.Green) * t) * 255),
            (int)Math.Round((a.Blue + (b.Blue - a.Blue) * t) * 255));
    }

    /// <summary>按实际高度算圆角、内边距与滑块位移，保证 47/53/30 三种尺寸都贴合。
    /// 布局变化时直接落位（不播动画），状态切换才播动画。</summary>
    private void OnGeometryChanged()
    {
        if (Track is null || Knob is null || KnobHost is null)
            return;

        double h = Height;
        double w = Width;
        if (h <= 0 || w <= 0)
            return;

        double knob = KnobSize > 0 ? KnobSize : Math.Max(4, h - 4);
        double inset = Math.Max(0, (h - knob) / 2);

        if (Track.StrokeShape is RoundRectangle rr)
            rr.CornerRadius = h / 2;

        Knob.WidthRequest = knob;
        Knob.HeightRequest = knob;
        KnobHost.Padding = new Thickness(inset);

        _travel = Math.Max(0, w - (inset * 2) - knob);

        // 布局期直接落位，避免出现“先在左边再跳过去”
        Knob.CancelAnimations();
        Knob.TranslationX = IsOn ? _travel : 0;
        Track.BackgroundColor = TrackColor;
        Knob.Fill = KnobColor;
        _laidOut = true;
    }
}
