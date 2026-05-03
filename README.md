# Job Application Assistant

A .NET 10 console application for working through Arbetsförmedlingen job listings with your existing profile, document, and history files.

## Current Status

The repository has been switched over to the .NET implementation.

Implemented today:
- YAML-based settings and profile loading
- Interactive document selection
- JobTech API search
- Job history persistence and deduplication
- Playwright-based browser manager foundation
- Root solution, test project, and PowerShell launcher

Still being ported:
- Form analysis and auto-fill heuristics
- File-upload routing
- End-to-end browser application flow

## Requirements

- Windows
- .NET 10 SDK

## Setup

**1. Build the solution**
```powershell
dotnet build JobAssistant.slnx
```

**2. Create your profile**

Copy the example profile and fill in your details:
```powershell
Copy-Item config\profile.yaml.example config\profile.yaml
```

Open `config\profile.yaml` and fill in your name, email, phone, and any other details.

**3. Add your documents**

Place your files in the relevant folders:
- `documents\CVs\` — your CV
- `documents\PersonalLetters\` — your personal letter
- `documents\Other\` — any other attachments

**4. Create job_history.json**

Copy the example file and rename it to `job_history.json`.

## Running the application

```powershell
.\run.ps1
```

Or run the console project directly:

```powershell
dotnet run --project .\JobAssistant.Console\JobAssistant.Console.csproj
```

The current .NET console flow will:
1. Select which CV, personal letter, and other files to use
2. Prompt for search terms
3. Fetch the first JobTech results page
4. Filter out already processed jobs and show the new ones

## Testing

```powershell
dotnet test JobAssistant.slnx --no-build
```

## Settings

Open `config\settings.yaml` to adjust behaviour:

| Setting | Default | Description |
|---|---|---|
| `auto_submit` | `false` | Reserved for the form-submission port |
| `auto_accept_cookies` | `true` | Used by the browser layer |
| `action_delay` | `1.5` | Delay between browser actions |
| `browser_slow_mo` | `500` | Milliseconds between Playwright actions |
| `api_batch_size` | `25` | Number of jobs to fetch per page |
| `max_simple_form_fields` | `10` | Reserved for the form-analysis port |

## Job History

Processed jobs are saved to `data\job_history.json`. Jobs already recorded there are filtered out on later searches.
