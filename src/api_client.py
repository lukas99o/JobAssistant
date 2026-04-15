from __future__ import annotations

import time

import httpx

from .models import JobListing, Settings


class JobSearchClient:
    def __init__(self, settings: Settings):
        self._base_url = settings.api_base_url
        self._batch_size = settings.api_batch_size
        self._client = httpx.Client(timeout=30.0)

    def search(
        self, query: str, offset: int = 0, limit: int | None = None
    ) -> tuple[list[JobListing], int]:
        limit = limit or self._batch_size
        params = {"q": query, "offset": offset, "limit": limit}
        headers = {
            "Accept": "application/json",
        }

        response = self._request("/search", params=params, headers=headers)
        total = response.get("total", {}).get("value", 0)
        hits = response.get("hits", [])
        jobs = [JobListing.from_api_response(hit) for hit in hits]
        return jobs, total

    def get_ad(self, job_id: str) -> JobListing | None:
        try:
            data = self._request(f"/ad/{job_id}")
            return JobListing.from_api_response(data)
        except Exception:
            return None

    def _request(
        self, path: str, params: dict | None = None, headers: dict | None = None
    ) -> dict:
        url = f"{self._base_url}{path}"
        max_retries = 3

        for attempt in range(max_retries):
            try:
                resp = self._client.get(url, params=params, headers=headers)

                if resp.status_code == 429:
                    wait = 2 ** (attempt + 1)
                    print(f"  Rate limited. Waiting {wait}s...")
                    time.sleep(wait)
                    continue

                resp.raise_for_status()
                return resp.json()

            except httpx.HTTPStatusError as e:
                if attempt < max_retries - 1 and e.response.status_code >= 500:
                    time.sleep(2)
                    continue
                raise
            except httpx.RequestError as e:
                if attempt < max_retries - 1:
                    time.sleep(2)
                    continue
                raise

        return {}

    def close(self) -> None:
        self._client.close()
