using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Budgetr.Shared.Models;

namespace Budgetr.Shared.Services;

public record TutorialStep(string Message, string? Route = null, string? ImageUrl = null);

public interface ITutorialService
{
    bool IsActive { get; }
    TutorialStep? CurrentStep { get; }
    
    bool HasCompletedTutorial { get; }

    IReadOnlyList<AvatarProfile> AvailableAvatars { get; }
    AvatarProfile CurrentAvatar { get; }

    event Action? OnChange;

    Task InitializeAsync();
    void StartTutorial();
    void NextStep();
    Task CompleteTutorialAsync();
    Task ResetTutorialAsync();
    Task SetAvatarAsync(string avatarId);
}
