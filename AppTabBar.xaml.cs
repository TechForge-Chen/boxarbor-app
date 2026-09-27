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

    private static readonly Color Selected = Color.FromHex("#16c95c");
    private static readonly Color Unselected = Color.FromHex("#8b938e");

    public AppTabBar()
    {
        InitializeComponent();
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

    private void ApplyState()
    {
        string current = Shell.Current?.CurrentItem?.CurrentItem?.Route ?? HomeRoute;
        bool dev = current == DevicesRoute;
        bool set = current == SettingsRoute;

        IconDevices.Source = dev ? "tab_devices_green.png" : "tab_devices_gray.png";
        LabelDevices.TextColor = dev ? Selected : Unselected;

        IconSettings.Source = set ? "tab_settings_green.png" : "tab_settings_gray.png";
        LabelSettings.TextColor = set ? Selected : Unselected;
    }
}
