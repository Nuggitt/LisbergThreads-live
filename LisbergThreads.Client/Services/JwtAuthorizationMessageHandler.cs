using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Headers;

namespace LisbergThreads.Client.Services;

public class JwtAuthorizationMessageHandler : DelegatingHandler
{
    private readonly TokenStorageService _tokenStorageService;
    private readonly JwtAuthenticationStateProvider _authenticationStateProvider;
    private readonly NavigationManager _navigation;

    public JwtAuthorizationMessageHandler(
        TokenStorageService tokenStorageService,
        JwtAuthenticationStateProvider authenticationStateProvider,
        NavigationManager navigation)
    {
        _tokenStorageService = tokenStorageService;
        _authenticationStateProvider = authenticationStateProvider;
        _navigation = navigation;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenStorageService.GetTokenAsync();

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && !string.IsNullOrWhiteSpace(token)
            && !string.Equals(request.RequestUri?.AbsolutePath, "/api/Auth/login", StringComparison.OrdinalIgnoreCase)
            && await _authenticationStateProvider.NotifySessionRejectedAsync(token))
        {
            _navigation.NavigateTo("/login?sessionExpired=true");
        }

        return response;
    }
}