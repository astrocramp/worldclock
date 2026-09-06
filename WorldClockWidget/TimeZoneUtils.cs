using System;

namespace WorldClockWidget;

public static class TimeZoneUtils
{
    public static TimeZoneInfo TryFindTimeZone(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    // GeoNames' city data reports IANA IDs (e.g. "America/Indiana/Indianapolis").
    // .NET can usually resolve those directly even on Windows, but converting to
    // the matching Windows ID first is more reliable across .NET/OS versions.
    public static TimeZoneInfo TryFindTimeZoneFromIana(string ianaId)
    {
        if (string.IsNullOrEmpty(ianaId))
        {
            return null;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId))
        {
            var zone = TryFindTimeZone(windowsId);
            if (zone != null)
            {
                return zone;
            }
        }

        return TryFindTimeZone(ianaId);
    }

    public static string FriendlyZoneName(TimeZoneInfo zone)
    {
        var name = zone.DisplayName;
        var parenEnd = name.IndexOf(')');
        return parenEnd >= 0 && parenEnd + 1 < name.Length ? name[(parenEnd + 1)..].Trim() : name;
    }
}
