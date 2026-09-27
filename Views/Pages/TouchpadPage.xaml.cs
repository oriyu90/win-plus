using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinPlus.Models;
using WinPlus.Services;

namespace WinPlus.Views.Pages;

public sealed partial class TouchpadPage : Page
{
    private readonly List<(GestureBinding Binding, ComboBox TypeBox, TextBox PayloadBox)> _rows = new();

    public TouchpadPage()
    {
        InitializeComponent();
        Title.Text = S.T("Touch_Title");
        Desc.Text = S.T("Touch_Desc");
        OpenSettingsButton.Content = S.T("Touch_OpenSettings");
        RemapEnabled.Content = S.T("Touch_Remap");
        MeasureButton.Content = S.T("Touch_Measure");
        Subtitle.Text = S.T("Touch_Subtitle");
        SaveButton.Content = S.T("Common_Save");
        var state = AppState.Current;
        bool ptp = false;
        try { ptp = state.Touchpad.PtpPresent; } catch { }
        PtpBar.Title = ptp ? S.T("Touch_PtpOkTitle") : S.T("Touch_PtpNgTitle");
        PtpBar.Message = ptp ? S.T("Touch_PtpOkMsg") : S.T("Touch_PtpNgMsg");
        PtpBar.Severity = ptp ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        RemapEnabled.IsChecked = state.Touchpad.RemapEnabled;
        RemapEnabled.Checked += (_, _) => { state.Touchpad.RemapEnabled = true; };
        RemapEnabled.Unchecked += (_, _) => { state.Touchpad.RemapEnabled = false; };
        foreach (var g in state.Config.Gestures)
            GestureList.Children.Add(BuildRow(g));
    }

    private StackPanel BuildRow(GestureBinding g)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = AppConfig.GestureDisplayName(g.Gesture), Style = (Microsoft.UI.Xaml.Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
        var typeBox = new ComboBox();
        foreach (var preset in ActionOptions.All)
            typeBox.Items.Add(new ComboBoxItem { Tag = preset, Content = preset.Label });
        foreach (ComboBoxItem item in typeBox.Items)
            if (item.Tag is ActionPreset p && ActionOptions.BuildPreset(p, "") is AppAction built
                && built.Type == g.Action.Type && built.Payload == g.Action.Payload) { typeBox.SelectedItem = item; break; }
        if (typeBox.SelectedItem is null)
        {
            foreach (ComboBoxItem item in typeBox.Items)
                if (item.Tag is ActionPreset p2 && !p2.Fixed && p2.Type == g.Action.Type) { typeBox.SelectedItem = item; break; }
        }
        if (typeBox.SelectedItem is null) typeBox.SelectedIndex = 0;
        var payload = new TextBox { Text = g.Action.Payload, PlaceholderText = S.T("Devices_PayloadPh") };
        void Refresh()
        {
            var preset = ((ComboBoxItem?)typeBox.SelectedItem)?.Tag as ActionPreset;
            payload.Visibility = preset is not null && ActionOptions.NeedsPayload(preset) ? Visibility.Visible : Visibility.Collapsed;
        }
        typeBox.SelectionChanged += (_, _) => Refresh();
        Refresh();
        panel.Children.Add(typeBox);
        panel.Children.Add(payload);
        _rows.Add((g, typeBox, payload));
        return panel;
    }

    private void OpenOsSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo { FileName = "ms-settings:devices-touchpad", UseShellExecute = true }); }
        catch { }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (b, typeBox, payload) in _rows)
        {
            var preset = ((ComboBoxItem?)typeBox.SelectedItem)?.Tag as ActionPreset;
            b.Action = preset is null ? AppAction.None() : ActionOptions.BuildPreset(preset, payload.Text);
        }
        AppState.Current.Save();
        Status.Text = S.T("Common_SavedAt", DateTime.Now.ToString("T"));
    }

    private void Measure_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MeasureResult.Text = S.T("Touch_MeasureHint");
            var w = new TouchMeasureWindow(count =>
            {
                App.RunOnUi(() =>
                {
                    MeasureResult.Text = count switch
                    {
                        -2 => S.T("Touch_MeasureNoDevice"),
                        < 0 => S.T("Touch_MeasureNone"),
                        _ => S.T("Touch_MeasureResult", count),
                    };
                });
            });
            w.Activate();
        }
        catch { }
    }
}
