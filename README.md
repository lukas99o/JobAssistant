# Job Application Assistant

A Python tool that fetches job listings from Arbetsförmedlingen and helps you apply to them one by one. It opens a browser so you can see what it's doing, fills in application forms using your personal profile, and attaches your CV and personal letter automatically.

## What it does

- Searches jobs on Arbetsförmedlingen via the public JobTech API
- Opens each job's external application page in a browser
- Fills in simple forms (name, email, phone, etc.) using your profile
- Uploads your CV and personal letter to the correct fields
- Pastes your personal letter as text if the form uses a text field
- Accepts cookie banners automatically
- Tracks which jobs you've already processed so they won't appear again
- Prompts you between pages to continue, change files, or start a new search

## Requirements

- Windows
- Python 3.13 — download from [python.org](https://www.python.org/downloads/)

## Setup

**1. Install dependencies**
```
python -m pip install -r requirements.txt
```

**2. Install the browser**
```
python -m playwright install chromium
```

**3. Create your profile**

Copy the example profile and fill in your details:
```
copy config\profile.yaml.example config\profile.yaml
```
Open `config\profile.yaml` and fill in your name, email, phone, and any other details.

**4. Add your documents**

Place your files in the relevant folders:
- `documents\CVs\` — your CV (PDF recommended)
- `documents\PersonalLetters\` — your personal letter (PDF for upload fields, `.txt` for text fields)
- `documents\Other\` — any other attachments (optional)

## Running the application

```
python -m src.main
```

At startup you will:
1. Select which CV, personal letter, and other files to use
2. Enter your search terms (e.g. `python stockholm` or `c# developer`)

The tool then fetches matching jobs and processes them one by one in a browser window.

## Settings

Open `config\settings.yaml` to adjust behaviour:

| Setting | Default | Description |
|---|---|---|
| `auto_submit` | `false` | Set to `true` to submit forms automatically |
| `auto_accept_cookies` | `true` | Automatically dismiss cookie banners |
| `action_delay` | `1.5` | Seconds to wait between actions (increase for slower connections) |
| `browser_slow_mo` | `500` | Milliseconds between browser actions (makes it easier to follow) |
| `api_batch_size` | `25` | Number of jobs to fetch per page |
| `max_simple_form_fields` | `10` | Forms with more fields than this are skipped as too complex |

## Job history

Processed jobs are saved to `data\job_history.json`. Jobs you have already applied to or handled manually will not appear in future searches.
