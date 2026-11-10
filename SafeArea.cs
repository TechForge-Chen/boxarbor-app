#if IOS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
#endif

namespace BluetoothMauiApp;

/// <summary>
/// iOS 全屏布局辅助：页面使用 <c>ios:Page.UseSafeArea="False"</c> 让背景/遮罩铺满全屏后，
/// 由各页面把安全区高度补回内容容器，避免标题顶到状态栏、内容压到 Home Indicator。
/// 其他平台返回 0，不影响 Android。
/// </summary>
internal static class SafeArea
{
    public static Thickness Insets(Microsoft.Maui.Controls.Page page)
    {
#if IOS
        try
        {
            return page.On<iOS>().SafeAreaInsets();
        }
        catch
        {
            return Thickness.Zero;
        }
#else
        return Thickness.Zero;
#endif
    }
}
