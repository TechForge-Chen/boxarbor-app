using System.Linq;
using Microsoft.Maui.Controls;

namespace BluetoothMauiApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // 注册Tab对应的页面路由 键名自定义
            Routing.RegisterRoute("StartPage", typeof(StartPage));
            Routing.RegisterRoute("DataPage", typeof(DataPage));
            Routing.RegisterRoute("SetPage", typeof(SetPage));

            // 图标不做着色（Shell 只对 FontImageSource 着色），
            // 所以准备了 gray / green 两套矢量，切页时手动换。
            Navigated += OnShellNavigated;

            // 交接文档要求：初始化完成后停在 HOME。
            // 视觉顺序仍按设计稿 Devices | Home | Settings，这里把初始 Tab 挪到 Home。
            SelectTab("hometab");
            RefreshTabIcons();
        }

        private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
            => RefreshTabIcons();

        /// <summary>切到指定 Route 的 Tab。找不到时静默返回，不影响启动。
        /// 由自绘的 AppTabBar 调用（各页面已把 Shell.TabBarIsVisible 设为 False）。</summary>
        public void SelectTab(string route)
        {
            foreach (var shellItem in Items)
            {
                if (shellItem is not TabBar tabBar)
                    continue;
                foreach (var tab in tabBar.Items)
                {
                    if (tab.Route == route)
                    {
                        tabBar.CurrentItem = tab;
                        return;
                    }
                }
            }
        }

        private void RefreshTabIcons()
        {
            foreach (var shellItem in Items)
            {
                if (shellItem is not TabBar tabBar)
                    continue;

                foreach (var tab in tabBar.Items)
                {
                    // Home 中心按钮恒为绿色，不随选中态变化
                    if (tab.Route == "hometab")
                        continue;

                    bool selected = ReferenceEquals(tabBar.CurrentItem, tab);
                    string file = tab.Route switch
                    {
                        "devtab" => selected ? "tab_devices_green.png" : "tab_devices_gray.png",
                        "settab" => selected ? "tab_settings_green.png" : "tab_settings_gray.png",
                        _ => ""
                    };
                    if (file.Length > 0)
                        tab.Icon = ImageSource.FromFile(file);
                }
            }
        }
    }
}
