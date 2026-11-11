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

    /// <summary>
    /// 自绘底栏要占的底部边距。
    /// iOS：把根视图控制器的底部安全区去掉（AdditionalSafeAreaInsets 取负值），
    /// 页面内容就能铺到屏幕底边，105 高的底栏直接坐到底；去掉后返回 0。
    /// 去掉失败时退回安全区高度（外观与之前一致，不会更糟）。
    /// Android：返回 0，导航栏由系统栏着色处理。
    /// </summary>
    public static double TabBarBottomInset(Microsoft.Maui.Controls.Page page)
    {
        double bottom = System.Math.Max(0, Insets(page).Bottom);
        return ResolveBottom(bottom);
    }

    private static double ResolveBottom(double bottom)
    {
#if IOS
        if (bottom <= 0)
            return 0;
        if (_bottomRemoved)
            return 0;
        try
        {
            // 负的 AdditionalSafeAreaInsets 会把安全区缩掉：底部 34 变成 0
            var root = UIKit.UIApplication.SharedApplication.KeyWindow?.RootViewController;
            if (root is not null)
            {
                root.AdditionalSafeAreaInsets = new UIKit.UIEdgeInsets(0, 0, (nfloat)(-bottom), 0);
                _bottomRemoved = true;
                return 0;
            }
        }
        catch
        {
            // 拿不到根控制器就退回原值
        }
        return bottom;
#else
        return bottom;
#endif
    }

#if IOS
    private static bool _bottomRemoved;
#endif
}
