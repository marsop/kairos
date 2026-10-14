using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kairos.PlaywrightTests
{
    [TestFixture]
    public class HistoryStatsCsvExportTests : PageTest
    {
        private const string AppUrl = "http://localhost:5111";

        [SetUp]
        public async Task Setup()
        {
            await Page.GotoAsync(AppUrl);
            await Page.Locator("text='Loading Kairos...'").WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 120000 });

            var settings = new
            {
                TutorialCompleted = true,
                Language = "en",
                CommentRequired = false,
                ActivityGroupsEnabled = true,
                ActivityGroupCount = 1,
                HistoryView = "stats"
            };

            var session = new
            {
                AccessToken = "fake-token",
                RefreshToken = "fake-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1).ToString("O"),
                User = new { id = "test", email = "test@test.com" }
            };

            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            var account = new
            {
                Activities = new[]
                {
                    new { Id = Guid.NewGuid(), Name = "Work", Emoji = "💻", Color = "#ff0000", DisplayOrder = 0, GroupId = 0 }
                },
                Events = new[]
                {
                    new
                    {
                        Id = Guid.NewGuid(),
                        StartTime = today.AddHours(9).ToString("O"),
                        EndTime = today.AddHours(11).ToString("O"),
                        ActivityName = "Work",
                        ActivityEmoji = "💻",
                        ActivityColor = "#ff0000"
                    },
                    new
                    {
                        Id = Guid.NewGuid(),
                        StartTime = yesterday.AddHours(10).ToString("O"),
                        EndTime = yesterday.AddHours(12).ToString("O"),
                        ActivityName = "Work",
                        ActivityEmoji = "💻",
                        ActivityColor = "#ff0000"
                    }
                }
            };

            await Page.EvaluateAsync($@"
                localStorage.setItem('Kairos_settings', JSON.stringify({JsonSerializer.Serialize(settings)}));
                localStorage.setItem('Kairos_supabase_session', JSON.stringify({JsonSerializer.Serialize(session)}));
                localStorage.setItem('Kairos_account', JSON.stringify({JsonSerializer.Serialize(account)}));
                localStorage.setItem('Kairos_TutorialCompleted', 'true');
                localStorage.setItem('Kairos_LastSeenVersion', 'true');
                localStorage.setItem('Kairos_ReleaseNotesSeenVersion', 'true');
            ");

            await Page.GotoAsync($"{AppUrl}/history");
            await Page.Locator("text='Loading Kairos...'").WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 120000 });
            await Page.WaitForSelectorAsync(".page-title", new() { Timeout = 10000 });
        }

        [Test]
        public async Task StatsView_CsvExport_VisibleOnlyWhenDaySelected()
        {
            // Initially in Day mode
            var menuTrigger = Page.Locator(".header-action-menu .menu-trigger");
            await Expect(menuTrigger).ToBeVisibleAsync();

            // Switch to Week view
            var weekButton = Page.Locator(".history-toolbar .view-toggle button:has-text('Week')");
            await weekButton.ClickAsync();
            await Expect(menuTrigger).Not.ToBeVisibleAsync();

            // Switch to Month view
            var monthButton = Page.Locator(".history-toolbar .view-toggle button:has-text('Month')");
            await monthButton.ClickAsync();
            await Expect(menuTrigger).Not.ToBeVisibleAsync();

            // Switch back to Day view
            var dayButton = Page.Locator(".history-toolbar .view-toggle button:has-text('Day')");
            await dayButton.ClickAsync();
            await Expect(menuTrigger).ToBeVisibleAsync();
        }

        [Test]
        public async Task StatsView_CsvExport_DownloadsSelectedDayInDatePicker()
        {
            // Install download spy
            await Page.EvaluateAsync(@"() => {
                window.__lastDownloadedFile = null;
                window.kairosFile = window.kairosFile || {};
                window.kairosFile.downloadText = function(req) {
                    window.__lastDownloadedFile = req;
                };
            }");

            // Navigate to previous day using the Stats day date selector
            var prevButton = Page.Locator(".history-toolbar .history-date-selector .history-date-nav-btn").First;
            await prevButton.ClickAsync();
            await Task.Delay(200);

            var expectedDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

            // Click menu trigger
            var menuTrigger = Page.Locator(".header-action-menu .menu-trigger");
            await menuTrigger.ClickAsync();

            // Click export dropdown item
            var exportItem = Page.Locator(".dropdown-menu.show .dropdown-item");
            var boundingBox = await exportItem.BoundingBoxAsync();
            Assert.That(boundingBox, Is.Not.Null, "Dropdown item bounding box should not be null");

            await Page.Mouse.MoveAsync(boundingBox!.X + boundingBox.Width / 2, boundingBox.Y + boundingBox.Height / 2);
            await Page.Mouse.DownAsync();
            await Task.Delay(100);
            await Page.Mouse.UpAsync();
            await Task.Delay(200);

            // Verify the exported file name
            var downloadedFileName = await Page.EvaluateAsync<string>(@"() => {
                return window.__lastDownloadedFile ? window.__lastDownloadedFile.fileName : '';
            }");

            Assert.That(downloadedFileName, Is.EqualTo($"kairos-entries-{expectedDate}.csv"));
        }
    }
}
