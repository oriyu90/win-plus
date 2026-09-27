using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinPlus.Models;
using WinPlus.Services;

namespace WinPlus.Views.Pages;

public sealed partial class DevicesPage : Page
{
    private List<InputDeviceInfo> _devices = new();

    public DevicesPage()
    {
        InitializeComponent();
        Title.Text = S.T("Devices_Title");
        RefreshButton.Content = S.T("Devices_Rescan");
        UsbOnly.Content = S.T("Devices_UsbOnly");
        LastInput.Text = S.T("Devices_LastInput_None");
        RulesTitle.Text = S.T("Devices_Rules");
        NewRuleTitle.Text = S.T("Devices_NewRule");
        RuleDevice.PlaceholderText = S.T("Devices_DevicePh");
        RuleTrigger.PlaceholderText = S.T("Devices_TriggerPh");
        HotkeyCapture.Attach(RuleTrigger);
        RulePayload.PlaceholderText = S.T("Devices_PayloadPh");
        RuleDevice.PlaceholderText = S.T("Devices_DevicePh");
        RuleTrigger.PlaceholderText = S.T("Devices_TriggerPh");
        RulePayload.PlaceholderText = S.T("Devices_PayloadPh");
        AddButton.Content = S.T("Common_Add");
        DeleteButton.Content = S.T("Common_Delete");
        foreach (var preset in ActionOptions.All)
            RuleActionType.Items.Add(new ComboBoxItem { Tag = preset, Content = preset.Label });
        RuleActionType.SelectedIndex = 0;
        UsbOnly.IsChecked = AppState.Current.Config.UsbOnlyFilter;
        UsbOnly.Checked += (_, _) => { AppState.Current.Config.UsbOnlyFilter = true; AppState.Current.Save(); RefreshDevices(); };
        UsbOnly.Unchecked += (_, _) => { AppState.Current.Config.UsbOnlyFilter = false; AppState.Current.Save(); RefreshDevices(); };
        RefreshDevices();
        RefreshRules();
        var sink = AppState.Current.Devices;
        if (sink is not null)
            sink.InputReceived += OnInput;
        Unloaded += (_, _) =>
        {
            try
            {
                var s = AppState.Current.Devices;
                if (s is not null) s.InputReceived -= OnInput;
            }
            catch { }
        };
    }

    private void OnInput(nint handle)
    {
        App.RunOnUi(() =>
        {
            var d = _devices.FirstOrDefault(x => x.Handle == handle);
            LastInput.Text = d is null ? S.T("Devices_LastInput_Unknown") : S.T("Devices_LastInput", d.DisplayName);
        });
    }

    private void RefreshDevices()
    {
        _devices = DeviceService.Enumerate();
        var shown = AppState.Current.Config.UsbOnlyFilter ? _devices.Where(d => d.IsUsb).ToList() : _devices;
        DeviceList.ItemsSource = new List<InputDeviceInfo>(shown);
        RuleDevice.Items.Clear();
        foreach (var d in _devices)
            RuleDevice.Items.Add(new ComboBoxItem { Tag = d.DeviceName, Content = $"[{d.BusDisplay}] {d.DisplayName}" });
        if (RuleDevice.Items.Count > 0) RuleDevice.SelectedIndex = 0;
        int usb = _devices.Count(d => d.IsUsb);
        Status.Text = S.T("Devices_Count", _devices.Count, usb, shown.Count);
    }

    private void RefreshRules()
    {
        RuleList.ItemsSource = null;
        RuleList.ItemsSource = AppState.Current.Config.DeviceRules;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        if (RuleDevice.SelectedItem is not ComboBoxItem dev || string.IsNullOrWhiteSpace(RuleTrigger.Text))
        {
            Status.Text = S.T("Devices_NeedFields");
            return;
        }
        var preset = ((ComboBoxItem?)RuleActionType.SelectedItem)?.Tag as ActionPreset;
        AppState.Current.Config.DeviceRules.Add(new DeviceRule
        {
            DeviceId = (string)dev.Tag,
            DeviceName = dev.Content?.ToString() ?? "",
            Trigger = RuleTrigger.Text.Trim(),
            Action = preset is null ? AppAction.None() : ActionOptions.BuildPreset(preset, RulePayload.Text),
        });
        AppState.Current.Save();
        AppState.Current.RebuildHotkeys();
        RefreshRules();
        RuleTrigger.Text = "";
        Status.Text = S.T("Devices_RuleAdded");
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (RuleList.SelectedItem is DeviceRule r)
        {
            AppState.Current.Config.DeviceRules.Remove(r);
            AppState.Current.Save();
            AppState.Current.RebuildHotkeys();
            RefreshRules();
        }
    }
}
