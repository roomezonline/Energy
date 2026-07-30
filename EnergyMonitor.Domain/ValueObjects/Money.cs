namespace EnergyMonitor.Domain.ValueObjects;

public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public decimal Rials { get; }

    public Money(decimal rials)
    {
        Rials = rials >= 0 ? rials : 0;
    }

    public static Money Zero => new(0);

    public static Money operator +(Money a, Money b) => new(a.Rials + b.Rials);
    public static Money operator -(Money a, Money b) => new(a.Rials - b.Rials);
    public static Money operator *(Money m, decimal factor) => new(m.Rials * factor);
    public static Money operator *(decimal factor, Money m) => new(m.Rials * factor);
    public static Money operator *(Money m, int factor) => new(m.Rials * factor);

    public bool Equals(Money other) => Rials == other.Rials;
    public override bool Equals(object? obj) => obj is Money other && Equals(other);
    public override int GetHashCode() => Rials.GetHashCode();
    public int CompareTo(Money other) => Rials.CompareTo(other.Rials);
    public override string ToString() => $"{Rials:N0}";
}
