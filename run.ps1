# Run the Job Application Assistant

$dotnetExe = Get-Command dotnet -ErrorAction SilentlyContinue

if (-not $dotnetExe) {
    Write-Error ".NET SDK not found. Install the .NET 10 SDK and try again."
    exit 1
}

Set-Location $PSScriptRoot
& dotnet run --project .\JobAssistant.Console\JobAssistant.Console.csproj -- @args
