# Quick Build Script - Minimal version
# Usage: .\run.ps1 [port] [configuration]
# Example: .\run.ps1 5001 Release

param(
    [string]$Port = "5000",
    [string]$Config = "Debug"
)

# Setup logging
$LogFile = "app.log"

# Kill existing processes FIRST (before trying to access log file)
Write-Host "🔄 Stopping existing processes..." -ForegroundColor Yellow
taskkill /F /IM CatalogExpertBot.exe 2>$null
taskkill /F /IM dotnet.exe /FI "WINDOWTITLE eq *CatalogExpertBot*" 2>$null
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.MainModule.FileName -like "*catalogexpertagent*" } | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep 2

# Clear previous log file (after processes are stopped)
if (Test-Path $LogFile) {
    try {
        Remove-Item $LogFile -Force
    } catch {
        # If file is locked, try to rename it first
        if (Test-Path "$LogFile.old") { Remove-Item "$LogFile.old" -Force -ErrorAction SilentlyContinue }
        Rename-Item $LogFile "$LogFile.old" -ErrorAction SilentlyContinue
    }
}

# Function to write timestamped messages to both console and log
function Write-LogMessage {
    param([string]$Message, [string]$Color = "White")
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $TimestampedMessage = "[$Timestamp] $Message"
    Write-Host $Message -ForegroundColor $Color
    try {
        $TimestampedMessage | Out-File -FilePath $LogFile -Append -Encoding UTF8
    } catch {
        # If still can't write to log, continue without logging to file
    }
}

Write-LogMessage "🚀 Quick Build - Port $Port ($Config)" "Cyan"

# Build and run
Write-LogMessage "🔨 Building..." "Yellow"
try {
    dotnet build catalogexpertagent.sln --configuration $Config --verbosity quiet 2>&1 | Tee-Object -FilePath $LogFile -Append
} catch {
    # If can't write to log file, just run the build
    dotnet build catalogexpertagent.sln --configuration $Config --verbosity quiet
}

if ($LASTEXITCODE -eq 0) {
    Write-LogMessage "✅ Build successful - Starting on port $Port" "Green"
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    Write-LogMessage "🌐 Application starting at http://localhost:$Port" "Cyan"
    Write-LogMessage "📝 Logs are being written to: $LogFile" "Gray"
    dotnet run --project CatalogExpertBot.csproj --configuration $Config --no-build 2>&1 | Tee-Object -FilePath $LogFile -Append
} else {
    Write-LogMessage "❌ Build failed - Check $LogFile for details" "Red"
}
