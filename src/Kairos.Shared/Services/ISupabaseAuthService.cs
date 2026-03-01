namespace Kairos.Shared.Services;

/// <summary>
/// Authentication service for Supabase email/password flows.
/// </summary>
public interface ISupabaseAuthService
{
    bool IsInitialized { get; }
    bool IsConfigured { get; }
    bool IsAuthenticated { get; }
    string? CurrentUserEmail { get; }
    string? CurrentUserId { get; }
    string? CurrentAccessToken { get; }

    event Action? OnAuthStateChanged;

    Task InitializeAsync();
    Task<SupabaseAuthResult> SignInAsync(string email, string password);
    Task<SupabaseAuthResult> SignUpAsync(string email, string password);
    Task SignOutAsync();
}

public sealed class SupabaseAuthResult
{
    public bool Succeeded { get; init; }
    public bool RequiresEmailConfirmation { get; init; }
    public string? ErrorMessage { get; init; }
}
