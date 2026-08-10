using System.Text.Json;
using Kairos.Application.Services;
using Kairos.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kairos.ValidationTest;

public class BudgetSettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_ValidLocalJson_AppliesData()
    {
        // Arrange
        var storage = new InMemoryStorageService();
        var localData = new BudgetSettingsData
        {
            MinimumEnabled = true,
            Threshold = 85,
            ColorMinimumNotReached = "#111111",
            ColorMinimumReachedMaxNotReached = "#222222",
            ColorBetweenThresholdMax = "#333333",
            ColorOverMax = "#444444",
            BudgetType = BudgetType.Weekly,
            NotificationsEnabled = false
        };
        await storage.SetItemAsync("Kairos_budget_settings", JsonSerializer.Serialize(localData));

        var sut = new BudgetSettingsService(storage, new StubSettingsService(), NullLogger<BudgetSettingsService>.Instance);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.True(sut.MinimumEnabled);
        Assert.Equal(85, sut.Threshold);
        Assert.Equal("#111111", sut.ColorMinimumNotReached);
        Assert.Equal("#222222", sut.ColorMinimumReachedMaxNotReached);
        Assert.Equal("#333333", sut.ColorBetweenThresholdMax);
        Assert.Equal("#444444", sut.ColorOverMax);
        Assert.Equal(BudgetType.Weekly, sut.BudgetType);
        Assert.False(sut.NotificationsEnabled);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task LoadAsync_WithSupabase_RemoteExists_AppliesRemoteAndSavesLocal()
    {
        // Arrange
        var storage = new InMemoryStorageService();
        var remoteData = new BudgetSettingsData
        {
            MinimumEnabled = true,
            Threshold = 90
        };
        var supabase = new StubSupabaseBudgetSettingsStore
        {
            LoadedData = remoteData
        };

        var sut = new BudgetSettingsService(storage, new StubSettingsService(), NullLogger<BudgetSettingsService>.Instance, supabase);

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.True(sut.MinimumEnabled);
        Assert.Equal(90, sut.Threshold);
        Assert.Equal(1, supabase.LoadCalls);

        // Verify it was saved locally
        var savedLocalJson = await storage.GetItemAsync("Kairos_budget_settings");
        Assert.NotNull(savedLocalJson);
        var savedData = JsonSerializer.Deserialize<BudgetSettingsData>(savedLocalJson!);
        Assert.True(savedData!.MinimumEnabled);
        Assert.Equal(90, savedData.Threshold);
    }

    [Fact]
    public async Task LoadAsync_WithSupabase_NoRemote_SeedsSupabaseWithLocalValues()
    {
        // Arrange
        var storage = new InMemoryStorageService();
        var localData = new BudgetSettingsData
        {
            MinimumEnabled = true,
            Threshold = 80
        };
        await storage.SetItemAsync("Kairos_budget_settings", JsonSerializer.Serialize(localData));

        var supabase = new StubSupabaseBudgetSettingsStore(); // No remote data

        var sut = new BudgetSettingsService(storage, new StubSettingsService(), NullLogger<BudgetSettingsService>.Instance, supabase);

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.True(sut.MinimumEnabled); // Should have applied local
        Assert.Equal(80, sut.Threshold);

        Assert.Equal(1, supabase.LoadCalls);
        Assert.Equal(1, supabase.SaveCalls);
        Assert.NotNull(supabase.SavedData);
        Assert.True(supabase.SavedData!.MinimumEnabled);
        Assert.Equal(80, supabase.SavedData.Threshold);
    }

    [Fact]
    public void Threshold_ClampsValueBetween75And99()
    {
        // Arrange
        var storage = new InMemoryStorageService();
        var sut = new BudgetSettingsService(storage, new StubSettingsService(), NullLogger<BudgetSettingsService>.Instance);

        // Act & Assert
        sut.Threshold = 50;
        Assert.Equal(75, sut.Threshold);

        sut.Threshold = 150;
        Assert.Equal(99, sut.Threshold);

        sut.Threshold = 85;
        Assert.Equal(85, sut.Threshold);
    }

    [Fact]
    public async Task PropertySetter_WhenLoaded_SavesAsyncAndNotifies()
    {
        // Arrange
        var storage = new InMemoryStorageService();
        var supabase = new StubSupabaseBudgetSettingsStore();
        var sut = new BudgetSettingsService(storage, new StubSettingsService(), NullLogger<BudgetSettingsService>.Instance, supabase);

        await sut.LoadAsync();

        var events = 0;
        sut.OnSettingsChanged += () => events++;

        var initialSaveCalls = supabase.SaveCalls;
        var initialLocalSaves = storage.SetCalls;

        // Act
        sut.MinimumEnabled = true;

        // Let the async save complete
        await Task.Delay(50);

        // Assert
        Assert.True(sut.MinimumEnabled);
        Assert.Equal(1, events);
        Assert.Equal(initialLocalSaves + 1, storage.SetCalls);
        Assert.Equal(initialSaveCalls + 1, supabase.SaveCalls);
    }

    [Fact]
    public async Task InitializeDefaultsIfEmptyAsync_RemoteEmpty_SavesDefaults()
    {
        // Arrange
        var storage = new InMemoryStorageService();
        var supabase = new StubSupabaseBudgetSettingsStore();
        var sut = new BudgetSettingsService(storage, new StubSettingsService(), NullLogger<BudgetSettingsService>.Instance, supabase);

        // Act
        await sut.InitializeDefaultsIfEmptyAsync();

        // Assert
        Assert.Equal(1, supabase.LoadCalls);
        Assert.Equal(1, supabase.SaveCalls);
        Assert.NotNull(supabase.SavedData);
        Assert.Equal(95, supabase.SavedData!.Threshold); // Default threshold
    }

    [Fact]
    public async Task SaveAsync_SavesLocallyAndRemotely()
    {
        // Arrange
        var storage = new InMemoryStorageService();
        var supabase = new StubSupabaseBudgetSettingsStore();
        var sut = new BudgetSettingsService(storage, new StubSettingsService(), NullLogger<BudgetSettingsService>.Instance, supabase);

        sut.BudgetType = BudgetType.Weekly;

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.Equal(1, supabase.SaveCalls);
        Assert.NotNull(supabase.SavedData);
        Assert.Equal(BudgetType.Weekly, supabase.SavedData!.BudgetType);

        var savedLocalJson = await storage.GetItemAsync("Kairos_budget_settings");
        Assert.NotNull(savedLocalJson);
        var savedData = JsonSerializer.Deserialize<BudgetSettingsData>(savedLocalJson!);
        Assert.Equal(BudgetType.Weekly, savedData!.BudgetType);
    }
}
