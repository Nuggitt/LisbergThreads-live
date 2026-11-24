using Microsoft.JSInterop;

namespace LisbergThreads.Client.Services;

public class TokenStorageService
{
    private const string TokenKey = "authToken";

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public TokenStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetTokenAsync(string token)
    {
        await _writeGate.WaitAsync();
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            TokenKey);
    }

    public async Task RemoveTokenAsync()
    {
        await _writeGate.WaitAsync();
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<bool> RemoveTokenIfMatchesAsync(string rejectedToken)
    {
        await _writeGate.WaitAsync();
        try
        {
            var currentToken = await GetTokenAsync();
            if (!string.Equals(currentToken, rejectedToken, StringComparison.Ordinal))
            {
                return false;
            }

            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            return true;
        }
        finally
        {
            _writeGate.Release();
        }
    }
}