using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinPlus.Services;

namespace WinPlus.Views.Pages;

public sealed partial class GeneralPage : Page
{
    private static readonly (string Value, string Key)[] Languages =
    {
        ("System", "Lang_System"),
        ("en", "Lang_En"),
        ("ja", "Lang_Ja"),
        ("zh-Hant-TW", "Lang_ZhTw"),
        ("pt", "Lang_Pt"),
        ("fr", "Lang_Fr"),
        ("es", "Lang_Es"),
        ("ru", "Lang_Ru"),
    };

    private bool _ready;

    public GeneralPage()
    {
        InitializeComponent();
        Title.Text = S.T("General_Title");
        StartAtLogon.Content = S.T("General_StartAtLogon");
        MinimizeToTray.Content = S.T("General_MinimizeToTray");
        ThemeLabel.Text = S.T("General_Theme");
        LanguageLabel.Text = S.T("General_Language");
        SaveButton.Content = S.T("Common_SaveNow");
        OpenFolderButton.Content = S.T("Common_OpenFolder");
        ExportButton.Content = S.T("Backup_Export");
        ImportButton.Content = S.T("Backup_Import");
        Note.Text = S.T("General_Note");
        UninstallTitle.Text = S.T("Uninstall_Title");
        UninstallDesc.Text = S.T("Uninstall_Desc");
        DeleteData.Content = S.T("Uninstall_DeleteData");
        DeleteShots.Content = S.T("Uninstall_DeleteShots");
        UninstallButton.Content = S.T("Uninstall_Run");
        CertNote.Text = S.T("Uninstall_CertNote");
        AboutTitle.Text = S.T("General_About");
        VersionRun.Text = S.T("General_Version");
        DeveloperRun.Text = S.T("General_Developer");
        LicenseRun.Text = S.T("General_License");

        var themeItems = ThemeBox.Items;
        if (themeItems[0] is ComboBoxItem t0) t0.Content = S.T("Theme_System");
        if (themeItems[1] is ComboBoxItem t1) t1.Content = S.T("Theme_Dark");
        if (themeItems[2] is ComboBoxItem t2) t2.Content = S.T("Theme_Light");

        LanguageBox.Items.Clear();
        foreach (var (value, key) in Languages)
            LanguageBox.Items.Add(new ComboBoxItem { Tag = value, Content = S.T(key) });

        var cfg = AppState.Current.Config;
        StartAtLogon.IsChecked = cfg.StartAtLogon;
        MinimizeToTray.IsChecked = cfg.MinimizeToTray;
        foreach (ComboBoxItem item in ThemeBox.Items)
        {
            if ((string)item.Tag == cfg.Memo.Theme) { ThemeBox.SelectedItem = item; break; }
        }
        if (ThemeBox.SelectedItem is null && ThemeBox.Items.Count > 0) ThemeBox.SelectedIndex = 0;
        foreach (ComboBoxItem item in LanguageBox.Items)
        {
            if (string.Equals((string)item.Tag, cfg.Language, StringComparison.OrdinalIgnoreCase)) { LanguageBox.SelectedItem = item; break; }
        }
        if (LanguageBox.SelectedItem is null && LanguageBox.Items.Count > 0) LanguageBox.SelectedIndex = 0;
        StartAtLogon.Checked += (_, _) => Save();
        StartAtLogon.Unchecked += (_, _) => Save();
        MinimizeToTray.Checked += (_, _) => Save();
        MinimizeToTray.Unchecked += (_, _) => Save();
        ThemeBox.SelectionChanged += (_, _) => Save();
        LanguageBox.SelectionChanged += (_, _) => OnLanguageChanged();
        _ready = true;
    }

    private void Save()
    {
        if (!_ready) return;
        var cfg = AppState.Current.Config;
        cfg.StartAtLogon = StartAtLogon.IsChecked == true;
        cfg.MinimizeToTray = MinimizeToTray.IsChecked == true;
        if (ThemeBox.SelectedItem is ComboBoxItem item) cfg.Memo.Theme = (string)item.Tag;
        AppState.Current.Save();
        _ = StartupManager.ApplyAsync(cfg.StartAtLogon);
        App.ApplyTheme();
        Status.Text = S.T("Common_SavedAt", DateTime.Now.ToString("T"));
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private async void OnLanguageChanged()
    {
        if (!_ready || LanguageBox.SelectedItem is not ComboBoxItem item) return;
        var value = (string)item.Tag;
        var cfg = AppState.Current.Config;
        if (string.Equals(cfg.Language, value, StringComparison.OrdinalIgnoreCase)) return;
        cfg.Language = value;
        AppState.Current.Save();
        var dialog = new ContentDialog
        {
            Title = S.T("General_RestartTitle"),
            Content = S.T("General_RestartMsg"),
            PrimaryButtonText = S.T("General_RestartNow"),
            CloseButtonText = S.T("General_RestartLater"),
            XamlRoot = Content.XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try { Microsoft.Windows.AppLifecycle.AppInstance.Restart(""); }
            catch { }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo { FileName = ConfigStore.Dir, UseShellExecute = true }); }
        catch { }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            picker.SuggestedFileName = $"win-plus-backup-{DateTime.Now:yyyyMMdd_HHmmss}";
            picker.FileTypeChoices.Add("ZIP", new System.Collections.Generic.List<string> { ".zip" });
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            AppState.Current.Save();
            AppState.Current.Memo.Save();
            BackupService.CreateZip(file.Path);
            Status.Text = S.T("Backup_Exported", file.Path);
        }
        catch (Exception ex) { Status.Text = S.T("Backup_BadFile") + " " + ex.Message; }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            picker.FileTypeFilter.Add(".zip");
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var dialog = new ContentDialog
            {
                Title = S.T("Backup_ConfirmTitle"),
                Content = S.T("Backup_ConfirmMsg"),
                PrimaryButtonText = S.T("Backup_Import"),
                CloseButtonText = S.T("Common_Cancel"),
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            BackupService.RestoreZip(file.Path);
            AppState.Current.ConfigStore.Load();
            AppState.Current.Memo.Load();
            AppState.Current.RebuildHotkeys();
            App.ApplyTheme();
            Status.Text = S.T("Backup_Imported", file.Path);
            var restart = new ContentDialog
            {
                Title = S.T("General_RestartTitle"),
                Content = S.T("General_RestartMsg"),
                PrimaryButtonText = S.T("General_RestartNow"),
                CloseButtonText = S.T("General_RestartLater"),
                XamlRoot = Content.XamlRoot,
            };
            if (await restart.ShowAsync() == ContentDialogResult.Primary)
            {
                try { Microsoft.Windows.AppLifecycle.AppInstance.Restart(""); }
                catch { }
            }
        }
        catch (Exception ex) { Status.Text = S.T("Backup_BadFile") + " " + ex.Message; }
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = S.T("Uninstall_ConfirmTitle"),
            Content = S.T("Uninstall_ConfirmMsg"),
            PrimaryButtonText = S.T("Uninstall_Run"),
            CloseButtonText = S.T("Common_Cancel"),
            XamlRoot = Content.XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        bool packaged = UninstallService.IsPackaged();
        UninstallService.Cleanup(DeleteData.IsChecked == true, DeleteShots.IsChecked == true);
        if (packaged)
        {
            AppState.Current.RealExitRequested = true;
            UninstallService.RemoveOwnPackage();
            Environment.Exit(0);
        }
        else
        {
            Status.Text = S.T("Uninstall_PortableMsg");
        }
    }
}
