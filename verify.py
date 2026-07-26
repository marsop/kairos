from playwright.sync_api import sync_playwright
import json

def run_cuj(page):
    page.goto("http://localhost:5111")
    page.wait_for_timeout(10000)

    # Bypass tutorial & fake session to avoid Supabase auth
    mock_settings = {"TutorialCompleted": True}
    mock_session = {"ExpiresAt": "2099-01-01T00:00:00.000Z"}

    page.evaluate(f"localStorage.setItem('Kairos_settings', '{json.dumps(mock_settings)}');")
    page.evaluate(f"localStorage.setItem('Kairos_supabase_session', '{json.dumps(mock_session)}');")

    page.reload()
    page.wait_for_timeout(10000)

    # Wait for the nav-item element which implies main layout loaded
    page.wait_for_selector(".nav-item", timeout=30000)
    page.wait_for_timeout(5000)

    # Navigate to activities page via bottom navigation
    page.click("a[href='activities']")
    page.wait_for_timeout(3000)

    # Start adding new activity (since editing requires activities to exist)
    page.locator(".add-activity-btn").click()
    page.wait_for_timeout(2000)

    # Take screenshot for adding activity to verify metadata field is gone
    page.screenshot(path="/home/jules/verification/screenshots/verification_add.png")
    page.wait_for_timeout(1000)

if __name__ == "__main__":
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(
            record_video_dir="/home/jules/verification/videos"
        )
        page = context.new_page()
        try:
            run_cuj(page)
        finally:
            context.close()
            browser.close()
