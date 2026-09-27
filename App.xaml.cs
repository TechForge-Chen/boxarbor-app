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

            // 设计稿只提供亮色画板，交接文档已注明「深色未交付」。
            // 强制亮色可避免 Styles.xaml 的 AppThemeBinding 把标签字色翻成白色。
            UserAppTheme = AppTheme.Light;

            MainPage = new SplashPage();   // 开屏页 -> AppShell
        }
    }
}
