#if IOS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
#endif

namespace BluetoothMauiApp;

/// <summary>
/// 安全区高度读取：iOS 取系统安全区；Android 返回 0（调用方用固定外扩值覆盖刘海/导航栏）。
/// 页面使用系统安全区（内容自动内缩，窗口与页面底色跟主题走保证无断层），
/// 全屏遮罩（蓝牙弹窗/Save/Rename）用负 Margin 外扩以铺满状态栏与 Home/导航区；
/// 外扩必须在 SizeChanged 后再做一次——OnAppearing 时可能还没排版取到 0。
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
