using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace BluetoothMauiApp;

/// <summary>
/// 自绘底部导航（设计稿 Frame 52）。Shell 内建 TabBar 给不了 79dp 条高、
/// 中央挖孔和凸起 58dp FAB，所以各页面都设了 Shell.TabBarIsVisible="False"，
/// 改由本控件驱动 AppShell.SelectTab。
/// </summary>
public partial class AppTabBar : ContentView
{
    public const string DevicesRoute = "devtab";
    public const string HomeRoute = "hometab";
    public const string SettingsRoute = "settab";

    public static readonly Color Selected = Color.FromHex("#16c95c");

    /// <summary>选中态绿：亮态 #16c95c，暗稿标签 #62EBA0、图标 #43DC89。</summary>
    private static Color SelectedColor => ThemeManager.IsDark ? Color.FromHex("#62EBA0") : Selected;

    public AppTabBar()
    {
        InitializeComponent();
        ThemeManager.BindIcon(BarBg, "tabbar_bg.png", "tabbar_bg_dark.png");
        ThemeManager.BindIcon(FabImg, "tab_home_fab.png", "tab_home_fab_dark.png");
        Loaded += (_, _) => { ThemeManager.Changed -= OnTheme; ThemeManager.Changed += OnTheme; OnTheme(); };
        Unloaded += (_, _) => ThemeManager.Changed -= OnTheme;
        // 选中态从 Shell 派生：每页各有一个 TabBar 实例，实例各自记状态会在跨页导航后不同步
        if (Shell.Current != null)
            Shell.Current.Navigated += OnShellNavigated;
        ApplyState();
    }

    /// <summary>第 1 步引导要高亮的目标（底部 Devices 点击区）。</summary>
    public VisualElement? DevicesTarget => DevicesStack;

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(ApplyState);

    private void OnDevicesTapped(object sender, TappedEventArgs e) => Go(DevicesRoute);
    private void OnHomeTapped(object sender, TappedEventArgs e) => Go(HomeRoute);
    private void OnSettingsTapped(object sender, TappedEventArgs e) => Go(SettingsRoute);

    private void Go(string route)
    {
        if (Shell.Current is AppShell shell)
            shell.SelectTab(route);
        ApplyState();
        StartPage.TapHaptic();
    }

    private void OnTheme() => ApplyState();

    private void ApplyState()
    {
        string current = Shell.Current?.CurrentItem?.CurrentItem?.Route ?? HomeRoute;
        bool dev = current == DevicesRoute;
        bool set = current == SettingsRoute;
        bool dark = ThemeManager.IsDark;

        string grayDev = dark ? "tab_devices_gray_w.png" : "tab_devices_gray.png";
        string graySet = dark ? "tab_settings_gray_w.png" : "tab_settings_gray.png";
        string grnDev = dark ? "tab_devices_green_dark.png" : "tab_devices_green.png";
        string grnSet = dark ? "tab_settings_green_dark.png" : "tab_settings_green.png";

        IconDevices.Source = dev ? grnDev : grayDev;
        LabelDevices.TextColor = dev ? SelectedColor : ThemeManager.Color("DgMuted");

        IconSettings.Source = set ? grnSet : graySet;
        LabelSettings.TextColor = set ? SelectedColor : ThemeManager.Color("DgMuted");

        // 底部安全区填充色与导航条底色一致（亮 #FFFFFF / 暗 #21372B）
        SafeAreaFill.Color = dark ? Color.FromHex("#21372B") : Colors.White;
    }

    /// <summary>
    /// 页面使用全屏布局后调用：导航条主体保持 105 高，底部安全区（Home Indicator）
    /// 由填充色延伸铺满，避免底部出现断层。
    /// </summary>
    public void ApplySafeAreaInsets(Thickness insets)
    {
        double bottom = Math.Max(0, insets.Bottom);
        HeightRequest = 105 + bottom;
        SafeAreaFill.IsVisible = bottom > 0;
        SafeAreaFill.Color = ThemeManager.IsDark ? Color.FromHex("#21372B") : Colors.White;
    }
}
