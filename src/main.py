from __future__ import annotations

import signal
import sys
import time
from pathlib import Path

import yaml

from .api_client import JobSearchClient
from .browser import BrowserManager
from .file_selector import select_files
from .form_filler import analyze_page, fill_form
from .job_history import JobHistoryStore
from .models import JobListing, SelectedFiles, Settings, UserProfile


BASE_DIR = Path(__file__).resolve().parent.parent
CONFIG_DIR = BASE_DIR / "config"
DATA_DIR = BASE_DIR / "data"
DOCUMENTS_DIR = BASE_DIR / "documents"


class SessionStats:
    def __init__(self):
        self.processed = 0
        self.applied = 0
        self.skipped = 0
        self.manual = 0

    def display(self) -> None:
        print("\n=== Session Summary ===")
        print(f"  Processed: {self.processed}")
        print(f"  Applied (form filled): {self.applied}")
        print(f"  Manual (email/complex form): {self.manual}")
        print(f"  Skipped: {self.skipped}")


def load_settings() -> Settings:
    path = CONFIG_DIR / "settings.yaml"
    if path.exists():
        with open(path, "r", encoding="utf-8") as f:
            return Settings.from_yaml(yaml.safe_load(f) or {})
    return Settings()


def load_profile() -> UserProfile:
    path = CONFIG_DIR / "profile.yaml"
    if not path.exists():
        print("Warning: profile.yaml not found. Form filling will be limited.")
        return UserProfile()
    with open(path, "r", encoding="utf-8") as f:
        data = yaml.safe_load(f) or {}
    profile = UserProfile.from_yaml(data)
    warnings = profile.validate()
    if warnings:
        print("Profile warnings:")
        for w in warnings:
            print(f"  - {w}")
    return profile


def prompt_search_query() -> str:
    while True:
        query = input("\nEnter search terms (e.g. 'python stockholm'): ").strip()
        if query:
            return query
        print("Search terms cannot be empty.")


def end_of_page_menu(has_more: bool) -> str:
    print("\n=== Page Complete ===")
    print("What would you like to do?")
    if has_more:
        print("  1. Continue to next page")
        print("  2. Select new files and continue to next page")
        print("  3. Select new files and start a new job search")
        print("  4. Start a new job search (keep current files)")
        valid = {"1", "2", "3", "4"}
    else:
        print("  No more results for this search.")
        print("  1. Select new files and start a new job search")
        print("  2. Start a new job search (keep current files)")
        valid = {"1", "2"}

    while True:
        choice = input(f"Choice [{'/'.join(sorted(valid))}]: ").strip()
        if choice in valid:
            return choice
        print(f"Invalid choice. Enter {' or '.join(sorted(valid))}.")


def process_job(
    job: JobListing,
    index: int,
    total: int,
    browser: BrowserManager,
    profile: UserProfile,
    selected_files: SelectedFiles,
    settings: Settings,
    history: JobHistoryStore,
    query: str,
    stats: SessionStats,
) -> None:
    city = job.workplace_city or "Unknown location"
    print(f"\n[{index}/{total}] {job.headline}")
    print(f"  Employer: {job.employer_name} — {city}")

    method = job.application_method

    if method == "external":
        print(f"  Application URL: {job.application_url}")
        try:
            page = browser.navigate(job.application_url)
            time.sleep(settings.action_delay)

            analysis = analyze_page(page, settings)

            if analysis.is_simple:
                filled = fill_form(page, analysis, profile, selected_files, settings)
                if filled:
                    if not settings.auto_submit:
                        input("  Press Enter when done to continue...")
                    history.record(job, "applied", query)
                    stats.applied += 1
                else:
                    stats.skipped += 1
            else:
                print(f"  {analysis.reason}")
                fill_form(page, analysis, profile, selected_files, settings, force_manual=True)
                input("  Complete any remaining fields and submit manually, then press Enter to continue...")
                history.record(job, "manual", query)
                stats.manual += 1

        except Exception as e:
            print(f"  Error navigating to application: {e}")
            stats.skipped += 1

    elif method == "email":
        print(f"  Email application: {job.application_email}")
        if job.application_info:
            print(f"  Instructions: {job.application_info}")
        input("  Complete the application manually, then press Enter to continue...")
        history.record(job, "manual", query)
        stats.manual += 1

    else:
        print("  No application method found. Skipping.")
        if job.application_info:
            print(f"  Info: {job.application_info}")
        stats.skipped += 1

    stats.processed += 1
    time.sleep(settings.action_delay)


def main() -> None:
    print("=" * 50)
    print("  Job Application Assistant")
    print("  Arbetsförmedlingen / JobTech API")
    print("=" * 50)

    settings = load_settings()
    profile = load_profile()
    history = JobHistoryStore(DATA_DIR / "job_history.json")

    print(f"\nAuto-submit: {'ON' if settings.auto_submit else 'OFF'}")

    # Statistics
    stats = SessionStats()

    # Browser manager
    browser = BrowserManager(settings)

    # Graceful shutdown
    def shutdown(sig=None, frame=None):
        print("\n\nShutting down...")
        stats.display()
        browser.close()
        api_client.close()
        sys.exit(0)

    signal.signal(signal.SIGINT, shutdown)

    # Select documents
    selected_files = select_files(DOCUMENTS_DIR)

    # API client
    api_client = JobSearchClient(settings)
    query = prompt_search_query()

    # Start browser
    browser.start()

    try:
        offset = 0

        while True:
            # Fetch jobs
            print(f"\nSearching: '{query}' (offset {offset})...")
            jobs, total = api_client.search(query, offset=offset)

            if not jobs:
                print("No jobs found.")
                choice = end_of_page_menu(has_more=False)
                if choice == "3":
                    selected_files = select_files(DOCUMENTS_DIR)
                    query = prompt_search_query()
                    offset = 0
                    continue
                elif choice == "4":
                    query = prompt_search_query()
                    offset = 0
                    continue

            # Filter already processed
            new_jobs, skipped_count = history.filter_new(jobs)
            print(f"Found {len(jobs)} jobs ({len(new_jobs)} new, {skipped_count} already processed)")

            if not new_jobs:
                print("All jobs on this page were already processed.")
                has_more = offset + settings.api_batch_size < total
                choice = end_of_page_menu(has_more=has_more)
                if choice == "1":
                    offset += settings.api_batch_size
                    continue
                elif choice == "2":
                    selected_files = select_files(DOCUMENTS_DIR)
                    offset += settings.api_batch_size
                    continue
                elif choice == "3":
                    selected_files = select_files(DOCUMENTS_DIR)
                    query = prompt_search_query()
                    offset = 0
                    continue
                elif choice == "4":
                    query = prompt_search_query()
                    offset = 0
                    continue

            # Process each new job
            for i, job in enumerate(new_jobs, 1):
                process_job(
                    job, i, len(new_jobs),
                    browser, profile, selected_files, settings,
                    history, query, stats,
                )

            # End of page menu
            has_more = offset + settings.api_batch_size < total
            choice = end_of_page_menu(has_more=has_more)

            if choice == "1":
                offset += settings.api_batch_size
            elif choice == "2":
                selected_files = select_files(DOCUMENTS_DIR)
                offset += settings.api_batch_size
            elif choice == "3":
                selected_files = select_files(DOCUMENTS_DIR)
                query = prompt_search_query()
                offset = 0
            elif choice == "4":
                query = prompt_search_query()
                offset = 0

    except KeyboardInterrupt:
        pass
    finally:
        stats.display()
        browser.close()
        api_client.close()


if __name__ == "__main__":
    main()
