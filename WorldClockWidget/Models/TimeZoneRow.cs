using System;

namespace WorldClockWidget.Models;

public class TimeZoneRow
{
    public string DisplayName { get; set; }
    public TimeZoneInfo Zone { get; }
    public bool IsLocal { get; }

    public TimeZoneRow(string displayName, TimeZoneInfo zone, bool isLocal = false)
    {
        DisplayName = displayName;
        Zone = zone;
        IsLocal = isLocal;
    }
}
