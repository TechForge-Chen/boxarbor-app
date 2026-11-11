#if IOS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
#endif

namespace BluetoothMauiApp;

/// <summary>
/// iOS 全屏布局：把根视图控制器的上下安全区去掉（AdditionalSafeAreaInsets 取负值），
/// 页面就能铺满整屏——底栏 105 直接坐到底边、弹窗遮罩天然盖住状态栏与 Home 区。
/// 去掉后由各页面把原始安全区高度补回内容内边距，内容视觉位置不变。
/// Android 无需处理（状态栏透明 + 导航栏同色），全部返回 0。
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

    /// <summary>
    /// 让当前页面全屏：第一次调用时读原始安全区并从根控制器移除，
    /// 返回原始安全区高度供页面把内容内缩补回来（之后每次返回同一值）。
    /// 移除失败时返回原值且页面保持系统安全区，外观不会变差。
    /// </summary>
    public static Thickness EnsureFullScreen(Microsoft.Maui.Controls.Page page)
    {
#if IOS
        if (!_removed)
        {
            var insets = Insets(page);
            _top = System.Math.Max(0, insets.Top);
            _bottom = System.Math.Max(0, insets.Bottom);
            try
            {
                var root = UIKit.UIApplication.SharedApplication.KeyWindow?.RootViewController;
                if (root is not null)
                {
                    root.AdditionalSafeAreaInsets = new UIKit.UIEdgeInsets(
                        (nfloat)(-_top), 0, (nfloat)(-_bottom), 0);
                    _removed = true;
                }
            }
            catch
            {
                // 拿不到根控制器就保持系统安全区
            }
        }
        return _removed ? new Thickness(0, _top, 0, _bottom) : Thickness.Zero;
#else
        return Thickness.Zero;
#endif
    }

#if IOS
    private static bool _removed;
    private static double _top;
    private static double _bottom;
#endif
}
