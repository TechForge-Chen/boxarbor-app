using Microsoft.Maui.Controls;

namespace BluetoothMauiApp;

/// <summary>
/// 开屏页（Figma 67:1289）。交接文档约定：启动 → 展示 LOGO 与 slogan → 初始化完成进入 HOME。
/// 直接把 Application.MainPage 换成 AppShell，不走 Shell 路由，避免开屏页留在导航栈里。
/// </summary>
public partial class SplashPage : ContentPage
{
    /// <summary>开屏时长。交接文档把"开屏时长"列为待确认项，暂定 1.8 秒。</summary>
    private const int SplashDurationMs = 1800;

    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await Task.Delay(SplashDurationMs);

            if (Application.Current is App app)
                app.MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"开屏跳转失败: {ex.Message}");
        }
    }
}
