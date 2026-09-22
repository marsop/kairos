using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace Kairos.PlaywrightTests
{
    [TestFixture]
    public class CalendarZoomTests : PageTest
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
                HistoryView = "calendar"
            };

            var session = new
            {
                AccessToken = "fake-token",
                RefreshToken = "fake-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1).ToString("O"),
                User = new { id = "test", email = "test@test.com" }
            };

            var today = DateTime.UtcNow.Date;
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
            await Page.WaitForSelectorAsync(".calendar-scroll-area", new() { Timeout = 10000 });
        }

        [Test]
        public async Task CalendarZoom_ZoomInAndZoomOut_RecalculatesMarkerPositionsCorrectly()
        {
            // Helper to get marker positions (seconds, style.top, text)
            async Task<(int Seconds, double Top, string Text)[]> GetMarkersAsync()
            {
                var json = await Page.EvaluateAsync<string>(@"() => {
                    var markers = Array.from(document.querySelectorAll('.calendar-hour-marker'));
                    return JSON.stringify(markers.map(m => ({
                        seconds: parseFloat(m.dataset.seconds || '0'),
                        top: parseFloat(m.style.top || '0'),
                        text: m.textContent.trim()
                    })));
                }");
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.EnumerateArray()
                    .Select(e => (
                        e.GetProperty("seconds").GetInt32(),
                        e.GetProperty("top").GetDouble(),
                        e.GetProperty("text").GetString()!
                    ))
                    .ToArray();
            }

            // 1. Initial render at 60 px/hr
            var initial = await GetMarkersAsync();
            Assert.That(initial.Length, Is.EqualTo(24));
            // At 60 px/hr, 01:00 (3600s) should be at 60px
            var init1 = initial.First(m => m.Seconds == 3600);
            Assert.That(init1.Top, Is.EqualTo(60.0).Within(0.5));
            Assert.That(init1.Text, Is.EqualTo("01:00"));

            // 2. Zoom in 1x -> 120 px/hr (30-minute intervals = 48 markers)
            await Page.ClickAsync("button[title*='Zoom in'], button:has-text('+')");
            await Task.Delay(400);

            var zoomedIn = await GetMarkersAsync();
            Assert.That(zoomedIn.Length, Is.EqualTo(48));
            var inHalfHour = zoomedIn.First(m => m.Seconds == 1800);
            Assert.That(inHalfHour.Top, Is.EqualTo(60.0).Within(0.5)); // 0.5h * 120px = 60px
            Assert.That(inHalfHour.Text, Is.EqualTo("00:30"));
            var inOneHour = zoomedIn.First(m => m.Seconds == 3600);
            Assert.That(inOneHour.Top, Is.EqualTo(120.0).Within(0.5)); // 1h * 120px = 120px
            Assert.That(inOneHour.Text, Is.EqualTo("01:00"));

            // 3. Zoom out 1x -> back to 60 px/hr (60-minute intervals = 24 markers)
            await Page.ClickAsync("button[title*='Zoom out'], button:has-text('-')");
            await Task.Delay(400);

            var zoomedOut1 = await GetMarkersAsync();
            Assert.That(zoomedOut1.Length, Is.EqualTo(24));
            var out1Hour = zoomedOut1.First(m => m.Seconds == 3600);
            // 01:00 at 60 px/hr MUST be at 60px, NOT squashed to 30px!
            Assert.That(out1Hour.Top, Is.EqualTo(60.0).Within(0.5), "01:00 should be positioned at 60px after zoom out");
            Assert.That(out1Hour.Text, Is.EqualTo("01:00"));
            var out2Hour = zoomedOut1.First(m => m.Seconds == 7200);
            // 02:00 at 60 px/hr MUST be at 120px, NOT squashed to 60px!
            Assert.That(out2Hour.Top, Is.EqualTo(120.0).Within(0.5), "02:00 should be positioned at 120px after zoom out");
            Assert.That(out2Hour.Text, Is.EqualTo("02:00"));

            // 4. Zoom out 2x -> 30 px/hr (120-minute intervals = 12 markers)
            await Page.ClickAsync("button[title*='Zoom out'], button:has-text('-')");
            await Task.Delay(400);

            var zoomedOut2 = await GetMarkersAsync();
            Assert.That(zoomedOut2.Length, Is.EqualTo(12));
            var out2x2Hour = zoomedOut2.First(m => m.Seconds == 7200);
            // 02:00 at 30 px/hr MUST be at 60px!
            Assert.That(out2x2Hour.Top, Is.EqualTo(60.0).Within(0.5), "02:00 should be positioned at 60px at 30 px/hr zoom level");
            Assert.That(out2x2Hour.Text, Is.EqualTo("02:00"));
            var out2x4Hour = zoomedOut2.First(m => m.Seconds == 14400);
            // 04:00 at 30 px/hr MUST be at 120px!
            Assert.That(out2x4Hour.Top, Is.EqualTo(120.0).Within(0.5), "04:00 should be positioned at 120px at 30 px/hr zoom level");
            Assert.That(out2x4Hour.Text, Is.EqualTo("04:00"));
        }
    }
}
