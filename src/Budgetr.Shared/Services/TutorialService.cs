using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Localization;
using Budgetr.Shared.Resources;
using Budgetr.Shared.Models;

namespace Budgetr.Shared.Services;

public class TutorialService : ITutorialService
{
    private readonly IStorageService _storageService;
    private readonly ISettingsService _settingsService;
    private readonly NavigationManager _navigationManager;
    private readonly IStringLocalizer<Strings> _localizer;
    private const string TutorialCompletedKey = "tutorial_completed_v1"; // Kept for migration
    private const string TutorialAvatarKey = "tutorial_avatar_v1";
    
    private int _currentStepIndex = -1;
    private List<TutorialStep> _steps = new();

    public bool IsActive => _currentStepIndex >= 0 && _currentStepIndex < _steps.Count;
    public TutorialStep? CurrentStep => IsActive ? _steps[_currentStepIndex] : null;

    public bool HasCompletedTutorial => _settingsService.TutorialCompleted;

    public IReadOnlyList<AvatarProfile> AvailableAvatars { get; } = new List<AvatarProfile>
    {
        new AvatarProfile("kawaii", "Kawaii", "img/avatars/kawaii"),
        new AvatarProfile("zarzaparrilla", "Zarzaparrilla", "img/avatars/zarzaparrilla")
    };

    public AvatarProfile CurrentAvatar { get; private set; }

    public event Action? OnChange;

    public TutorialService(IStorageService storageService, ISettingsService settingsService, NavigationManager navigationManager, IStringLocalizer<Strings> localizer)
    {
        _storageService = storageService;
        _settingsService = settingsService;
        _navigationManager = navigationManager;
        _localizer = localizer;
        
        CurrentAvatar = AvailableAvatars[0];
        InitializeSteps();
    }

    private void InitializeSteps()
    {
        var basePath = CurrentAvatar.BasePath;
        
        _steps = new List<TutorialStep>
        {
            // 1. Intro
            new TutorialStep(_localizer["TutorialIntro"], "", $"{basePath}/tutorial-avatar-5.png"),
            
            // 2. Overview
            new TutorialStep(_localizer["TutorialOverview"], "", $"{basePath}/tutorial-avatar-2.png"),
            
            // 3. Meters
            new TutorialStep(_localizer["TutorialMeters"], "meters", $"{basePath}/tutorial-avatar-3.png"),
            
            // 4. Timeline
            new TutorialStep(_localizer["TutorialTimeline"], "timeline", $"{basePath}/tutorial-avatar-4.png"),
            
            // 5. History
            new TutorialStep(_localizer["TutorialHistory"], "history", $"{basePath}/tutorial-avatar.png"),
            
            // 6. Sync
            new TutorialStep(_localizer["TutorialSync"], "sync", $"{basePath}/tutorial-avatar-2.png"),
            
            // 7. Completion
            new TutorialStep(_localizer["TutorialCompletion"], "", $"{basePath}/tutorial-avatar-3.png")
        };
    }

    public async Task InitializeAsync()
    {
        var avatarId = await _storageService.GetItemAsync(TutorialAvatarKey);
        if (!string.IsNullOrEmpty(avatarId))
        {
            var profile = AvailableAvatars.FirstOrDefault(a => a.Id == avatarId);
            if (profile != null)
            {
                CurrentAvatar = profile;
                InitializeSteps(); // Re-init steps with loaded avatar
            }
        }

        // Check if we need to migrate legacy completion status
        if (!_settingsService.TutorialCompleted)
        {
            var legacyCompleted = await _storageService.GetItemAsync(TutorialCompletedKey);
            if (legacyCompleted != null)
            {
                // Migrate to settings
                _settingsService.TutorialCompleted = true;
                // We can optionally remove the old key, but keeping it is harmless for now
                await _storageService.RemoveItemAsync(TutorialCompletedKey);
            }
            else
            {
                // First time launch!
                StartTutorial();
            }
        }
    }

    public async Task SetAvatarAsync(string avatarId)
    {
        var profile = AvailableAvatars.FirstOrDefault(a => a.Id == avatarId);
        if (profile != null && profile.Id != CurrentAvatar.Id)
        {
            CurrentAvatar = profile;
            InitializeSteps();
            await _storageService.SetItemAsync(TutorialAvatarKey, avatarId);
            NotifyStateChanged();
        }
    }

    public void StartTutorial()
    {
        _currentStepIndex = 0;
        NavigateToCurrentStep();
        NotifyStateChanged();
    }

    public void NextStep()
    {
        if (!IsActive) return;

        _currentStepIndex++;

        if (_currentStepIndex >= _steps.Count)
        {
            // Tutorial finished
            _ = CompleteTutorialAsync();
        }
        else
        {
            NavigateToCurrentStep();
            NotifyStateChanged();
        }
    }

    public async Task CompleteTutorialAsync()
    {
        _currentStepIndex = -1;
        _settingsService.TutorialCompleted = true;
        NotifyStateChanged();
        await Task.CompletedTask; 
    }

    public async Task ResetTutorialAsync()
    {
        _settingsService.TutorialCompleted = false;
        // Ensure legacy key is gone too
        await _storageService.RemoveItemAsync(TutorialCompletedKey);
        StartTutorial();
    }

    private void NavigateToCurrentStep()
    {
        if (CurrentStep?.Route != null)
        {
            // Check if we are already there to avoid reload if not needed? 
            // NavigationManager handles that gracefully usually.
            _navigationManager.NavigateTo(CurrentStep.Route);
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
