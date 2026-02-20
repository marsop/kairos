using Budgetr.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace Budgetr.Web.Services;

/// <summary>
/// Web implementation of Supabase service using JavaScript interop.
/// </summary>
public class SupabaseService
{
    private readonly IJSRuntime _js;
    private readonly IStorageService _storage;
    private readonly IConfiguration _configuration;
    private const string UrlStorageKey = "budgetr_supabase_url";
    private const string AnonKeyStorageKey = "budgetr_supabase_anonkey";
    private string? _supabaseUrl;
    private string? _anonKey;
    private bool _isInitialized;

    public SupabaseService(IJSRuntime js, IStorageService storage, IConfiguration configuration)
    {
        _js = js;
        _storage = storage;
        _configuration = configuration;
    }

    public async Task InitializeAsync(string supabaseUrl, string anonKey)
    {
        if (_isInitialized && _supabaseUrl == supabaseUrl && _anonKey == anonKey)
            return;
            
        _supabaseUrl = supabaseUrl;
        _anonKey = anonKey;
        await _storage.SetItemAsync(UrlStorageKey, supabaseUrl);
        await _storage.SetItemAsync(AnonKeyStorageKey, anonKey);
        
        try
        {
            await _js.InvokeVoidAsync("supabaseInterop.initialize", supabaseUrl, anonKey);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Supabase: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> TryAutoInitializeAsync()
    {
        if (_isInitialized)
            return true;
            
        // First priority: Configuration file
        var configUrl = _configuration["Supabase:Url"];
        var configKey = _configuration["Supabase:AnonKey"];
        if (!string.IsNullOrEmpty(configUrl) && configUrl != "YOUR_SUPABASE_URL" &&
            !string.IsNullOrEmpty(configKey) && configKey != "YOUR_ANON_KEY")
        {
            try 
            {
                await InitializeAsync(configUrl, configKey);
                return true;
            }
            catch
            {
                // Fallback to storage
            }
        }
    
        // Second priority: Previously saved credentials
        var savedUrl = await _storage.GetItemAsync(UrlStorageKey);
        var savedKey = await _storage.GetItemAsync(AnonKeyStorageKey);
        if (!string.IsNullOrEmpty(savedUrl) && !string.IsNullOrEmpty(savedKey))
        {
            try
            {
                await InitializeAsync(savedUrl, savedKey);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public string? GetConfiguredUrl() => _supabaseUrl;
    public string? GetConfiguredAnonKey() => _anonKey;

    public async Task<bool> IsSignedInAsync()
    {
        if (!_isInitialized)
            return false;
            
        try
        {
            return await _js.InvokeAsync<bool>("supabaseInterop.isSignedIn");
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetUserEmailAsync()
    {
        if (!_isInitialized)
            return null;
            
        try
        {
            return await _js.InvokeAsync<string?>("supabaseInterop.getUserEmail");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> SignInAsync(string email, string password)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Supabase service not initialized. Please configure URL and anon key first.");
            
        try
        {
            return await _js.InvokeAsync<bool>("supabaseInterop.signIn", email, password);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Supabase sign in failed: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> SignUpAsync(string email, string password)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Supabase service not initialized. Please configure URL and anon key first.");
            
        try
        {
            return await _js.InvokeAsync<bool>("supabaseInterop.signUp", email, password);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Supabase sign up failed: {ex.Message}");
            throw;
        }
    }

    public async Task SignOutAsync()
    {
        if (!_isInitialized)
            return;
            
        try
        {
            await _js.InvokeVoidAsync("supabaseInterop.signOut");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Supabase sign out failed: {ex.Message}");
        }
    }

    public async Task<string?> GetLatestBackupContentAsync()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Supabase service not initialized.");
            
        try
        {
            return await _js.InvokeAsync<string?>("supabaseInterop.downloadData");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get Supabase backup: {ex.Message}");
            throw;
        }
    }

    public async Task<DateTimeOffset?> SaveBackupAsync(string content)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Supabase service not initialized.");
            
        try
        {
            var updatedAt = await _js.InvokeAsync<string?>("supabaseInterop.uploadData", content);
            if (!string.IsNullOrEmpty(updatedAt) && DateTimeOffset.TryParse(updatedAt, out var result))
            {
                return result;
            }
            return DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save Supabase backup: {ex.Message}");
            throw;
        }
    }

    public async Task<DateTimeOffset?> GetBackupLastModifiedAsync()
    {
        if (!_isInitialized)
            return null;
            
        try
        {
            var isoString = await _js.InvokeAsync<string?>("supabaseInterop.getLastBackupTime");
            if (!string.IsNullOrEmpty(isoString) && DateTimeOffset.TryParse(isoString, out var result))
            {
                return result;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
