from __future__ import annotations

import time

from playwright.sync_api import sync_playwright, Browser, Page, Playwright

from .models import Settings


class BrowserManager:
    def __init__(self, settings: Settings):
        self._settings = settings
        self._playwright: Playwright | None = None
        self._browser: Browser | None = None
        self._page: Page | None = None

    def start(self) -> None:
        self._playwright = sync_playwright().start()
        self._browser = self._playwright.chromium.launch(
            headless=self._settings.browser_headless,
            slow_mo=self._settings.browser_slow_mo,
        )
        self._page = self._browser.new_page()
        print("  Browser started.")

    def navigate(self, url: str) -> Page:
        if not self._page:
            raise RuntimeError("Browser not started. Call start() first.")
        self._page.goto(url, wait_until="domcontentloaded", timeout=30000)
        if self._settings.auto_accept_cookies:
            self._dismiss_cookies()
        time.sleep(self._settings.action_delay)
        return self._page
    
    def try_click_apply_button(self) -> bool:
        """Try to click an apply button or link to reach the application form.

        Returns True if a button was clicked, False otherwise.
        """
        if not self._page:
            return False
        apply_selectors = [
            "a:has-text('Apply here')",
            "a:has-text('Ansök här')",
            "a:has-text('Apply now')",
            "a:has-text('Ansök nu')",
            "a:has-text('Apply')",
            "a:has-text('Ansök')",
            "button:has-text('Apply here')",
            "button:has-text('Ansök här')",
            "button:has-text('Apply now')",
            "button:has-text('Ansök nu')",
            "button:has-text('Apply')",
            "button:has-text('Ansök')",
            "button:has-text('Sök nu')",
            "button:has-text('Sök')",
        ]
        for selector in apply_selectors:
            try:
                btn = self._page.locator(selector).first
                if btn.is_visible(timeout=1000):
                    btn.click()
                    print("  Apply button clicked, loading next page...")
                    self._page.wait_for_load_state("domcontentloaded", timeout=15000)
                    if self._settings.auto_accept_cookies:
                        self._dismiss_cookies()
                    time.sleep(self._settings.action_delay)
                    return True
            except Exception:
                continue
        return False

    def _dismiss_cookies(self) -> None:
        """Try to accept/dismiss common cookie consent banners."""
        if not self._page:
            return
        cookie_selectors = [
            "button:has-text('Accept')",
            "button:has-text('Acceptera')",
            "button:has-text('Godkänn')",
            "button:has-text('Accept all')",
            "button:has-text('Acceptera alla')",
            "button:has-text('Allow all')",
            "button:has-text('I agree')",
            "button:has-text('OK')",
            "button:has-text('Got it')",
            "button:has-text('Agree')",
            "[id*='cookie'] button",
            "[class*='cookie'] button",
            "[id*='consent'] button",
            "[class*='consent'] button",
        ]
        for selector in cookie_selectors:
            try:
                btn = self._page.locator(selector).first
                if btn.is_visible(timeout=1000):
                    btn.click()
                    print("  Cookies accepted.")
                    time.sleep(0.5)
                    return
            except Exception:
                continue

    @property
    def page(self) -> Page:
        if not self._page:
            raise RuntimeError("Browser not started. Call start() first.")
        return self._page

    def close(self) -> None:
        if self._page:
            try:
                self._page.close()
            except Exception:
                pass
            self._page = None
        if self._browser:
            try:
                self._browser.close()
            except Exception:
                pass
            self._browser = None
        if self._playwright:
            try:
                self._playwright.stop()
            except Exception:
                pass
            self._playwright = None
        print("  Browser closed.")
