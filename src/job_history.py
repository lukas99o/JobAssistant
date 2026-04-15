from __future__ import annotations

import json
from datetime import date
from pathlib import Path

from .models import JobHistoryRecord, JobListing


class JobHistoryStore:
    def __init__(self, path: Path):
        self._path = path
        self._records: dict[str, dict] = {}
        self.load()

    def load(self) -> None:
        if self._path.exists():
            with open(self._path, "r", encoding="utf-8") as f:
                self._records = json.load(f)
        else:
            self._records = {}
            self._path.parent.mkdir(parents=True, exist_ok=True)
            self.save()

    def save(self) -> None:
        with open(self._path, "w", encoding="utf-8") as f:
            json.dump(self._records, f, indent=2, ensure_ascii=False)

    def is_processed(self, job_id: str) -> bool:
        return job_id in self._records

    def record(self, job: JobListing, status: str, search_query: str) -> None:
        entry = JobHistoryRecord(
            job_id=job.id,
            company_name=job.employer_name,
            headline=job.headline,
            company_purpose=job.company_purpose,
            summary=job.summary,
            last_search_date=date.today().isoformat(),
            search_query=search_query,
            status=status,
        )
        self._records[job.id] = entry.to_dict()
        self.save()

    def filter_new(self, jobs: list[JobListing]) -> tuple[list[JobListing], int]:
        """Return (new_jobs, skipped_count)."""
        new_jobs = [j for j in jobs if not self.is_processed(j.id)]
        skipped = len(jobs) - len(new_jobs)
        return new_jobs, skipped

    def get_stats(self) -> dict[str, int]:
        stats: dict[str, int] = {"applied": 0, "skipped": 0, "manual": 0, "total": 0}
        for rec in self._records.values():
            status = rec.get("status", "skipped")
            stats[status] = stats.get(status, 0) + 1
            stats["total"] += 1
        return stats
