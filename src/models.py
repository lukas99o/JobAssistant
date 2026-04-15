from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from datetime import date


@dataclass
class JobListing:
    id: str
    headline: str
    employer_name: str
    description: str
    application_url: str | None
    application_email: str | None
    application_info: str | None
    workplace_city: str | None
    published_date: str | None
    last_apply_date: str | None

    @classmethod
    def from_api_response(cls, data: dict) -> JobListing:
        app = data.get("application_details") or {}
        employer = data.get("employer") or {}
        workplace = data.get("workplace_address") or {}
        desc = data.get("description") or {}

        return cls(
            id=str(data.get("id", "")),
            headline=data.get("headline", ""),
            employer_name=employer.get("name", "Unknown"),
            description=desc.get("text", "") if isinstance(desc, dict) else str(desc),
            application_url=app.get("url"),
            application_email=app.get("email"),
            application_info=app.get("information"),
            workplace_city=workplace.get("city") or workplace.get("municipality"),
            published_date=data.get("publication_date"),
            last_apply_date=data.get("last_publication_date"),
        )

    @property
    def application_method(self) -> str:
        if self.application_url:
            return "external"
        if self.application_email:
            return "email"
        return "none"

    @property
    def company_purpose(self) -> str:
        """Extract a brief company purpose from the description (first 200 chars)."""
        if not self.description:
            return ""
        text = self.description.strip().replace("\n", " ")
        return text[:200] + ("..." if len(text) > 200 else "")

    @property
    def summary(self) -> str:
        return f"{self.headline} at {self.employer_name}"


@dataclass
class JobHistoryRecord:
    job_id: str
    company_name: str
    headline: str
    company_purpose: str
    summary: str
    last_search_date: str
    search_query: str
    status: str  # "applied", "skipped", "manual"

    def to_dict(self) -> dict:
        return {
            "job_id": self.job_id,
            "company_name": self.company_name,
            "headline": self.headline,
            "company_purpose": self.company_purpose,
            "summary": self.summary,
            "last_search_date": self.last_search_date,
            "search_query": self.search_query,
            "status": self.status,
        }

    @classmethod
    def from_dict(cls, data: dict) -> JobHistoryRecord:
        return cls(**data)


@dataclass
class SelectedFiles:
    cv_path: Path | None = None
    personal_letter_path: Path | None = None
    other_paths: list[Path] = field(default_factory=list)

    def display(self) -> str:
        lines = []
        lines.append(f"  CV: {self.cv_path.name if self.cv_path else 'None'}")
        lines.append(f"  Personal letter: {self.personal_letter_path.name if self.personal_letter_path else 'None'}")
        if self.other_paths:
            names = ", ".join(p.name for p in self.other_paths)
            lines.append(f"  Other files: {names}")
        else:
            lines.append("  Other files: None")
        return "\n".join(lines)


@dataclass
class UserProfile:
    first_name: str = ""
    last_name: str = ""
    email: str = ""
    phone: str = ""
    street: str = ""
    city: str = ""
    postal_code: str = ""
    country: str = ""
    title: str = ""
    professional_summary: str = ""
    linkedin: str = ""
    github: str = ""
    website: str = ""

    @classmethod
    def from_yaml(cls, data: dict) -> UserProfile:
        personal = data.get("personal") or {}
        address = personal.get("address") or {}
        professional = data.get("professional") or {}
        links = data.get("links") or {}

        return cls(
            first_name=personal.get("first_name", ""),
            last_name=personal.get("last_name", ""),
            email=personal.get("email", ""),
            phone=personal.get("phone", ""),
            street=address.get("street", ""),
            city=address.get("city", ""),
            postal_code=address.get("postal_code", ""),
            country=address.get("country", ""),
            title=professional.get("title", ""),
            professional_summary=professional.get("summary", ""),
            linkedin=links.get("linkedin", ""),
            github=links.get("github", ""),
            website=links.get("website", ""),
        )

    @property
    def full_name(self) -> str:
        return f"{self.first_name} {self.last_name}".strip()

    def validate(self) -> list[str]:
        warnings = []
        if not self.first_name:
            warnings.append("First name is empty")
        if not self.last_name:
            warnings.append("Last name is empty")
        if not self.email:
            warnings.append("Email is empty")
        if not self.phone:
            warnings.append("Phone is empty")
        return warnings


@dataclass
class Settings:
    action_delay: float = 1.5
    api_batch_size: int = 25
    api_base_url: str = "https://jobsearch.api.jobtechdev.se"
    browser_headless: bool = False
    browser_slow_mo: int = 500
    auto_submit: bool = False
    max_simple_form_fields: int = 10

    @classmethod
    def from_yaml(cls, data: dict) -> Settings:
        return cls(
            action_delay=data.get("action_delay", 1.5),
            api_batch_size=data.get("api_batch_size", 25),
            api_base_url=data.get("api_base_url", "https://jobsearch.api.jobtechdev.se"),
            browser_headless=data.get("browser_headless", False),
            browser_slow_mo=data.get("browser_slow_mo", 500),
            auto_submit=data.get("auto_submit", False),
            max_simple_form_fields=data.get("max_simple_form_fields", 10),
        )
