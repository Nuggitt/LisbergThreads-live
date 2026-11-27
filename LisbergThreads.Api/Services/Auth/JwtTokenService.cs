using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LisbergThreads.Api.Data;
using LisbergThreads.Contracts.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace LisbergThreads.Api.Services.Auth;

public sealed class JwtTokenService(UserManager<ApplicationUser> userManager, IConfiguration configuration)
{
    // The security stamp claim lets the JWT validation reject tokens issued before a password or profile change.
    public async Task<LoginResponse> CreateLoginResponseAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(ClaimTypes.Name, user.Email!),
            new("security_stamp", await userManager.GetSecurityStampAsync(user))
        };
        foreach (var role in await userManager.GetRolesAsync(user))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var jwt = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token));
    }
}
