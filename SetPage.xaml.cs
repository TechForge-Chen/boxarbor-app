using Microsoft.Maui.ApplicationModel;

namespace BluetoothMauiApp;

public partial class SetPage : ContentPage
{
    public SetPage()
    {
        InitializeComponent();
        lblVersion.Text = $"App version {AppInfo.VersionString}";

        // 默认值与设计稿一致：三个开、Dark mode 关
        swOfflineAlerts.IsOn = Preferences.Get("offline_alerts", true);
        swAutomationAlerts.IsOn = Preferences.Get("automation_alerts", true);
        swHapticFeedback.IsOn = Preferences.Get("haptic_feedback", true);
        swDarkMode.IsOn = ThemeManager.IsDark;

        swOfflineAlerts.Toggled += (_, v) => Preferences.Set("offline_alerts", v);
        swAutomationAlerts.Toggled += (_, v) => Preferences.Set("automation_alerts", v);
        swHapticFeedback.Toggled += (_, v) => Preferences.Set("haptic_feedback", v);
        swDarkMode.Toggled += (_, v) => ThemeManager.Apply(v);

        // 深色版图标（图标 PNG 为深色，深色卡片上换白版）
        ThemeManager.BindIcon(imgInfo, "ic_info.png", "ic_info_w.png");
        ThemeManager.BindIcon(imgChevron, "ic_chevron.png", "ic_chevron_w.png");
        // 行图标：暗色下圆底变深，图标换浅色版（设计稿 #A9C7B5）
        ThemeManager.BindIcon(imgBell, "ic_bell.png", "ic_bell_w.png");
        ThemeManager.BindIcon(imgClock, "ic_clock.png", "ic_clock_w.png");
        ThemeManager.BindIcon(imgHaptic, "ic_haptic.png", "ic_haptic_w.png");
        ThemeManager.BindIcon(imgMoon, "ic_moon.png", "ic_moon_w.png");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }
}
