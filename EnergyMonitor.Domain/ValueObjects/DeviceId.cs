namespace EnergyMonitor.Domain.ValueObjects;

public readonly struct DeviceId : IEquatable<DeviceId>
{
    public string Value { get; }

    public DeviceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("DeviceId cannot be empty", nameof(value));
        Value = value;
    }

    public bool Equals(DeviceId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is DeviceId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
    public static bool operator ==(DeviceId a, DeviceId b) => a.Equals(b);
    public static bool operator !=(DeviceId a, DeviceId b) => !a.Equals(b);
}
