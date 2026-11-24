using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace LisbergThreads.Client.Services;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TokenStorageService _tokenStorageService;

    public JwtAuthenticationStateProvider(
        TokenStorageService tokenStorageService)
    {
        _tokenStorageService = tokenStorageService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorageService.GetTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
        {
            return CreateAnonymousState();
        }

        try
        {
            var claims = ParseClaimsFromJwt(token);

            if (IsTokenExpired(claims))
            {
                await _tokenStorageService.RemoveTokenAsync();
                return CreateAnonymousState();
            }

            var identity = new ClaimsIdentity(
                claims,
                authenticationType: "jwt");

            return new AuthenticationState(
                new ClaimsPrincipal(identity));
        }
        catch
        {
            await _tokenStorageService.RemoveTokenAsync();

            return CreateAnonymousState();
        }
    }

    private static bool IsTokenExpired(IEnumerable<Claim> claims)
    {
        var expClaim = claims.FirstOrDefault(
            claim => claim.Type == "exp");

        if (expClaim is null)
        {
            return true;
        }

        if (!long.TryParse(expClaim.Value, out var exp))
        {
            return true;
        }

        var expirationTime =
            DateTimeOffset.FromUnixTimeSeconds(exp);

        return expirationTime <= DateTimeOffset.UtcNow;
    }

    public async Task NotifyUserAuthenticationAsync()
    {
        var state = await GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }

    public async Task NotifyUserLogoutAsync()
    {
        await _tokenStorageService.RemoveTokenAsync();

        NotifyAuthenticationStateChanged(
            Task.FromResult(CreateAnonymousState()));
    }

    public async Task<bool> NotifySessionRejectedAsync(string rejectedToken)
    {
        if (!await _tokenStorageService.RemoveTokenIfMatchesAsync(rejectedToken))
        {
            return false;
        }

        NotifyAuthenticationStateChanged(Task.FromResult(CreateAnonymousState()));
        return true;
    }

    private static AuthenticationState CreateAnonymousState()
    {
        return new AuthenticationState(
            new ClaimsPrincipal(
                new ClaimsIdentity()));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];

        var jsonBytes = ParseBase64WithoutPadding(payload);

        var keyValuePairs =
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes)
            ?? [];

        var claims = new List<Claim>();

        foreach (var pair in keyValuePairs)
        {
            if (pair.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pair.Value.EnumerateArray())
                {
                    claims.Add(new Claim(
                        pair.Key,
                        item.ToString()));
                }
            }
            else
            {
                claims.Add(new Claim(
                    pair.Key,
                    pair.Value.ToString()));
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+')
                       .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;

            case 3:
                base64 += "=";
                break;
        }

        return Convert.FromBase64String(base64);
    }


}