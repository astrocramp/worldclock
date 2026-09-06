using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WorldClockWidget.Models;

namespace WorldClockWidget;

public partial class AddCityWindow : Window
{
    private const int MinQueryLength = 2;
    private const int MaxResults = 50;

    private class ZoneOption
    {
        public string Label { get; init; }
        public TimeZoneInfo Zone { get; init; }
        public int Population { get; init; }
    }

    private readonly List<ZoneOption> _allOptions;

    public string SelectedDisplayName { get; private set; }
    public TimeZoneInfo SelectedTimeZone { get; private set; }

    public AddCityWindow()
    {
        InitializeComponent();

        var options = new List<ZoneOption>();

        // A ~69,000-place dataset (derived from GeoNames, population 5,000+) so
        // even smaller towns like "Warsaw, IN" resolve to their real time zone,
        // not just the handful of cities Windows lists per zone by default.
        foreach (var city in LoadCities())
        {
            var zone = TimeZoneUtils.TryFindTimeZoneFromIana(city.TimeZoneId);
            if (zone != null)
            {
                options.Add(new ZoneOption
                {
                    Label = $"{city.City}, {RegionLabel(city.CountryCode, city.Admin1Code)}",
                    Zone = zone,
                    Population = city.Population
                });
            }
        }

        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            options.Add(new ZoneOption { Label = zone.DisplayName, Zone = zone, Population = 0 });
        }

        _allOptions = options;
        ZoneList.ItemsSource = Array.Empty<ZoneOption>();
    }

    private static string RegionLabel(string countryCode, string admin1Code)
    {
        if (countryCode == "US" && !string.IsNullOrEmpty(admin1Code))
        {
            return admin1Code;
        }

        try
        {
            return new RegionInfo(countryCode).EnglishName;
        }
        catch (ArgumentException)
        {
            return countryCode;
        }
    }

    private static List<CityEntry> LoadCities()
    {
        try
        {
            var assembly = typeof(AddCityWindow).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Cities.json", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                return new List<CityEntry>();
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<CityEntry>>(stream, options) ?? new List<CityEntry>();
        }
        catch (JsonException)
        {
            return new List<CityEntry>();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = SearchBox.Text?.Trim() ?? string.Empty;
        if (filter.Length < MinQueryLength)
        {
            ZoneList.ItemsSource = Array.Empty<ZoneOption>();
            return;
        }

        ZoneList.ItemsSource = _allOptions
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
