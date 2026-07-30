using System.Text.Json;
using Kairos.Core.Models;
using Kairos.Application.Services;

namespace Kairos.ValidationTest;

public class StatisticsServiceTests
{
    private readonly InMemoryStorageService _storage;
    private readonly StatisticsService _service;
    private const string StorageKey = "Kairos_budgets";

    public StatisticsServiceTests()
    {
        _storage = new InMemoryStorageService();
        _service = new StatisticsService(_storage);
    }

    [Fact]
    public async Task GetBudgetsAsync_ShouldReturnEmptyList_WhenNoBudgetsExist()
    {
        // Act
        var result = await _service.GetBudgetsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBudgetsAsync_ShouldReturnBudgets_WhenBudgetsExistInStorage()
    {
        // Arrange
        var budgets = new List<ActivityBudget>
        {
            new() { Id = Guid.NewGuid(), ActivityId = Guid.NewGuid(), Type = BudgetType.Daily, AllocatedTimeSpan = TimeSpan.FromHours(2) }
        };
        await _storage.SetItemAsync(StorageKey, JsonSerializer.Serialize(budgets));

        // Use a new service instance to force load from storage
        var service = new StatisticsService(_storage);

        // Act
        var result = await service.GetBudgetsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(budgets[0].Id, result[0].Id);
    }

    [Fact]
    public async Task SaveBudgetAsync_ShouldAddNewBudget()
    {
        // Arrange
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Weekly,
            AllocatedTimeSpan = TimeSpan.FromHours(10)
        };

        // Act
        await _service.SaveBudgetAsync(budget);

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Single(budgets);
        Assert.Equal(budget.Id, budgets[0].Id);
    }

    [Fact]
    public async Task SaveBudgetAsync_ShouldUpdateExistingBudgetByReplacingIt()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var budget1 = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            Type = BudgetType.Monthly,
            AllocatedTimeSpan = TimeSpan.FromHours(10)
        };

        await _service.SaveBudgetAsync(budget1);

        var budget2 = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            Type = BudgetType.Monthly,
            AllocatedTimeSpan = TimeSpan.FromHours(20)
        };

        // Act
        await _service.SaveBudgetAsync(budget2);

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Single(budgets);
        Assert.Equal(budget2.Id, budgets[0].Id);
        Assert.Equal(TimeSpan.FromHours(20), budgets[0].AllocatedTimeSpan);
    }

    [Fact]
    public async Task SaveBudgetAsync_ShouldAllowDifferentTypesForSameActivity()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var dailyBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            Type = BudgetType.Daily,
            AllocatedTimeSpan = TimeSpan.FromHours(2)
        };
        await _service.SaveBudgetAsync(dailyBudget);

        var weeklyBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            Type = BudgetType.Weekly,
            AllocatedTimeSpan = TimeSpan.FromHours(10)
        };

        // Act
        await _service.SaveBudgetAsync(weeklyBudget);

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Equal(2, budgets.Count);
        Assert.Contains(budgets, b => b.Type == BudgetType.Daily);
        Assert.Contains(budgets, b => b.Type == BudgetType.Weekly);
    }

    [Fact]
    public async Task DeleteBudgetAsync_ShouldRemoveExistingBudget()
    {
        // Arrange
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Daily
        };
        await _service.SaveBudgetAsync(budget);

        // Act
        await _service.DeleteBudgetAsync(budget.Id);

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Empty(budgets);
    }

    [Fact]
    public async Task DeleteBudgetAsync_ShouldDoNothing_WhenBudgetDoesNotExist()
    {
        // Arrange
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Weekly
        };
        await _service.SaveBudgetAsync(budget);

        // Act
        await _service.DeleteBudgetAsync(Guid.NewGuid());

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Single(budgets);
    }

    [Fact]
    public async Task GetBudgetAsync_ShouldReturnBudget_WhenBudgetExistsForType()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            Type = BudgetType.Monthly
        };
        await _service.SaveBudgetAsync(budget);

        // Act
        var result = await _service.GetBudgetAsync(activityId, BudgetType.Monthly);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(budget.Id, result.Id);
    }

    [Fact]
    public async Task GetBudgetAsync_ShouldReturnNull_WhenNoBudgetExistsForType()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            Type = BudgetType.Weekly
        };
        await _service.SaveBudgetAsync(budget);

        // Act
        var result = await _service.GetBudgetAsync(activityId, BudgetType.Monthly);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBudgetsAsync_ShouldPreferRemoteBudgets_WhenSupabaseReturnsData()
    {
        // Arrange
        var localBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Daily,
            AllocatedTimeSpan = TimeSpan.FromHours(1)
        };
        await _storage.SetItemAsync(StorageKey, JsonSerializer.Serialize(new List<ActivityBudget> { localBudget }));

        var remoteBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Monthly,
            AllocatedTimeSpan = TimeSpan.FromHours(25)
        };

        var supabaseStore = new StubSupabaseBudgetStore
        {
            Budgets = new List<ActivityBudget> { remoteBudget }
        };

        var service = new StatisticsService(_storage, supabaseStore: supabaseStore);

        // Act
        var result = await service.GetBudgetsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(remoteBudget.Id, result[0].Id);
    }

    [Fact]
    public async Task GetBudgetsAsync_ShouldBootstrapRemote_WhenRemoteIsEmptyAndLocalHasData()
    {
        // Arrange
        var localBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Weekly,
            AllocatedTimeSpan = TimeSpan.FromHours(8)
        };
        await _storage.SetItemAsync(StorageKey, JsonSerializer.Serialize(new List<ActivityBudget> { localBudget }));

        var supabaseStore = new StubSupabaseBudgetStore();
        var service = new StatisticsService(_storage, supabaseStore: supabaseStore);

        // Act
        await service.GetBudgetsAsync();

        // Assert
        Assert.Equal(1, supabaseStore.SaveCalls);
        Assert.Single(supabaseStore.LastSavedBudgets);
        Assert.Equal(localBudget.Id, supabaseStore.LastSavedBudgets[0].Id);
    }

    [Fact]
    public async Task SaveBudgetAsync_ShouldPersistToSupabaseStore()
    {
        // Arrange
        var supabaseStore = new StubSupabaseBudgetStore();
        var service = new StatisticsService(_storage, supabaseStore: supabaseStore);

        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Monthly,
            AllocatedTimeSpan = TimeSpan.FromHours(12)
        };

        // Act
        await service.SaveBudgetAsync(budget);

        // Assert
        Assert.Equal(1, supabaseStore.SaveCalls);
        Assert.Single(supabaseStore.LastSavedBudgets);
        Assert.Equal(budget.Id, supabaseStore.LastSavedBudgets[0].Id);
    }

    [Fact]
    public async Task DeleteBudgetAsync_ShouldPersistDeletionToSupabaseStore()
    {
        // Arrange
        var supabaseStore = new StubSupabaseBudgetStore();
        var service = new StatisticsService(_storage, supabaseStore: supabaseStore);

        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Type = BudgetType.Daily,
            AllocatedTimeSpan = TimeSpan.FromHours(2)
        };

        await service.SaveBudgetAsync(budget);

        // Act
        await service.DeleteBudgetAsync(budget.Id);

        // Assert
        Assert.True(supabaseStore.SaveCalls >= 2);
        Assert.Empty(supabaseStore.LastSavedBudgets);
    }
}

internal sealed class StubSupabaseBudgetStore : ISupabaseBudgetStore
{
    public List<ActivityBudget> Budgets { get; set; } = new();
    public List<ActivityBudget> LastSavedBudgets { get; private set; } = new();
    public int SaveCalls { get; private set; }

    public Task<IReadOnlyList<ActivityBudget>> LoadBudgetsAsync()
    {
        return Task.FromResult<IReadOnlyList<ActivityBudget>>(Budgets.Select(Clone).ToList());
    }

    public Task SaveBudgetsAsync(IReadOnlyList<ActivityBudget> budgets)
    {
        SaveCalls++;
        LastSavedBudgets = budgets.Select(Clone).ToList();
        Budgets = budgets.Select(Clone).ToList();
        return Task.CompletedTask;
    }

    private static ActivityBudget Clone(ActivityBudget budget)
    {
        return new ActivityBudget
        {
            Id = budget.Id,
            ActivityId = budget.ActivityId,
            AllocatedTimeSpan = budget.AllocatedTimeSpan,
            MinimumTimeSpan = budget.MinimumTimeSpan,
            Type = budget.Type
        };
    }
}
