# Quick Build Script - Minimal version
# Usage: .\run.ps1 [port] [configuration]
# Example: .\run.ps1 5001 Release

param(
    [string]$Port = "5000",
    [string]$Config = "Debug"
)

# Setup logging
$LogFile = "app.log"

# Clear previous log file
if (Test-Path $LogFile) {
    Remove-Item $LogFile
}

# Function to write timestamped messages to both console and log
function Write-LogMessage {
    param([string]$Message, [string]$Color = "White")
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $TimestampedMessage = "[$Timestamp] $Message"
    Write-Host $Message -ForegroundColor $Color
    $TimestampedMessage | Out-File -FilePath $LogFile -Append -Encoding UTF8
}

Write-LogMessage "🚀 Quick Build - Port $Port ($Config)" "Cyan"

# Kill existing processes
Write-LogMessage "🔄 Stopping existing processes..." "Yellow"
taskkill /F /IM CatalogExpertBot.exe 2>&1 | Tee-Object -FilePath $LogFile -Append | Out-Null
Start-Sleep 1

# Build and run
Write-LogMessage "🔨 Building..." "Yellow"
dotnet build catalogexpertagent.sln --configuration $Config --verbosity quiet 2>&1 | Tee-Object -FilePath $LogFile -Append

if ($LASTEXITCODE -eq 0) {
    Write-LogMessage "✅ Build successful - Starting on port $Port" "Green"
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    Write-LogMessage "🌐 Application starting at http://localhost:$Port" "Cyan"
    Write-LogMessage "📝 Logs are being written to: $LogFile" "Gray"
    dotnet run --project CatalogExpertBot.csproj --configuration $Config --no-build 2>&1 | Tee-Object -FilePath $LogFile -Append
} else {
    Write-LogMessage "❌ Build failed - Check $LogFile for details" "Red"
}
