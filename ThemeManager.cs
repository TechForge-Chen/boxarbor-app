using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace BluetoothMauiApp;

/// <summary>
/// 明暗主题切换。暗色值来自 Value.tokens.json 成对 token 与
/// 2026-09-30 改暗后的 0922主流程画板（0D1713 页底 / 21372B 卡 / 43DC89 亮绿等）；
/// 通过把 Dg* 资源换成 DynamicResource 引用的同名键实现整页换色。
/// </summary>
public static class ThemeManager
{
    public const string PrefKey = "dark_mode";

    public static bool IsDark { get; private set; }

    public static event Action? Changed;

    private static readonly Dictionary<string, Color> Light = new();
    private static bool _inited;

    private static readonly Dictionary<string, string> Dark = new()
    {
        // 页面/卡片（成对 token371/372、383/384 + 暗稿）
        ["DgPageBg"] = "#0D1713",
        ["DgAtmosBase"] = "#0D1713",
        ["DgCard"] = "#21372B",
        ["DgCardSelected"] = "#21372B",
        ["DgSceneCard"] = "#C221372B",   // 未选中场景卡 76%（暗稿）
        ["DgSceneCardStroke"] = "#29A2D4B8",
        ["DgIconBg"] = "#21372B",
        ["DgIconBgAlt"] = "#21372B",
        ["DgStatusCard"] = "#9921372B",  // 网关状态卡 60%（暗稿）
        ["DgGridCard"] = "#B821372B",    // 网关网格卡 72%（暗稿）
        ["DgIllustrationCard"] = "#1D3228",

        // 绿色族（暗稿：面→#087D40，亮强调→#43DC89，文字→#62EBA0）
        ["DgGreen"] = "#087D40",
        ["DgGreenBtn"] = "#087D40",
        ["DgGreenScan"] = "#087D40",
        ["DgGreenRing"] = "#43DC89",
        ["DgGreenLink"] = "#43DC89",
        ["DgGreenText"] = "#62EBA0",
        ["DgGreenText2"] = "#62EBA0",
        ["DgGreenText3"] = "#62EBA0",
        ["DgGreenFaint"] = "#D921372B",
        ["DgGreenBright"] = "#43DC89",
        ["DgGreenAccent"] = "#43DC89",

        // 文字（暗稿：标题族→#F1F7F3，次级→浅灰绿）
        ["DgTitle"] = "#F1F7F3",
        ["DgTitleAlt"] = "#F1F7F3",
        ["DgSceneTitle"] = "#F1F7F3",
        ["DgSub"] = "#B2C1B8",
        ["DgSub2"] = "#B2C1B8",
        ["DgSub3"] = "#B2C1B8",
        ["DgSub4"] = "#B2C1B8",
        ["DgMuted"] = "#B2C1B8",
        ["DgLabel"] = "#B2C1B8",
        ["DgLabel2"] = "#B2C1B8",
        ["DgNum"] = "#B2C1B8",

        // 状态
        ["DgOffline"] = "#FF9A89",
        ["DgOfflineBg"] = "#21372B",

        // 轨道/分隔/开关
        ["DgTrack"] = "#21372B",
        ["DgTrackRing"] = "#29A2D4B8",
        ["DgDivider"] = "#21372B",
        ["DgDivider2"] = "#B33B5245",
        ["DgSwitchOff"] = "#3B5245",

        // 描边/字段
        ["DgEdge"] = "#29A2D4B8",
        ["DgGreenBtnEdge"] = "#087D40",
        ["DgField"] = "#21372B",
        ["DgSaveOpt"] = "#D921372B",
        ["DgSaveDotRing"] = "#547363",

        // 灯控页左轨（暗稿：列底≈页色，芯片 #304439、描边 #1C3025）
        ["DgRailColumn"] = "#0E1D17",
        ["DgRailChip"] = "#304439",
        ["DgRailChipEdge"] = "#1C3025",

        // 弹窗（token519/520、524/525、675/676、678/679 成对）
        ["DgDialogBg"] = "#1B2C23",
        ["DgDialogBg2"] = "#F51B2C23",
        ["DgDialogBorder"] = "#3B5245",
        ["DgDialogText"] = "#F1F7F3",
        ["DgPickerBg"] = "#E81D3026",
        ["DgPickerTitle"] = "#F1F7F3",
        ["DgPickerSub"] = "#B2C1B8",
        ["DgPickerCancelBg"] = "#21372B",
        ["DgPickerCancelText"] = "#A9C7B5",
        ["DgBackdrop"] = "#020906",      // 半透明由父 Grid.Opacity 提供
        ["DgGuideChip"] = "#B821372B",

        // 氛围底（暗稿有三团低饱和色晕，PNG 见 atmos_*_dark）
        ["DgAtmosGreen"] = "#2916482F",
        ["DgAtmosBlue"] = "#6B203348",
        ["DgAtmosGreen2"] = "#6B16482F",
    };

    private static readonly List<(Image img, string light, string dark)> Icons = new();

#if ANDROID
    private static Android.Graphics.Color? _origStatus;
    private static Android.Graphics.Color? _origNav;
#endif

    /// <summary>启动时调用：套用已保存的偏好。</summary>
    public static void Initialize()
    {
        if (_inited)
            return;
        _inited = true;

        var res = Application.Current!.Resources;
        foreach (var key in Dark.Keys)
        {
            if (res.TryGetValue(key, out var v) && v is Color c)
                Light[key] = c;
        }
        Apply(Preferences.Get(PrefKey, false));
    }

    /// <summary>取当前主题下的 Dg* 颜色（代码里替代 Color.FromHex 字面量）。</summary>
    public static Color Color(string key)
    {
        var res = Application.Current?.Resources;
        if (res != null && res.TryGetValue(key, out var v) && v is Color c)
            return c;
        return Colors.Transparent;
    }

    public static void Apply(bool dark)
    {
        if (Application.Current is null)
            return;
        IsDark = dark;

        var res = Application.Current.Resources;
        foreach (var kv in Dark)
        {
            if (!Light.TryGetValue(kv.Key, out var light))
                continue;
            res[kv.Key] = dark ? ParseHex(kv.Value) : light;
        }

        foreach (var (img, light, dark2) in Icons)
            img.Source = dark ? dark2 : light;

        ApplySystemBars(dark);
        Preferences.Set(PrefKey, dark);
        Changed?.Invoke();
    }

    /// <summary>注册随主题换图的图标（深色版资源名）。</summary>
    public static void BindIcon(Image img, string light, string dark)
    {
        Icons.Add((img, light, dark));
        img.Source = IsDark ? dark : light;
    }

    /// <summary>#RRGGBB 或 #AARRGGBB（AA 在前）→ Color。</summary>
    private static Color ParseHex(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 8)
        {
            int a = Convert.ToInt32(h[..2], 16);
            int r = Convert.ToInt32(h[2..4], 16);
            int g = Convert.ToInt32(h[4..6], 16);
            int b = Convert.ToInt32(h[6..8], 16);
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
        int rr = Convert.ToInt32(h[..2], 16);
        int gg = Convert.ToInt32(h[2..4], 16);
        int bb = Convert.ToInt32(h[4..6], 16);
        return new Color(rr / 255f, gg / 255f, bb / 255f);
    }

    /// <summary>冷启动时 CurrentActivity 可能尚未就绪，首个页面出现后补调。</summary>
    public static void RefreshSystemBars() => ApplySystemBars(IsDark);

    private static void ApplySystemBars(bool dark)
    {
#if ANDROID
        try
        {
            var window = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
            if (window is null)
                return;

            if (_origStatus is null)
            {
                _origStatus = new Android.Graphics.Color(window.StatusBarColor);
                _origNav = new Android.Graphics.Color(window.NavigationBarColor);
            }

            int lightMask = (int)(Android.Views.WindowInsetsControllerAppearance.LightStatusBars |
                                  Android.Views.WindowInsetsControllerAppearance.LightNavigationBars);

            if (dark)
            {
                window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#0D1713"));
                window.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#0D1713"));
                window.InsetsController?.SetSystemBarsAppearance(0, lightMask);
            }
            else
            {
                if (_origStatus is { } s) window.SetStatusBarColor(s);
                if (_origNav is { } n) window.SetNavigationBarColor(n);
                window.InsetsController?.SetSystemBarsAppearance(lightMask, lightMask);
            }
        }
        catch
        {
            // 系统栏着色失败不影响主题
        }
#endif
    }
}
