namespace BluetoothMauiApp;

/// <summary>
/// 新手引导流程状态（Figma 演示模式：View guide → Onboarding 1→2→3→4 → HOME，均为 DISSOLVE）。
/// 各页面的 CoachMark 订阅 <see cref="Changed"/> 决定自己显不显示；
/// Step=0 表示引导未在进行。
/// </summary>
public static class Onboarding
{
    public const int Off = 0;

    public static int Step { get; private set; } = Off;

    public static event Action? Changed;

    /// <summary>View guide 入口 → 显示第 1 步。</summary>
    public static void Start()
    {
        if (Step != Off) return;
        Step = 1;
        SetGuideBars(true);
        Changed?.Invoke();
    }

    /// <summary>Next → 进入下一步（>4 时自动结束）。</summary>
    public static void Next()
    {
        if (Step == Off) return;
        Step++;
        if (Step > 4) Finish();
        else Changed?.Invoke();
    }

    /// <summary>Skip / Start using → 结束引导。</summary>
    public static void Finish()
    {
        if (Step == Off) return;
        Step = Off;
        SetGuideBars(false);
        Changed?.Invoke();
    }

#if ANDROID
    /// <summary>设计稿蒙层盖到屏幕顶：引导期间隐藏系统状态栏/导航栏（全屏沉浸，
    /// 演示图自带深色状态栏带顶到屏幕顶），结束恢复。</summary>
    private static void SetGuideBars(bool dim)
    {
        try
        {
            var window = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
            if (window is null) return;
            var ctrl = window.DecorView.WindowInsetsController ?? window.InsetsController;
            if (ctrl is null) return;

            int bars = (int)(Android.Views.WindowInsets.Type.StatusBars() |
                             Android.Views.WindowInsets.Type.NavigationBars());
            if (dim)
            {
                ctrl.Hide(bars);
                ctrl.SetSystemBarsAppearance(0,
                    (int)(Android.Views.WindowInsetsControllerAppearance.LightStatusBars |
                          Android.Views.WindowInsetsControllerAppearance.LightNavigationBars));
            }
            else
            {
                ctrl.Show(bars);
                ctrl.SetSystemBarsAppearance(
                    (int)(Android.Views.WindowInsetsControllerAppearance.LightStatusBars |
                          Android.Views.WindowInsetsControllerAppearance.LightNavigationBars),
                    (int)(Android.Views.WindowInsetsControllerAppearance.LightStatusBars |
                          Android.Views.WindowInsetsControllerAppearance.LightNavigationBars));
            }
        }
        catch (Exception ex)
        {
        }
    }
#else
    private static void SetGuideBars(bool dim) { }
#endif
}
