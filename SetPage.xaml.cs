using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

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
        swDarkMode.IsOn = Preferences.Get("dark_mode", false);

        swOfflineAlerts.Toggled += (_, v) => Preferences.Set("offline_alerts", v);
        swAutomationAlerts.Toggled += (_, v) => Preferences.Set("automation_alerts", v);
        swHapticFeedback.Toggled += (_, v) => Preferences.Set("haptic_feedback", v);
        // 深色画板未交付，只记住偏好；主题切换待完整深色页面后再启用
        swDarkMode.Toggled += (_, v) => Preferences.Set("dark_mode", v);
    }
}
