using System.Globalization;
using System.Text;

namespace EnergyMonitor.Services;

public static class PersianHelper
{
    private static readonly string[] PersianDigits = ["۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹"];

    public static string ToPersianDigits(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c >= '0' && c <= '9')
                sb.Append(PersianDigits[c - '0']);
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    public static string ToPersianNumber(this decimal value, string format = "N0")
    {
        return value.ToString(format, CultureInfo.InvariantCulture).ToPersianDigits();
    }

    public static string ToPersianNumber(this int value, string format = "N0")
    {
        return value.ToString(format, CultureInfo.InvariantCulture).ToPersianDigits();
    }

    public static string FormatMoney(this decimal amount)
    {
        return amount.ToPersianNumber("N0") + " ریال";
    }

    public static string FormatKWh(this decimal kwh)
    {
        return kwh.ToPersianNumber("N3") + " kWh";
    }
}
