namespace EnergyMonitor.Domain.ValueObjects;

public readonly struct MonthYear : IEquatable<MonthYear>, IComparable<MonthYear>
{
    public int Year { get; }
    public int Month { get; }

    public MonthYear(int year, int month)
    {
        if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
        Year = year;
        Month = month;
    }

    public bool Equals(MonthYear other) => Year == other.Year && Month == other.Month;
    public override bool Equals(object? obj) => obj is MonthYear other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Year, Month);
    public int CompareTo(MonthYear other)
    {
        var c = Year.CompareTo(other.Year);
        return c != 0 ? c : Month.CompareTo(other.Month);
    }
    public override string ToString() => $"{Year:D4}/{Month:D2}";
    public static bool operator ==(MonthYear a, MonthYear b) => a.Equals(b);
    public static bool operator !=(MonthYear a, MonthYear b) => !a.Equals(b);
    public static bool operator <(MonthYear a, MonthYear b) => a.CompareTo(b) < 0;
    public static bool operator >(MonthYear a, MonthYear b) => a.CompareTo(b) > 0;
}
