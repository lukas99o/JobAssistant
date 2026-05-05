# Job Application Assistant

A .NET 10 console application for working through Arbetsförmedlingen job listings with your existing profile, document, and history files.

## Current Status

The repository has been switched over to the .NET implementation.

Implemented today:
- YAML-based settings and profile loading
- Interactive document selection
- JobTech API search
- Local LLM enrichment of job descriptions via Ollama (`qwen2.5:9b`)
- Job history persistence and deduplication
- Page-by-page browser application flow
- Form analysis, simple-field autofill, and file upload routing
- Root solution, test project, PowerShell launcher, and launcher smoke test

Still evolving:
- Site-specific form heuristics beyond the generic simple-form flow
- Additional edge-case handling for non-standard application pages

## Requirements

- Windows
- .NET 10 SDK
- Ollama
- Ollama model: `qwen2.5:9b`

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

**3. Start Ollama and pull the model**

```powershell
ollama pull qwen2.5:9b
ollama serve
```

The app calls the local Ollama API at `http://127.0.0.1:11434` by default. If Ollama is unavailable, the app falls back to a local summary and keyword extraction path and prints a warning once.

**4. Add your documents**

Place your files in the relevant folders:
- `documents\CVs\` — your CV
- `documents\PersonalLetters\` — your uploadable personal letter, typically PDF
- `documents\PersonalLettersText\` — your plain-text personal letter files for textarea-based forms
- `documents\Other\` — any other attachments

**5. Create job_history.json**

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
1. Select which CV, personal letter, personal-letter text files, and other files to use
2. Prompt for search terms
3. Fetch JobTech results page by page
4. Fetch full job ads and summarize each description with Ollama, plus extract keywords
5. Filter out already processed jobs
6. Open a temporary editor window for personal-letter text before textarea autofill or text-file uploads
7. Open external applications in Playwright, fill simple forms, and route uploads
8. Fall back to manual review for email applications and more complex forms

## Testing

```powershell
dotnet test JobAssistant.slnx --no-build
```

## Settings

Open `config\settings.yaml` to adjust behaviour:

| Setting | Default | Description |
|---|---|---|
| `auto_submit` | `false` | Submit simple forms automatically after autofill |
| `auto_accept_cookies` | `true` | Used by the browser layer |
| `action_delay` | `1.5` | Delay between browser actions |
| `browser_slow_mo` | `500` | Milliseconds between Playwright actions |
| `api_batch_size` | `25` | Number of jobs to fetch per page |
| `ollama_enabled` | `true` | Enable or disable Ollama-based enrichment |
| `ollama_base_url` | `http://127.0.0.1:11434` | Local Ollama API base URL |
| `ollama_model` | `qwen2.5:9b` | Model used for job description summarization and keyword extraction |
| `ollama_timeout_seconds` | `90` | Timeout for each Ollama request |
| `max_simple_form_fields` | `10` | Forms above this field count fall back to manual review |

## Job History

Processed jobs are saved to `data\job_history.json`. Jobs already recorded there are filtered out on later searches.

Each stored job now includes:
- `company_desc`: the summarized job/company description
- `company_keywords`: extracted keywords from the description

Existing history entries using the legacy `company_purpose` field are migrated automatically the next time the history file is loaded.
