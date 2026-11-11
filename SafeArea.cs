#if IOS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
#endif

namespace BluetoothMauiApp;

/// <summary>
/// iOS 安全区高度读取：页面使用系统安全区（内容自动内缩，页面底色与氛围底同色保证无断层），
/// 全屏遮罩（蓝牙弹窗/引导/Rename）用负 Margin 向安全区外扩以铺满状态栏与 Home 区。
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
