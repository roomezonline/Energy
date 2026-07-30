using System.Text.Json.Serialization;
using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Domain.Entities;

public class EnergyLimit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    [JsonIgnore]
    public Center? Center { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LimitType LimitType { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PeriodType PeriodType { get; set; }
    [JsonConverter(typeof(DecimalConverter))]
    public decimal MaxValue { get; set; }
    [JsonConverter(typeof(DecimalConverter))]
    public decimal AlertThresholdPercent { get; set; } = 80;
    public bool IsActive { get; set; } = true;
}

public class DecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal>
{
    public override decimal Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
            return reader.GetDecimal();
        if (reader.TokenType == System.Text.Json.JsonTokenType.String && decimal.TryParse(reader.GetString(), out var d))
            return d;
        return 0;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, decimal value, System.Text.Json.JsonSerializerOptions options)
        => writer.WriteNumberValue((double)value);
}
