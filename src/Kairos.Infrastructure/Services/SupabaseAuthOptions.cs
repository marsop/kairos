namespace Kairos.Infrastructure.Services;

public sealed class SupabaseAuthOptions
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
}
