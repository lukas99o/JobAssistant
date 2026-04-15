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
        time.sleep(self._settings.action_delay)
        return self._page

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
