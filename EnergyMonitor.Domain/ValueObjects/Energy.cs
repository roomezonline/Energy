namespace EnergyMonitor.Domain.ValueObjects;

public readonly struct Energy : IEquatable<Energy>, IComparable<Energy>
{
    private const decimal PzemMaxKWh = 4294967m;

    public decimal KWh { get; }

    public Energy(decimal kWh)
    {
        KWh = kWh >= 0 ? kWh : 0;
    }

    public static Energy Zero => new(0);

    public static Energy Delta(Energy current, Energy previous)
    {
        if (previous.KWh < 0.001m) return Zero;
        var d = current.KWh - previous.KWh;
        if (d < -0.001m) d += PzemMaxKWh;
        return new Energy(d < 0 ? 0 : d);
    }

    public static Energy FromKWh(decimal value) => new(value);

    public static Energy operator +(Energy a, Energy b) => new(a.KWh + b.KWh);
    public static Energy operator -(Energy a, Energy b) => new(a.KWh - b.KWh);
    public static Energy operator *(Energy e, decimal factor) => new(e.KWh * factor);
    public static bool operator >(Energy a, decimal b) => a.KWh > b;
    public static bool operator <(Energy a, decimal b) => a.KWh < b;
    public static bool operator >=(Energy a, decimal b) => a.KWh >= b;
    public static bool operator <=(Energy a, decimal b) => a.KWh <= b;

    public bool Equals(Energy other) => KWh == other.KWh;
    public override bool Equals(object? obj) => obj is Energy other && Equals(other);
    public override int GetHashCode() => KWh.GetHashCode();
    public int CompareTo(Energy other) => KWh.CompareTo(other.KWh);
    public override string ToString() => $"{KWh:F4}";
}
