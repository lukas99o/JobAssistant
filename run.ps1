# Run the Job Application Assistant
# Uses the real Python 3.13 explicitly, bypassing the Windows Store alias

$pythonExe = "C:\Users\Lukas\AppData\Local\Programs\Python\Python313\python.exe"

if (-not (Test-Path $pythonExe)) {
    Write-Error "Python not found at $pythonExe. Please reinstall Python 3.13."
    exit 1
}

Set-Location $PSScriptRoot
& $pythonExe -m src.main
