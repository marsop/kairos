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

        [Test]
        public async Task CalendarEvent_ClickSelectsWithoutZooming_DoubleClickZoomsEvent()
        {
            var eventLocator = Page.Locator(".calendar-event-block").First;
            await eventLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

            // Initial state: not selected
            var isSelectedInitially = await eventLocator.EvaluateAsync<bool>("el => el.classList.contains('selected-event')");
            Assert.That(isSelectedInitially, Is.False);

            // Record initial zoom via hour marker 01:00 (3600s)
            var marker1h = Page.Locator(".calendar-hour-marker[data-seconds='3600']");
            var initialTop = await marker1h.EvaluateAsync<double>("el => parseFloat(el.style.top || '0')");

            // 1. Single click on event
            await eventLocator.ClickAsync();
            await Task.Delay(400);

            // Verify event is now selected
            var isSelectedAfterClick = await eventLocator.EvaluateAsync<bool>("el => el.classList.contains('selected-event')");
            Assert.That(isSelectedAfterClick, Is.True, "Event should be selected after single click");

            // Verify zoom has NOT changed
            var topAfterClick = await marker1h.EvaluateAsync<double>("el => parseFloat(el.style.top || '0')");
            Assert.That(topAfterClick, Is.EqualTo(initialTop).Within(0.5), "Zoom level should NOT change on single click");

            // 2. Double click on event
            await eventLocator.DblClickAsync();
            await Task.Delay(600); // Allow animation to complete (duration 360ms)

            // Verify event remains selected
            var isSelectedAfterDblClick = await eventLocator.EvaluateAsync<bool>("el => el.classList.contains('selected-event')");
            Assert.That(isSelectedAfterDblClick, Is.True, "Event should remain selected after double click");

            // Verify zoom HAS changed
            var topAfterDblClick = await marker1h.EvaluateAsync<double>("el => parseFloat(el.style.top || '0')");
            Assert.That(topAfterDblClick, Is.Not.EqualTo(initialTop), "Zoom level SHOULD change on double click");
        }

        [Test]
        public async Task CalendarEvent_SelectAndPressDelete_ShowsConfirmationDialogAndDeletes()
        {
            var eventLocator = Page.Locator(".calendar-event-block").First;
            await eventLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

            // 1. Click to select the event
            await eventLocator.ClickAsync();
            await Task.Delay(200);

            var isSelected = await eventLocator.EvaluateAsync<bool>("el => el.classList.contains('selected-event')");
            Assert.That(isSelected, Is.True, "Event should be selected after click");

            // 2. Press DELETE key on keyboard
            await Page.Keyboard.PressAsync("Delete");

            // 3. Verify confirmation dialog appears
            var modal = Page.Locator(".modal-overlay");
            await modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            var title = await modal.Locator("h3").TextContentAsync();
            Assert.That(title, Is.Not.Null.And.Not.Empty);

            // 4. Test cancel: event should still exist
            var cancelButton = modal.Locator(".btn-cancel");
            await cancelButton.ClickAsync();
            await modal.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
            Assert.That(await eventLocator.CountAsync(), Is.EqualTo(1), "Event should still exist after cancelling deletion");

            // 5. Select again and press DELETE
            await eventLocator.ClickAsync();
            await Task.Delay(200);
            await Page.Keyboard.PressAsync("Delete");
            await modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            // 6. Confirm deletion
            var deleteButton = modal.Locator(".btn-delete");
            await deleteButton.ClickAsync();
            await modal.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });

            // 7. Event should be deleted from the calendar
            await eventLocator.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 5000 });
            var remainingEvents = await Page.Locator(".calendar-event-block").CountAsync();
            Assert.That(remainingEvents, Is.EqualTo(0), "Event should be removed from calendar after confirming deletion");
        }
    }
}
