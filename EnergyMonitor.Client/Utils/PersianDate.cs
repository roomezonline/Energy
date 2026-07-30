using System.Globalization;

namespace EnergyMonitor.Client.Utils;

public static class PersianDate
{
    private static readonly PersianCalendar pc = new();

    public static string ToPersianDate(this DateTime dt)
    {
        return $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
    }

    public static string ToPersianDateTime(this DateTime dt)
    {
        return $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00} {dt.Hour:00}:{dt.Minute:00}";
    }

    public static string ToPersianDateTime(this DateTime? dt)
    {
        return dt.HasValue ? ToPersianDateTime(dt.Value) : "---";
    }
}
