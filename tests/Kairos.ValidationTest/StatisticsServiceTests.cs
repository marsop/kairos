using System.Text.Json;
using Kairos.Shared.Models;
using Kairos.Shared.Services;

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
            new() { Id = Guid.NewGuid(), ActivityId = Guid.NewGuid(), StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)) }
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
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7))
        };

        // Act
        await _service.SaveBudgetAsync(budget);

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Single(budgets);
        Assert.Equal(budget.Id, budgets[0].Id);
    }

    [Fact]
    public async Task SaveBudgetAsync_ShouldUpdateExistingBudget()
    {
        // Arrange
        var budgetId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var budget = new ActivityBudget
        {
            Id = budgetId,
            ActivityId = activityId,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            AllocatedTimeSpan = TimeSpan.FromHours(10)
        };

        await _service.SaveBudgetAsync(budget);

        var updatedBudget = new ActivityBudget
        {
            Id = budgetId,
            ActivityId = activityId,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            AllocatedTimeSpan = TimeSpan.FromHours(20)
        };

        // Act
        await _service.SaveBudgetAsync(updatedBudget);

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Single(budgets);
        Assert.Equal(TimeSpan.FromHours(20), budgets[0].AllocatedTimeSpan);
    }

    [Theory]
    [InlineData(0, 10, 5, 15)] // New budget starts inside existing, ends after
    [InlineData(5, 15, 0, 10)] // New budget starts before existing, ends inside
    [InlineData(0, 10, 2, 8)]  // New budget entirely inside existing
    [InlineData(2, 8, 0, 10)]  // Existing budget entirely inside new
    [InlineData(0, 10, 0, 10)] // Exact match
    public async Task SaveBudgetAsync_ShouldThrowInvalidOperationException_WhenOverlappingBudget(
        int existingStartOffset, int existingEndOffset,
        int newStartOffset, int newEndOffset)
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var existingBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            StartDate = today.AddDays(existingStartOffset),
            EndDate = today.AddDays(existingEndOffset)
        };
        await _service.SaveBudgetAsync(existingBudget);

        var newBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            StartDate = today.AddDays(newStartOffset),
            EndDate = today.AddDays(newEndOffset)
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveBudgetAsync(newBudget));
    }

    [Fact]
    public async Task SaveBudgetAsync_ShouldNotThrow_WhenDifferentActivityIdOverlaps()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var existingBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartDate = today,
            EndDate = today.AddDays(7)
        };
        await _service.SaveBudgetAsync(existingBudget);

        var newBudget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartDate = today,
            EndDate = today.AddDays(7)
        };

        // Act & Assert
        await _service.SaveBudgetAsync(newBudget);
        var budgets = await _service.GetBudgetsAsync();
        Assert.Equal(2, budgets.Count);
    }

    [Fact]
    public async Task DeleteBudgetAsync_ShouldRemoveExistingBudget()
    {
        // Arrange
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7))
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
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7))
        };
        await _service.SaveBudgetAsync(budget);

        // Act
        await _service.DeleteBudgetAsync(Guid.NewGuid());

        // Assert
        var budgets = await _service.GetBudgetsAsync();
        Assert.Single(budgets);
    }

    [Fact]
    public async Task GetBudgetForPeriodAsync_ShouldReturnBudget_WhenPeriodOverlaps()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            StartDate = today,
            EndDate = today.AddDays(7)
        };
        await _service.SaveBudgetAsync(budget);

        // Act - check a period that overlaps
        var result = await _service.GetBudgetForPeriodAsync(activityId, today.AddDays(2), today.AddDays(5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(budget.Id, result.Id);
    }

    [Fact]
    public async Task GetBudgetForPeriodAsync_ShouldReturnNull_WhenNoBudgetOverlaps()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var budget = new ActivityBudget
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            StartDate = today,
            EndDate = today.AddDays(7)
        };
        await _service.SaveBudgetAsync(budget);

        // Act - check a period completely after
        var result = await _service.GetBudgetForPeriodAsync(activityId, today.AddDays(8), today.AddDays(14));

        // Assert
        Assert.Null(result);
    }
}
