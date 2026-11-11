using Microsoft.Maui.Controls;

namespace BluetoothMauiApp;

/// <summary>Rename 共享弹窗（Figma 78:339）。整块含遮罩，IsVisible 控制显隐。</summary>
public partial class RenameDialog : ContentView
{
    /// <summary>点 Save：回传去空格后的名称。</summary>
    public event Action<string>? Confirmed;

    /// <summary>点 Cancel 或点遮罩：关闭且不改名。</summary>
    public event Action? Cancelled;

    public RenameDialog()
    {
        InitializeComponent();
    }

    /// <summary>打开弹窗并预填当前名称。</summary>
    public void Open(string initialName, string? subtitle = null)
    {
        NameEntry.Text = initialName;
        if (!string.IsNullOrEmpty(subtitle))
            SubtitleLabel.Text = subtitle;
        IsVisible = true;
        ThemeManager.SetOverlayDim(true, Shell.Current?.CurrentPage is { } pg ? SafeArea.Insets(pg).Top : 0);
        NameEntry.Focus();
    }

    public void Close()
    {
        IsVisible = false;
        ThemeManager.SetOverlayDim(false);
        NameEntry.Unfocus();
    }

    private void OnBackdropTapped(object sender, TappedEventArgs e) => CancelAndClose();

    private void OnCancelTapped(object sender, TappedEventArgs e) => CancelAndClose();

    private void OnEntryCompleted(object? sender, EventArgs e) => SaveAndClose();

    private void OnSaveTapped(object sender, TappedEventArgs e) => SaveAndClose();

    private void CancelAndClose()
    {
        Close();
        Cancelled?.Invoke();
    }

    private void SaveAndClose()
    {
        var name = (NameEntry.Text ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            // 设计交接约定：空名称不允许保存，弹窗保持打开
            NameEntry.Focus();
            return;
        }
        Close();
        Confirmed?.Invoke(name);
    }
}
