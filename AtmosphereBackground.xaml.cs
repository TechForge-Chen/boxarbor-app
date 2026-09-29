namespace BluetoothMauiApp;

public partial class AtmosphereBackground : ContentView
{
    public AtmosphereBackground()
    {
        InitializeComponent();
        Loaded += (_, _) => { ThemeManager.Changed -= OnTheme; ThemeManager.Changed += OnTheme; OnTheme(); };
        Unloaded += (_, _) => ThemeManager.Changed -= OnTheme;
    }

    /// <summary>暗色页面是纯色（Figma 暗稿无光晕），隐藏三团光晕图。</summary>
    private void OnTheme()
    {
        bool show = !ThemeManager.IsDark;
        if (GlowTop is not null) GlowTop.IsVisible = show;
        if (GlowRight is not null) GlowRight.IsVisible = show;
        if (GlowBottom is not null) GlowBottom.IsVisible = show;
    }
}
