using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinPlus.Models;
using WinPlus.Services;

namespace WinPlus.Views.Pages;

public sealed partial class ShortcutsPage : Page
{
    private ShortcutBinding? _editing;

    public ShortcutsPage()
    {
        InitializeComponent();
        Title.Text = S.T("Short_Title");
        Explainer.Text = S.T("Short_Explainer");
        EditName.Header = S.T("Short_Name");
        EditHotkey.Header = S.T("Short_Hotkey");
        EditActionType.Header = S.T("Short_Action");
        EditPayload.Header = S.T("Short_Content");
        HotkeyCapture.Attach(EditHotkey);
        EditEnabled.Content = S.T("Short_Enabled");
        NewButton.Content = S.T("Common_New");
        SaveButton.Content = S.T("Common_Save");
        DeleteButton.Content = S.T("Common_Delete");
        foreach (var preset in ActionOptions.All)
            EditActionType.Items.Add(new ComboBoxItem { Tag = preset, Content = preset.Label });
        EditActionType.SelectedIndex = 0;
        RefreshList();
    }

    private void RefreshList()
    {
        ShortcutList.ItemsSource = null;
        ShortcutList.ItemsSource = AppState.Current.Config.Shortcuts;
    }

    private void ShortcutList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _editing = ShortcutList.SelectedItem as ShortcutBinding;
        if (_editing is null) return;
        EditName.Text = _editing.Name;
        EditHotkey.Text = _editing.Hotkey;
        EditEnabled.IsChecked = _editing.Enabled;
        EditPayload.Text = _editing.Action.Payload;
        foreach (ComboBoxItem item in EditActionType.Items)
        {
            if (item.Tag is not ActionPreset p) continue;
            var built = ActionOptions.BuildPreset(p, "");
            if (!p.Fixed && p.Type == _editing.Action.Type) { EditActionType.SelectedItem = item; break; }
            if (p.Fixed && built.Type == _editing.Action.Type && built.Payload == _editing.Action.Payload) { EditActionType.SelectedItem = item; break; }
        }
        RefreshRegInfo();
    }

    private void RefreshRegInfo()
    {
        var entries = AppState.Current.Hotkeys.Entries.Values
            .Where(x => string.Equals(x.Spec, EditHotkey.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        RegInfo.Text = entries.Count == 0 ? S.T("Short_RegNone") :
            string.Join(", ", entries.Select(x => x.RegisteredByOs ? S.T("Short_RegOs") : S.T("Short_RegHook")));
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _editing = null;
        ShortcutList.SelectedItem = null;
        EditName.Text = S.T("Default_ShortcutName");
        EditHotkey.Text = "Win+U";
        EditActionType.SelectedIndex = 0;
        EditPayload.Text = "";
        EditEnabled.IsChecked = true;
        RegInfo.Text = "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!HotkeySpec.TryParse(EditHotkey.Text.Trim(), out _, out _))
        {
            Status.Text = S.T("Short_BadHotkey");
            return;
        }
        var dup = AppState.Current.Config.Shortcuts
            .Where(s => s != _editing && s.Enabled && string.Equals(s.Hotkey, EditHotkey.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var preset = ((ComboBoxItem?)EditActionType.SelectedItem)?.Tag as ActionPreset;
        if (_editing is null)
        {
            _editing = new ShortcutBinding { Name = S.T("Default_ShortcutName") };
            AppState.Current.Config.Shortcuts.Add(_editing);
        }
        _editing.Name = EditName.Text.Trim();
        _editing.Hotkey = EditHotkey.Text.Trim();
        _editing.Enabled = EditEnabled.IsChecked == true;
        _editing.Action = preset is null ? AppAction.None() : ActionOptions.BuildPreset(preset, EditPayload.Text);
        AppState.Current.Save();
        AppState.Current.RebuildHotkeys();
        RefreshList();
        RefreshRegInfo();
        Status.Text = dup.Count > 0 ? S.T("Short_DupWarn", dup.Count) : S.T("Common_SavedAt", DateTime.Now.ToString("T"));
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is not null)
        {
            AppState.Current.Config.Shortcuts.Remove(_editing);
            _editing = null;
            AppState.Current.Save();
            AppState.Current.RebuildHotkeys();
            RefreshList();
        }
    }
}
