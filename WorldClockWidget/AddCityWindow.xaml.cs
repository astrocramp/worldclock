using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorldClockWidget;

public partial class AddCityWindow : Window
{
    private const int MinQueryLength = 2;
    private const int MaxResults = 50;

    private List<ZoneOption> _allOptions;
    private bool _isLoaded;

    public string SelectedDisplayName { get; private set; }
    public TimeZoneInfo SelectedTimeZone { get; private set; }

    public AddCityWindow()
    {
        InitializeComponent();
        ZoneList.ItemsSource = Array.Empty<ZoneOption>();

        Loaded += async (_, _) =>
        {
            // Usually already finished, since MainWindow starts this same
            // background load as soon as the app opens (see CityDatabase.WarmUp).
            // If not, the search box stays typable and results appear as soon
            // as loading catches up.
            _allOptions = await CityDatabase.GetOptionsAsync();
            _isLoaded = true;
            HintText.Text = "Type at least 2 letters to search ~69,000 world cities and towns.";
            ApplyFilter();
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!_isLoaded)
        {
            return;
        }

        var filter = SearchBox.Text?.Trim() ?? string.Empty;
        ZoneList.ItemsSource = filter.Length < MinQueryLength
            ? Array.Empty<ZoneOption>()
            : _allOptions
                .Where(o => o.Label.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.Population)
                .Take(MaxResults)
                .ToList();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => TryAccept();

    private void ZoneList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => TryAccept();

    private void TryAccept()
    {
        if (ZoneList.SelectedItem is ZoneOption option)
        {
            SelectedDisplayName = option.Label;
            SelectedTimeZone = option.Zone;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
