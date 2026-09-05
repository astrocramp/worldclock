using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorldClockWidget;

public partial class AddCityWindow : Window
{
    private readonly List<TimeZoneInfo> _allZones;

    public TimeZoneInfo SelectedTimeZone { get; private set; }

    public AddCityWindow()
    {
        InitializeComponent();
        _allZones = TimeZoneInfo.GetSystemTimeZones().ToList();
        ZoneList.ItemsSource = _allZones;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = SearchBox.Text?.Trim() ?? string.Empty;
        ZoneList.ItemsSource = _allZones
            .Where(z => z.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => TryAccept();

    private void ZoneList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => TryAccept();

    private void TryAccept()
    {
        if (ZoneList.SelectedItem is TimeZoneInfo zone)
        {
            SelectedTimeZone = zone;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
