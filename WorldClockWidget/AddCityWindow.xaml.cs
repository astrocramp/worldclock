using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WorldClockWidget.Models;

namespace WorldClockWidget;

public partial class AddCityWindow : Window
{
    private class ZoneOption
    {
        public string Label { get; init; }
        public TimeZoneInfo Zone { get; init; }
    }

    private readonly List<ZoneOption> _allOptions;

    public string SelectedDisplayName { get; private set; }
    public TimeZoneInfo SelectedTimeZone { get; private set; }

    public AddCityWindow()
    {
        InitializeComponent();

        var options = new List<ZoneOption>();

        // Curated city names come first so common searches like "Munich" or
        // "Bahrain" match a real place instead of relying on the generic
        // Windows time zone display names, which only list a few cities per zone.
        foreach (var city in LoadCities())
        {
            var zone = TimeZoneUtils.TryFindTimeZone(city.TimeZoneId);
            if (zone != null)
            {
                options.Add(new ZoneOption { Label = $"{city.City}, {city.Country}", Zone = zone });
            }
        }

        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            options.Add(new ZoneOption { Label = zone.DisplayName, Zone = zone });
        }

        _allOptions = options;
        ZoneList.ItemsSource = _allOptions;
    }

    private static List<CityEntry> LoadCities()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Cities.json");
            if (!File.Exists(path))
            {
                return new List<CityEntry>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<CityEntry>>(json) ?? new List<CityEntry>();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new List<CityEntry>();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = SearchBox.Text?.Trim() ?? string.Empty;
        ZoneList.ItemsSource = _allOptions
            .Where(o => o.Label.Contains(filter, StringComparison.OrdinalIgnoreCase))
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
