using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace EnergyMonitor.Infrastructure.Security;

public class JwtTokenGenerator : ITokenGenerator
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryHours;

    public JwtTokenGenerator(string secretKey, string issuer, string audience, int expiryHours = 24)
    {
        _secretKey = secretKey;
        _issuer = issuer;
        _audience = audience;
        _expiryHours = expiryHours;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var centerIds = user.UserCenters?.Select(uc => uc.CenterId.ToString()).ToList() ?? new();
        if (user.CenterId.HasValue)
        {
            var cid = user.CenterId.Value.ToString();
            if (!centerIds.Contains(cid))
                centerIds.Insert(0, cid);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("centerId", user.CenterId?.ToString() ?? ""),
            new("centerIds", string.Join(",", centerIds)),
            new("regionId", user.RegionId?.ToString() ?? ""),
        };

        if (user.FullName != null)
            claims.Add(new Claim("fullName", user.FullName));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_expiryHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
