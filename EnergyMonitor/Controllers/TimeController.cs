using EnergyMonitor.Services;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers;

[ApiController]
[Route("api/time")]
public class TimeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var utc = DateTime.UtcNow;
        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(utc, iranTz);
        var p = JalaliDateTimeHelper.GetPersianDateTime(now);

        return Ok(new
        {
            utc = utc.ToString("o"),
            persianDate = $"{p.Year:D4}/{p.Month:D2}/{p.Day:D2}",
            persianTime = $"{p.Hour:D2}:{p.Minute:D2}:{p.Second:D2}",
            persianDateTime = $"{p.Year:D4}/{p.Month:D2}/{p.Day:D2} {p.Hour:D2}:{p.Minute:D2}:{p.Second:D2}",
            persianDateTimeDigits = JalaliDateTimeHelper.ToPersianDigits(
                $"{p.Year:D4}/{p.Month:D2}/{p.Day:D2} {p.Hour:D2}:{p.Minute:D2}:{p.Second:D2}")
        });
    }
}
