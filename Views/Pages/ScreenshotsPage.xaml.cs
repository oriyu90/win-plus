using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinPlus.Models;
using WinPlus.Services;

namespace WinPlus.Views.Pages;

public sealed partial class ScreenshotsPage : Page
{
    private ScreenshotProfile? _editing;

    public ScreenshotsPage()
    {
        InitializeComponent();
        Title.Text = S.T("Shots_Title");
        ProfilesTitle.Text = S.T("Shots_Profiles");
        EditName.Header = S.T("Shots_Name");
        EditHotkey.Header = S.T("Shots_Hotkey");
        HotkeyCapture.Attach(EditHotkey);
        EditScope.Header = S.T("Shots_Scope");
        EditFolder.Header = S.T("Shots_Folder");
        EditFormat.Header = S.T("Shots_Format");
        EditPreview.Header = S.T("Shots_Preview");
        EditCursor.Content = S.T("Shots_Cursor");
        NewButton.Content = S.T("Common_New");
        SaveButton.Content = S.T("Common_Save");
        DeleteButton.Content = S.T("Common_Delete");
        ShootButton.Content = S.T("Shots_Shoot");
        FolderButton.Content = S.T("Common_OpenFolder");
        var scopes = EditScope.Items;
        if (scopes[0] is ComboBoxItem s0) s0.Content = S.T("Shots_ScopeFull");
        if (scopes[1] is ComboBoxItem s1) s1.Content = S.T("Shots_ScopeActive");
        if (scopes[2] is ComboBoxItem s2) s2.Content = S.T("Shots_ScopeRegion");
        EditScope.SelectedIndex = 0;
        EditFormat.SelectedIndex = 0;
        RefreshList();
        if (AppState.Current.Config.ShotProfiles.Count > 0)
            ProfileList.SelectedIndex = 0;
    }

    private void RefreshList()
    {
        ProfileList.ItemsSource = null;
        ProfileList.ItemsSource = AppState.Current.Config.ShotProfiles;
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _editing = ProfileList.SelectedItem as ScreenshotProfile;
        if (_editing is null) return;
        EditName.Text = _editing.Name;
        EditHotkey.Text = _editing.Hotkey;
        EditFolder.Text = _editing.Folder;
        EditPreview.Value = _editing.PreviewSeconds;
        EditCursor.IsChecked = _editing.IncludeCursor;
        foreach (ComboBoxItem item in EditScope.Items)
            if ((string)item.Tag == _editing.Scope) { EditScope.SelectedItem = item; break; }
        foreach (ComboBoxItem item in EditFormat.Items)
            if ((string)item.Tag == _editing.Format) { EditFormat.SelectedItem = item; break; }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _editing = null;
        ProfileList.SelectedItem = null;
        EditName.Text = $"{S.T("Default_ProfileName")}";
        EditHotkey.Text = "PrintScreen";
        EditScope.SelectedIndex = 0;
        EditFormat.SelectedIndex = 0;
        EditFolder.Text = "";
        EditPreview.Value = 5;
        EditCursor.IsChecked = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is null)
        {
            _editing = new ScreenshotProfile { Name = S.T("Default_ProfileName") };
            AppState.Current.Config.ShotProfiles.Add(_editing);
        }
        _editing.Name = EditName.Text.Trim();
        _editing.Hotkey = EditHotkey.Text.Trim();
        _editing.Scope = ((ComboBoxItem?)EditScope.SelectedItem)?.Tag as string ?? "Full";
        _editing.Format = ((ComboBoxItem?)EditFormat.SelectedItem)?.Tag as string ?? "PNG";
        _editing.Folder = EditFolder.Text.Trim();
        _editing.PreviewSeconds = (int)EditPreview.Value;
        _editing.IncludeCursor = EditCursor.IsChecked == true;
        if (string.IsNullOrWhiteSpace(_editing.Folder))
            _editing.Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "win-plus", _editing.Name);
        AppState.Current.Save();
        AppState.Current.RebuildHotkeys();
        RefreshList();
        Status.Text = S.T("Common_SavedAt", DateTime.Now.ToString("T"));
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is not null && AppState.Current.Config.ShotProfiles.Count > 1)
        {
            AppState.Current.Config.ShotProfiles.Remove(_editing);
            _editing = null;
            AppState.Current.Save();
            AppState.Current.RebuildHotkeys();
            RefreshList();
        }
    }

    private void Shoot_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is not null)
            AppState.Current.Actions.Execute(AppAction.Screenshot(_editing.Name));
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = _editing?.Folder ?? EditFolder.Text;
        try
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
        }
        catch { }
    }
}
