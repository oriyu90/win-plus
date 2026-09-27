using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinPlus.Services;

namespace WinPlus.Views.Pages;

public sealed partial class MemoSettingsPage : Page
{
    public MemoSettingsPage()
    {
        InitializeComponent();
        Title.Text = S.T("Nav_Memo");
        Explainer.Text = S.T("MemoSet_Explainer");
        EditHotkey.Header = S.T("MemoSet_Hotkey");
        HotkeyCapture.Attach(EditHotkey);
        EditSeconds.Header = S.T("MemoSet_Seconds");
        EditTheme.Header = S.T("General_Theme");
        SaveButton.Content = S.T("Common_Save");
        OpenButton.Content = S.T("MemoSet_Open");
        ClearButton.Content = S.T("MemoSet_ClearImages");
        ResetBoundsButton.Content = S.T("MemoSet_ResetBounds");
        var themes = EditTheme.Items;
        if (themes[0] is ComboBoxItem t0) t0.Content = S.T("Theme_System");
        if (themes[1] is ComboBoxItem t1) t1.Content = S.T("Theme_Dark");
        if (themes[2] is ComboBoxItem t2) t2.Content = S.T("Theme_Light");
        var m = AppState.Current.Config.Memo;
        EditHotkey.Text = m.Hotkey;
        EditSeconds.Value = m.AutoCloseSeconds;
        foreach (ComboBoxItem item in EditTheme.Items)
            if ((string)item.Tag == m.Theme) { EditTheme.SelectedItem = item; break; }
        if (EditTheme.SelectedItem is null && EditTheme.Items.Count > 0) EditTheme.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!HotkeySpec.TryParse(EditHotkey.Text.Trim(), out _, out _))
        {
            Status.Text = S.T("MemoSet_BadHotkey");
            return;
        }
        var m = AppState.Current.Config.Memo;
        m.Hotkey = EditHotkey.Text.Trim();
        m.AutoCloseSeconds = (int)Math.Clamp(EditSeconds.Value, 3, 60);
        if (EditTheme.SelectedItem is ComboBoxItem item) m.Theme = (string)item.Tag;
        AppState.Current.Save();
        AppState.Current.RebuildHotkeys();
        App.ApplyTheme();
        Status.Text = S.T("Common_SavedAt", DateTime.Now.ToString("T"));
    }

    private void Open_Click(object sender, RoutedEventArgs e) =>
        AppState.Current.Actions.Execute(Models.AppAction.Memo());

    private void ClearImages_Click(object sender, RoutedEventArgs e)
    {
        foreach (var img in AppState.Current.Memo.Data.Images.ToArray())
            AppState.Current.Memo.RemoveImage(img);
        Status.Text = S.T("MemoSet_ImagesCleared");
    }

    private void ResetBounds_Click(object sender, RoutedEventArgs e)
    {
        MemoWindow.ResetBounds();
        Status.Text = S.T("MemoSet_BoundsReset");
    }
}
