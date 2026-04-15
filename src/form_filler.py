from __future__ import annotations

import time
from dataclasses import dataclass, field

from playwright.sync_api import Page, Locator

from .models import UserProfile, SelectedFiles, Settings


@dataclass
class FormField:
    locator: Locator
    tag: str
    input_type: str
    name: str
    label: str
    is_file_upload: bool = False


@dataclass
class FormAnalysis:
    is_simple: bool
    fields: list[FormField] = field(default_factory=list)
    has_file_upload: bool = False
    submit_button: Locator | None = None
    reason: str = ""


# Keywords mapped to profile attributes for heuristic matching
FIELD_MAP: dict[str, str] = {
    "email": "email",
    "e-post": "email",
    "mail": "email",
    "first_name": "first_name",
    "firstname": "first_name",
    "förnamn": "first_name",
    "fornamn": "first_name",
    "last_name": "last_name",
    "lastname": "last_name",
    "efternamn": "last_name",
    "surname": "last_name",
    "name": "full_name",
    "namn": "full_name",
    "phone": "phone",
    "telefon": "phone",
    "tel": "phone",
    "mobile": "phone",
    "mobil": "phone",
    "street": "street",
    "gata": "street",
    "adress": "street",
    "address": "street",
    "city": "city",
    "stad": "city",
    "ort": "city",
    "postal": "postal_code",
    "postnummer": "postal_code",
    "zip": "postal_code",
    "linkedin": "linkedin",
    "github": "github",
    "website": "website",
    "hemsida": "website",
    "url": "website",
    "title": "title",
    "titel": "title",
}


def analyze_page(page: Page, settings: Settings) -> FormAnalysis:
    forms = page.locator("form")
    form_count = forms.count()

    if form_count == 0:
        return FormAnalysis(is_simple=False, reason="No forms found on page")

    # Use the first visible form
    target_form = None
    for i in range(form_count):
        form = forms.nth(i)
        if form.is_visible():
            target_form = form
            break

    if target_form is None:
        return FormAnalysis(is_simple=False, reason="No visible forms found")

    # Gather visible input fields
    fields: list[FormField] = []
    has_file_upload = False

    for selector in ["input", "textarea", "select"]:
        elements = target_form.locator(selector)
        for i in range(elements.count()):
            el = elements.nth(i)
            if not el.is_visible():
                continue

            tag = selector
            input_type = el.get_attribute("type") or "text"

            if input_type in ("hidden", "submit", "button", "reset", "image"):
                continue

            name = el.get_attribute("name") or ""
            el_id = el.get_attribute("id") or ""
            placeholder = el.get_attribute("placeholder") or ""

            # Try to find label
            label_text = ""
            if el_id:
                label = page.locator(f'label[for="{el_id}"]')
                if label.count() > 0:
                    label_text = label.first.inner_text().strip()
            if not label_text:
                label_text = el.get_attribute("aria-label") or placeholder

            is_file = input_type == "file"
            if is_file:
                has_file_upload = True

            fields.append(FormField(
                locator=el,
                tag=tag,
                input_type=input_type,
                name=name,
                label=label_text,
                is_file_upload=is_file,
            ))

    # Find submit button
    submit_btn = None
    for btn_selector in [
        'button[type="submit"]',
        'input[type="submit"]',
        "button:has-text('Submit')",
        "button:has-text('Skicka')",
        "button:has-text('Apply')",
        "button:has-text('Ansök')",
    ]:
        btn = target_form.locator(btn_selector)
        if btn.count() > 0 and btn.first.is_visible():
            submit_btn = btn.first
            break

    non_file_fields = [f for f in fields if not f.is_file_upload]
    is_simple = len(non_file_fields) <= settings.max_simple_form_fields

    reason = ""
    if not is_simple:
        reason = f"Form has {len(non_file_fields)} fields (max {settings.max_simple_form_fields})"

    return FormAnalysis(
        is_simple=is_simple,
        fields=fields,
        has_file_upload=has_file_upload,
        submit_button=submit_btn,
        reason=reason,
    )


def _match_field(f: FormField) -> str | None:
    """Return the profile attribute key that best matches this field, or None."""
    search_text = f"{f.name} {f.label}".lower()
    for keyword, attr in FIELD_MAP.items():
        if keyword in search_text:
            return attr
    return None


def _get_profile_value(profile: UserProfile, attr: str) -> str:
    if attr == "full_name":
        return profile.full_name
    return getattr(profile, attr, "")


def fill_form(
    page: Page,
    analysis: FormAnalysis,
    profile: UserProfile,
    selected_files: SelectedFiles,
    settings: Settings,
) -> bool:
    if not analysis.is_simple:
        print(f"  Complex form detected: {analysis.reason}. Skipping.")
        return False

    filled_count = 0

    for f in analysis.fields:
        if f.is_file_upload:
            # Attach documents to file upload fields
            files_to_upload = []
            if selected_files.cv_path and selected_files.cv_path.exists():
                files_to_upload.append(str(selected_files.cv_path))
            if selected_files.personal_letter_path and selected_files.personal_letter_path.exists():
                files_to_upload.append(str(selected_files.personal_letter_path))
            for p in selected_files.other_paths:
                if p.exists():
                    files_to_upload.append(str(p))

            if files_to_upload:
                try:
                    f.locator.set_input_files(files_to_upload)
                    names = ", ".join(p.split("\\")[-1].split("/")[-1] for p in files_to_upload)
                    print(f"    Attached: {names}")
                    filled_count += 1
                except Exception as e:
                    print(f"    Failed to attach files: {e}")
            continue

        attr = _match_field(f)
        if not attr:
            continue

        value = _get_profile_value(profile, attr)
        if not value:
            continue

        try:
            if f.tag == "select":
                # Try to select by visible text
                f.locator.select_option(label=value)
            else:
                f.locator.fill(value)
            print(f"    Filled '{f.label or f.name}' → {attr}")
            filled_count += 1
            time.sleep(0.3)
        except Exception as e:
            print(f"    Could not fill '{f.label or f.name}': {e}")

    print(f"  Filled {filled_count} field(s).")

    if settings.auto_submit and analysis.submit_button:
        print("  Auto-submitting form...")
        analysis.submit_button.click()
        time.sleep(settings.action_delay)
        print("  Form submitted.")
    elif not settings.auto_submit:
        print("  Review the form and submit manually.")

    return True
