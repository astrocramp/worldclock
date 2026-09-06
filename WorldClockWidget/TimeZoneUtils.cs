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

    public static string FriendlyZoneName(TimeZoneInfo zone)
    {
        var name = zone.DisplayName;
        var parenEnd = name.IndexOf(')');
        return parenEnd >= 0 && parenEnd + 1 < name.Length ? name[(parenEnd + 1)..].Trim() : name;
    }
}
