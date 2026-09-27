using Microsoft.UI.Xaml.Controls;
using WinPlus.Services;
using WinPlus.Views.Pages;

namespace WinPlus;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        var items = Nav.MenuItems;
        if (items[0] is NavigationViewItem general) general.Content = S.T("Nav_General");
        if (items[1] is NavigationViewItem touchpad) touchpad.Content = S.T("Nav_Touchpad");
        if (items[2] is NavigationViewItem devices) devices.Content = S.T("Nav_Devices");
        if (items[3] is NavigationViewItem shortcuts) shortcuts.Content = S.T("Nav_Shortcuts");
        if (items[4] is NavigationViewItem shots) shots.Content = S.T("Nav_Screenshots");
        if (items[5] is NavigationViewItem memo) memo.Content = S.T("Nav_Memo");
        Nav.SelectedItem = Nav.MenuItems[0];
        ContentFrame.Navigate(typeof(GeneralPage));
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag) return;
        var type = tag switch
        {
            "touchpad" => typeof(TouchpadPage),
            "devices" => typeof(DevicesPage),
            "shortcuts" => typeof(ShortcutsPage),
            "shots" => typeof(ScreenshotsPage),
            "memo" => typeof(MemoSettingsPage),
            _ => typeof(GeneralPage),
        };
        ContentFrame.Navigate(type);
    }
}
