namespace BluetoothMauiApp;

public partial class AtmosphereBackground : ContentView
{
    public AtmosphereBackground()
    {
        InitializeComponent();
        Loaded += (_, _) => { ThemeManager.Changed -= OnTheme; ThemeManager.Changed += OnTheme; OnTheme(); };
        Unloaded += (_, _) => ThemeManager.Changed -= OnTheme;
    }

    /// <summary>暗稿仍有三团低饱和色晕（同节点改色后重导出），明暗各用一套 PNG。</summary>
    private void OnTheme()
    {
        if (GlowTop is null) return;
        bool dark = ThemeManager.IsDark;
        GlowTop.Source = dark ? "atmos_topleft_dark.png" : "atmos_topleft.png";
        GlowRight.Source = dark ? "atmos_right_dark.png" : "atmos_right.png";
        GlowBottom.Source = dark ? "atmos_bottom_dark.png" : "atmos_bottom.png";
    }
}
