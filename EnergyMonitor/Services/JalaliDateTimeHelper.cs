using System.Globalization;
using System.Text;

namespace EnergyMonitor.Services;

public static class JalaliDateTimeHelper
{
    private static readonly ThreadLocal<PersianCalendar> _pc = new(() => new PersianCalendar());

    public static string ToPersianDateString(DateTime dateTime, string separator = "/")
    {
        var pc = _pc.Value;
        int year = pc.GetYear(dateTime);
        int month = pc.GetMonth(dateTime);
        int day = pc.GetDayOfMonth(dateTime);
        return $"{year:D4}{separator}{month:D2}{separator}{day:D2}";
    }

    public static string ToPersianDateTimeString(DateTime dateTime)
    {
        var pc = _pc.Value;
        int year = pc.GetYear(dateTime);
        int month = pc.GetMonth(dateTime);
        int day = pc.GetDayOfMonth(dateTime);
        int hour = pc.GetHour(dateTime);
        int minute = pc.GetMinute(dateTime);
        int second = pc.GetSecond(dateTime);
        return $"{year:D4}/{month:D2}/{day:D2} {hour:D2}:{minute:D2}:{second:D2}";
    }

    public static string ToPersianDigits(string str)
    {
        var sb = new StringBuilder(str);
        for (int i = 0; i < sb.Length; i++)
        {
            char c = sb[i];
            if (c >= '0' && c <= '9')
                sb[i] = (char)('۰' + (c - '0'));
        }
        return sb.ToString();
    }

    public static (int Year, int Month, int Day, int Hour, int Minute, int Second) GetPersianDateTime(DateTime dateTime)
    {
        var pc = _pc.Value;
        return (
            pc.GetYear(dateTime),
            pc.GetMonth(dateTime),
            pc.GetDayOfMonth(dateTime),
            pc.GetHour(dateTime),
            pc.GetMinute(dateTime),
            pc.GetSecond(dateTime)
        );
    }

    private static readonly TimeZoneInfo _iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");

    public static DateTime UtcToIran(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _iranTz);
    }

    public static string ToPersianDateTimeStringFromUtc(DateTime utcDateTime)
    {
        var iran = UtcToIran(utcDateTime);
        return ToPersianDateTimeString(iran);
    }

    public static string ToPersianDateStringFromUtc(DateTime utcDateTime, string separator = "/")
    {
        var iran = UtcToIran(utcDateTime);
        return ToPersianDateString(iran, separator);
    }

    public static (string Date, string Time) ToPersianDateTimePartsFromUtc(DateTime utcDateTime)
    {
        var iran = UtcToIran(utcDateTime);
        var (y, m, d, h, mn, s) = GetPersianDateTime(iran);
        return ($"{y:D4}/{m:D2}/{d:D2}", $"{h:D2}:{mn:D2}:{s:D2}");
    }

    public static string GetRelativeTime(DateTime utcNow, DateTime target)
    {
        var diff = utcNow - target;
        if (diff.TotalMinutes < 1) return "لحظاتی پیش";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} دقیقه پیش";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} ساعت پیش";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} روز پیش";
        return ToPersianDateStringFromUtc(target);
    }
}
