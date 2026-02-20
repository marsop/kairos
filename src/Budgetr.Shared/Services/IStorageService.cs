namespace Budgetr.Shared.Services;

/// <summary>
/// Interface for platform-agnostic storage operations.
/// Implemented differently for Web (localStorage) and MAUI (file storage).
/// </summary>
public interface IStorageService
{
    Task<string?> GetItemAsync(string key);
    Task SetItemAsync(string key, string value);
    Task RemoveItemAsync(string key);
}
