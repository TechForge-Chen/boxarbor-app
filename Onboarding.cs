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
        Console.WriteLine($"[Onb] Start called, current Step={Step}");
        if (Step != Off) return;
        Step = 1;
        Changed?.Invoke();
        Console.WriteLine($"[Onb] -> Step={Step}");
    }

    /// <summary>Next → 进入下一步（>4 时自动结束）。</summary>
    public static void Next()
    {
        Console.WriteLine($"[Onb] Next called, current Step={Step}");
        if (Step == Off) return;
        Step++;
        if (Step > 4) Finish();
        else Changed?.Invoke();
    }

    /// <summary>Skip / Start using → 结束引导。</summary>
    public static void Finish()
    {
        Console.WriteLine($"[Onb] Finish called, current Step={Step}");
        if (Step == Off) return;
        Step = Off;
        Changed?.Invoke();
        Console.WriteLine($"[Onb] -> Step={Step}");
    }
}
