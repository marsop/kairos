using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kairos.PlaywrightTests
{
    [TestFixture]
    public class DropdownTests : PageTest
    {
        private const string AppUrl = "http://localhost:5111";

        [SetUp]
        public async Task Setup()
        {
            await Page.GotoAsync(AppUrl);

            // Bypass tutorial and auth
            var settings = new
            {
                TutorialCompleted = true,
                Language = "en",
                CommentRequired = false,
                ActivityGroupsEnabled = true,
                ActivityGroupCount = 3
            };

            var session = new
            {
                AccessToken = "fake-token",
                RefreshToken = "fake-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1).ToString("O"),
                User = new { id = "test", email = "test@test.com" }
            };

            await Page.EvaluateAsync($@"
                localStorage.setItem('Kairos_settings', JSON.stringify({JsonSerializer.Serialize(settings)}));
                localStorage.setItem('Kairos_supabase_session', JSON.stringify({JsonSerializer.Serialize(session)}));
            ");

            await Page.ReloadAsync();
        }

        [Test]
        public async Task CanChangeActivityGroup_UsingDropdown()
        {
            // Navigate to activities page
            await Page.WaitForSelectorAsync("a.nav-item[href='activities']", new() { Timeout = 60000 });
            await Page.ClickAsync("a.nav-item[href='activities']");
            await Page.WaitForSelectorAsync(".page-title", new() { Timeout = 60000 });

            // Click dropdown trigger
            var trigger = Page.Locator(".dropdown-trigger").First;
            await trigger.ClickAsync();

            // Wait for dropdown menu to appear
            await Page.WaitForSelectorAsync(".dropdown-menu.show", new() { Timeout = 5000 });

            // Click the second item (Group 1)
            var item = Page.Locator(".dropdown-menu.show .dropdown-item:nth-child(2)").First;
            var boundingBox = await item.BoundingBoxAsync();
            Assert.That(boundingBox, Is.Not.Null, "Dropdown item bounding box should not be null");

            // Simulate mouse click directly on the item's coordinates
            await Page.Mouse.MoveAsync(boundingBox!.X + boundingBox!.Width / 2, boundingBox!.Y + boundingBox!.Height / 2);
            await Page.Mouse.DownAsync();
            await Task.Delay(100);
            await Page.Mouse.UpAsync();

            // Verify group changed
            await Task.Delay(500); // Give time for UI update
            var newGroup = await Page.Locator(".dropdown-trigger span").First.InnerTextAsync();

            Assert.That(newGroup, Does.Contain("Group 1"));
        }

        [Test]
        public async Task CanChangeLanguage_UsingDropdown_InSettings()
        {
            // Navigate to settings page
            await Page.GotoAsync($"{AppUrl}/settings");
            await Page.WaitForSelectorAsync(".page-title", new() { Timeout = 60000 });

            // Click language dropdown trigger (it's the first dropdown on the page)
            var trigger = Page.Locator(".dropdown-trigger").First;
            await trigger.ClickAsync();

            // Wait for dropdown menu to appear
            await Page.WaitForSelectorAsync(".dropdown-menu.show", new() { Timeout = 5000 });

            // Click the second item (Deutsch)
            var item = Page.Locator(".dropdown-menu.show .dropdown-item:nth-child(2)").First;
            var boundingBox = await item.BoundingBoxAsync();
            Assert.That(boundingBox, Is.Not.Null, "Dropdown item bounding box should not be null");

            // Simulate mouse click directly on the item's coordinates
            await Page.Mouse.MoveAsync(boundingBox!.X + boundingBox!.Width / 2, boundingBox!.Y + boundingBox!.Height / 2);
            await Page.Mouse.DownAsync();
            await Task.Delay(100);
            await Page.Mouse.UpAsync();

            // Verify language changed
            await Task.Delay(500); // Give time for UI update
            var newLang = await Page.Locator(".dropdown-trigger span").Nth(1).InnerTextAsync();

            Assert.That(newLang, Does.Contain("Deutsch"));
        }
    }
}
