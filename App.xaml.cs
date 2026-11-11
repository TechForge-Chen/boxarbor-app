using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace BluetoothMauiApp
{

    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // AppTheme 固定 Light：暗色由 ThemeManager 按 Figma token 整体换 Dg* 资源实现，
            // 不走系统主题（避免 Styles.xaml 的 AppThemeBinding 把标签字色翻成白色）。
            UserAppTheme = AppTheme.Light;
            ThemeManager.Initialize();     // 按保存的偏好套用明/暗

            MainPage = new AppShell();   // 只留平台启动屏，无应用内开屏页
            ThemeManager.ApplyWindowBackground();  // 窗口底色跟主题走，系统留白区不再是白色
        }
    }
}
