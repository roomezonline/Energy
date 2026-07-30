using System.Globalization;

namespace EnergyMonitor.Domain.ValueObjects;

public readonly struct PersianDate : IEquatable<PersianDate>, IComparable<PersianDate>
{
    public int Year { get; }
    public int Month { get; }
    public int Day { get; }

    public PersianDate(int year, int month, int day)
    {
        if (year < 1300 || year > 1500) throw new ArgumentOutOfRangeException(nameof(year));
        if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (day < 1 || day > 31) throw new ArgumentOutOfRangeException(nameof(day));
        Year = year;
        Month = month;
        Day = day;
    }

    public static PersianDate FromDateTime(DateTime dt)
    {
        var pc = new PersianCalendar();
        return new PersianDate(pc.GetYear(dt), pc.GetMonth(dt), pc.GetDayOfMonth(dt));
    }

    public DateTime ToDateTime() => new PersianCalendar().ToDateTime(Year, Month, Day, 0, 0, 0, 0);

    public override string ToString() => $"{Year:D4}/{Month:D2}/{Day:D2}";

    public static PersianDate Parse(string s)
    {
        var parts = s.Split('/');
        if (parts.Length != 3) throw new FormatException("Invalid Persian date format");
        return new PersianDate(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    public bool Equals(PersianDate other) => Year == other.Year && Month == other.Month && Day == other.Day;
    public override bool Equals(object? obj) => obj is PersianDate other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Year, Month, Day);
    public int CompareTo(PersianDate other)
    {
        var c = Year.CompareTo(other.Year);
        if (c != 0) return c;
        c = Month.CompareTo(other.Month);
        return c != 0 ? c : Day.CompareTo(other.Day);
    }
    public static bool operator ==(PersianDate a, PersianDate b) => a.Equals(b);
    public static bool operator !=(PersianDate a, PersianDate b) => !a.Equals(b);
    public static bool operator <(PersianDate a, PersianDate b) => a.CompareTo(b) < 0;
    public static bool operator >(PersianDate a, PersianDate b) => a.CompareTo(b) > 0;
    public static bool operator <=(PersianDate a, PersianDate b) => a.CompareTo(b) <= 0;
    public static bool operator >=(PersianDate a, PersianDate b) => a.CompareTo(b) >= 0;

    public static PersianDate operator +(PersianDate d, int days)
    {
        var dt = d.ToDateTime().AddDays(days);
        return FromDateTime(dt);
    }

    public static int operator -(PersianDate a, PersianDate b)
    {
        return (int)(a.ToDateTime() - b.ToDateTime()).TotalDays;
    }
}
