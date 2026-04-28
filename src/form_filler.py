from __future__ import annotations

import re
import time
from dataclasses import dataclass, field
from pathlib import Path

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
    question_label: str = ""  # enclosing fieldset legend, for radio/checkbox groups


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
    "gatuadress": "street",
    "adress": "street",
    "address": "street",
    "city": "city",
    "stad": "city",
    "ort": "city",
    "bostadsort": "city",
    "postal": "postal_code",
    "postnummer": "postal_code",
    "zip": "postal_code",
    "country": "country",
    "land": "country",
    "linkedin": "linkedin",
    "github": "github",
    "website": "website",
    "hemsida": "website",
    "url": "website",
    "title": "title",
    "titel": "title",
    "role": "title",
    "befattning": "title",
    "position": "title",
    "organisation": "organization",
    "organization": "organization",
}

# Yes/No questions answered automatically (can be overridden in profile form_answers)
YES_NO_DEFAULTS: dict[str, str] = {
    "skallkrav": "Yes",
    "meriterande": "Yes",
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

            # For radio/checkbox, try to capture the enclosing question text
            question_label = ""
            if input_type in ("radio", "checkbox"):
                try:
                    question_label = el.evaluate(
                        """e => {
                            const fs = e.closest('fieldset');
                            if (fs) {
                                const legend = fs.querySelector('legend');
                                if (legend) return legend.textContent.trim();
                            }
                            // Fallback: look for a preceding sibling label/p/span
                            let node = e.parentElement;
                            while (node) {
                                const q = node.querySelector('label, legend, p, span');
                                if (q && q.textContent.trim()) return q.textContent.trim();
                                node = node.parentElement;
                                if (node && node.tagName === 'FORM') break;
                            }
                            return '';
                        }"""
                    ) or ""
                except Exception:
                    pass

            fields.append(FormField(
                locator=el,
                tag=tag,
                input_type=input_type,
                name=name,
                label=label_text,
                is_file_upload=is_file,
                question_label=question_label,
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
    search_text = f"{f.name} {f.label} {f.question_label}".lower()
    for keyword, attr in FIELD_MAP.items():
        if keyword in search_text:
            return attr
    return None


def _get_profile_value(profile: UserProfile, attr: str) -> str:
    if attr == "full_name":
        return profile.full_name
    return getattr(profile, attr, "")


# Keywords to identify file upload field purpose
CV_KEYWORDS = ["cv", "resume", "curriculum", "meritförteckning", "cv/resume"]
LETTER_KEYWORDS = ["personligt brev", "personal letter", "cover letter", "brev", "motivational", "motivation"]
OTHER_KEYWORDS = ["other", "övrigt", "övriga", "additional", "attachment", "bilaga", "dokument"]


def _classify_file_upload(f: FormField) -> str:
    """Classify a file upload field as 'cv', 'letter', or 'other'.

    Label text is checked first since it is semantically more reliable than
    the raw HTML name attribute. 'cv' is matched as a whole word to avoid
    false positives from name attributes like 'cv_other' or 'recv'.
    """
    label = f.label.lower()
    name = f.name.lower()

    # Check label first — it reflects what the user sees
    for kw in LETTER_KEYWORDS:
        if kw in label:
            return "letter"
    for kw in OTHER_KEYWORDS:
        if kw in label:
            return "other"
    # 'cv' requires a word boundary; other CV keywords are long enough to be safe
    if re.search(r'\bcv\b', label) or any(kw in label for kw in CV_KEYWORDS[1:]):
        return "cv"

    # Fall back to name attribute with the same precedence
    for kw in LETTER_KEYWORDS:
        if kw in name:
            return "letter"
    for kw in OTHER_KEYWORDS:
        if kw in name:
            return "other"
    if re.search(r'\bcv\b', name) or any(kw in name for kw in CV_KEYWORDS[1:]):
        return "cv"

    return "other"


def _is_personal_letter_textarea(f: FormField) -> bool:
    """Check if a textarea is meant for personal letter / cover letter text."""
    search_text = f"{f.name} {f.label}".lower()
    for kw in LETTER_KEYWORDS:
        if kw in search_text:
            return True
    return False


def _read_text_file(path: Path) -> str:
    """Read text content from a file. Supports .txt files."""
    try:
        return path.read_text(encoding="utf-8").strip()
    except Exception:
        return ""


def _match_form_answer(f: FormField, profile: UserProfile) -> str | None:
    """Try to match a field against predefined form_answers in the profile."""
    if not profile.form_answers:
        return None

    search_text = f"{f.name} {f.label} {f.question_label}".lower()

    # Check language answers
    languages = profile.form_answers.get("languages") or {}
    for lang, value in languages.items():
        if lang.lower() in search_text:
            return str(value)

    # Check yes/no answers
    yes_no = profile.form_answers.get("yes_no") or {}
    for keyword, value in yes_no.items():
        if keyword.lower().replace("_", " ") in search_text or keyword.lower() in search_text:
            return str(value)

    # Check freeform text answers
    text_answers = profile.form_answers.get("text") or {}
    for keyword, value in text_answers.items():
        if keyword.lower().replace("_", " ") in search_text or keyword.lower() in search_text:
            return str(value)

    return None


def _match_yes_no_default(f: FormField, profile: UserProfile) -> str | None:
    """Return hardcoded default answer for well-known yes/no questions.

    Profile form_answers can override any default by defining the same keyword
    under yes_no — they are checked first in fill_form before this function.
    """
    search_text = f"{f.name} {f.label} {f.question_label}".lower()
    for keyword, answer in YES_NO_DEFAULTS.items():
        if keyword in search_text:
            return answer
    return None


def fill_form(
    page: Page,
    analysis: FormAnalysis,
    profile: UserProfile,
    selected_files: SelectedFiles,
    settings: Settings,
    force_manual: bool = False,
) -> bool:
    if not analysis.is_simple:
        print(f"  Complex form detected: {analysis.reason}. Attempting partial autofill.")

    filled_count = 0

    for f in analysis.fields:
        if f.is_file_upload:
            # Route files to the correct upload field based on label
            upload_type = _classify_file_upload(f)
            file_to_upload = None

            if upload_type == "cv" and selected_files.cv_path and selected_files.cv_path.exists():
                file_to_upload = [str(selected_files.cv_path)]
            elif upload_type == "letter" and selected_files.personal_letter_path and selected_files.personal_letter_path.exists():
                file_to_upload = [str(selected_files.personal_letter_path)]
            elif upload_type == "other" and selected_files.other_paths:
                file_to_upload = [str(p) for p in selected_files.other_paths if p.exists()]
            else:
                # Fallback: if field is unclassified, try CV first
                if selected_files.cv_path and selected_files.cv_path.exists():
                    file_to_upload = [str(selected_files.cv_path)]

            if file_to_upload:
                try:
                    f.locator.set_input_files(file_to_upload)
                    names = ", ".join(p.split("\\")[-1].split("/")[-1] for p in file_to_upload)
                    print(f"    Attached ({upload_type}): {names}")
                    filled_count += 1
                except Exception as e:
                    print(f"    Failed to attach files: {e}")
            continue

        # Check if this is a textarea meant for personal letter text
        if f.tag == "textarea" and _is_personal_letter_textarea(f):
            if selected_files.personal_letter_path and selected_files.personal_letter_path.exists():
                letter_text = _read_text_file(selected_files.personal_letter_path)
                if letter_text:
                    try:
                        f.locator.fill(letter_text)
                        print(f"    Filled personal letter text from {selected_files.personal_letter_path.name}")
                        filled_count += 1
                        time.sleep(0.3)
                    except Exception as e:
                        print(f"    Could not fill personal letter text: {e}")
            continue

        # Try profile field match first
        attr = _match_field(f)
        if attr:
            value = _get_profile_value(profile, attr)
            if value:
                try:
                    if f.tag == "select":
                        f.locator.select_option(label=value)
                    else:
                        f.locator.fill(value)
                    print(f"    Filled '{f.label or f.name}' → {attr}")
                    filled_count += 1
                    time.sleep(0.3)
                except Exception as e:
                    print(f"    Could not fill '{f.label or f.name}': {e}")
                continue

        # Try predefined form answers
        answer = _match_form_answer(f, profile)
        if answer:
            try:
                if f.tag == "select":
                    f.locator.select_option(label=answer)
                elif f.input_type in ("checkbox", "radio"):
                    if answer.lower() in ("yes", "true", "ja"):
                        if not f.locator.is_checked():
                            f.locator.check()
                    elif answer.lower() in ("no", "false", "nej"):
                        if f.locator.is_checked():
                            f.locator.uncheck()
                else:
                    f.locator.fill(answer)
                print(f"    Filled '{f.label or f.name}' → form_answer: {answer}")
                filled_count += 1
                time.sleep(0.3)
            except Exception as e:
                print(f"    Could not fill '{f.label or f.name}': {e}")
            continue

        # Try hardcoded yes/no defaults (e.g. "matchar du skallkraven" → Yes)
        default_answer = _match_yes_no_default(f, profile)
        if default_answer and f.input_type in ("checkbox", "radio", "text", "select-one"):
            try:
                if f.tag == "select":
                    f.locator.select_option(label=default_answer)
                elif f.input_type in ("checkbox", "radio"):
                    if default_answer.lower() in ("yes", "true", "ja"):
                        if not f.locator.is_checked():
                            f.locator.check()
                else:
                    f.locator.fill(default_answer)
                print(f"    Filled '{f.label or f.name}' → default: {default_answer}")
                filled_count += 1
                time.sleep(0.3)
            except Exception as e:
                print(f"    Could not fill '{f.label or f.name}': {e}")

    print(f"  Filled {filled_count} field(s).")

    should_auto_submit = settings.auto_submit and analysis.is_simple and not force_manual

    if should_auto_submit and analysis.submit_button:
        print("  Auto-submitting form...")
        analysis.submit_button.click()
        time.sleep(settings.action_delay)
        print("  Form submitted.")
    else:
        print("  Review the form and submit manually.")

    return True
