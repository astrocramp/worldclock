using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WorldClockWidget.Models;

namespace WorldClockWidget;

public class ZoneOption
{
    public string Label { get; init; }
    public TimeZoneInfo Zone { get; init; }
    public int Population { get; init; }
}

// Parsing and resolving all ~69,500 curated cities is too slow to redo every
// time the "Add city" dialog opens. This builds the combined city + system
// time zone list exactly once per app run (on a background thread), and every
// caller - including repeat dialog opens - shares that same cached result.
public static class CityDatabase
{
    private static readonly Lazy<Task<List<ZoneOption>>> Options = new(() => Task.Run(BuildOptions));

    public static Task<List<ZoneOption>> GetOptionsAsync() => Options.Value;

    public static void WarmUp() => _ = Options.Value;

    private static List<ZoneOption> BuildOptions()
    {
        var options = new List<ZoneOption>();
        var resolvedZones = new Dictionary<string, TimeZoneInfo>();

        foreach (var city in LoadCities())
        {
            if (!resolvedZones.TryGetValue(city.TimeZoneId, out var zone))
            {
                zone = TimeZoneUtils.TryFindTimeZoneFromIana(city.TimeZoneId);
                resolvedZones[city.TimeZoneId] = zone;
            }

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

        return options;
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
            var assembly = typeof(CityDatabase).Assembly;
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
}
