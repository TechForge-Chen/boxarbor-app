#if IOS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
#endif

namespace BluetoothMauiApp;

/// <summary>
/// 安全区处理：iOS 取系统安全区；Android 返回 0（状态栏透明 + 导航栏同色已处理）。
/// iOS 只移除底部安全区（AdditionalSafeAreaInsets 负值）——上下同时取负 UIKit 会整块忽略，
/// 单取底部实测有效：页面铺到屏幕底边，105 高的自绘底栏直接坐到底。
/// 顶部保持系统安全区，状态栏区域由页面自身背景填充，视觉无断层。
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

    /// <summary>MAUI 把系统安全区当作内边距加在页面根布局上，
    /// 导致遮罩层够不到最上面那一条（约 24pt）。这里把根布局内边距清零，
    /// 由页面自己用 Insets 补内容间距。</summary>
    public static void ClearRootPadding(Microsoft.Maui.Controls.Page page)
    {
        try
        {
            if (page.Content is Microsoft.Maui.Controls.Layout layout)
                layout.Padding = Microsoft.Maui.Thickness.Zero;
        }
        catch
        {
        }
    }

    /// <summary>自绘底栏要占的底部边距：移除底部安全区后返回 0，
    /// 移除失败则退回原始高度（外观不比现在差）。</summary>
    public static double TabBarBottomInset(Microsoft.Maui.Controls.Page page)
    {
        double bottom = System.Math.Max(0, Insets(page).Bottom);
#if IOS
        if (bottom <= 0)
            return 0;
        if (_removed)
            return 0;
        try
        {
            var root = UIKit.UIApplication.SharedApplication.KeyWindow?.RootViewController;
            if (root is not null)
            {
                root.AdditionalSafeAreaInsets = new UIKit.UIEdgeInsets(0, 0, (nfloat)(-bottom), 0);
                _removed = true;
                return 0;
            }
        }
        catch
        {
            // 拿不到根控制器就退回原值
        }
#endif
        return bottom;
    }

#if IOS
    private static bool _removed;
#endif
}
