using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Localization;
using Kairos.Shared.Resources;
using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

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
            new TutorialStep(_localizer["TutorialIntro"], "", $"{basePath}/tutorial-avatar-10.png"),
            
            // 2. How Kairos works
            new TutorialStep(_localizer["TutorialHowItWorks"], "", $"{basePath}/tutorial-avatar-2.png"),

            // 3. Overview - current balance
            new TutorialStep(_localizer["TutorialOverviewBalance"], "", $"{basePath}/tutorial-avatar-6.png"),

            // 4. Overview - active activity indicator
            new TutorialStep(_localizer["TutorialOverviewActive"], "", $"{basePath}/tutorial-avatar-5.png"),
            
            // 5. Activities - basics
            new TutorialStep(_localizer["TutorialActivitiesBasics"], "activities", $"{basePath}/tutorial-avatar-3.png"),

            // 6. Activities - comments and switching
            new TutorialStep(_localizer["TutorialActivitiesComment"], "activities", $"{basePath}/tutorial-avatar-7.png"),

            // 7. Activities - organizing list
            new TutorialStep(_localizer["TutorialActivitiesManage"], "activities", $"{basePath}/tutorial-avatar-9.png"),
            
            // 8. Timeline - period controls
            new TutorialStep(_localizer["TutorialTimelinePeriods"], "timeline", $"{basePath}/tutorial-avatar-4.png"),

            // 9. Timeline - reading the chart
            new TutorialStep(_localizer["TutorialTimelineInterpretation"], "timeline", $"{basePath}/tutorial-avatar-8.png"),
            
            // 10. History - review entries
            new TutorialStep(_localizer["TutorialHistoryReview"], "history", $"{basePath}/tutorial-avatar.png"),
            
            // 11. History - edit and delete
            new TutorialStep(_localizer["TutorialHistoryEdit"], "history", $"{basePath}/tutorial-avatar-10.png"),

            // 12. Settings - personalization
            new TutorialStep(_localizer["TutorialSettingsPersonalize"], "settings", $"{basePath}/tutorial-avatar-2.png"),

            // 13. Settings - integrations and notifications
            new TutorialStep(_localizer["TutorialSettingsIntegrations"], "settings", $"{basePath}/tutorial-avatar-6.png"),

            // 14. Backups and reset safety
            new TutorialStep(_localizer["TutorialBackupAndSafety"], "settings", $"{basePath}/tutorial-avatar-5.png"),

            // 15. Completion
            new TutorialStep(_localizer["TutorialCompletion"], "", $"{basePath}/tutorial-avatar-7.png")
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
