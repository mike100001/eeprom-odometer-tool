using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;

namespace OdometerTool.Services;

public class AuthService
{
    private readonly IJSRuntime _js;
    private readonly IConfiguration _config;
    private const string SessionKey = "ot_auth";

    public bool IsAuthenticated { get; private set; }
    public event Action? OnChange;

    public AuthService(IJSRuntime js, IConfiguration config)
    {
        _js = js;
        _config = config;
    }

    public async Task InitializeAsync()
    {
        var stored = await _js.InvokeAsync<string?>("sessionStorage.getItem", SessionKey);
        if (stored == "1")
            IsAuthenticated = true;
    }

    public async Task<bool> TryLoginAsync(string password)
    {
        var expectedHash = _config["PasswordHash"] ?? "";
        var inputHash = Hash(password);

        if (!string.IsNullOrEmpty(expectedHash) && inputHash == expectedHash)
        {
            IsAuthenticated = true;
            await _js.InvokeVoidAsync("sessionStorage.setItem", SessionKey, "1");
            OnChange?.Invoke();
            return true;
        }

        return false;
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
