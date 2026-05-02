#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds and deploys the Filament Tracker to Docker Hub for Synology deployment
.DESCRIPTION
    This script:
    1. Builds the Docker image with all recent fixes
    2. Pushes to Docker Hub (gulvballe/filament-tracker:latest)
    3. Provides instructions for updating on Synology
#>

param(
    [string]$ImageName = "gulvballe/filament-tracker",
    [string]$Tag = "latest"
)

$ErrorActionPreference = "Stop"
$FullImageName = "${ImageName}:${Tag}"

Write-Host "`n=== Filament Tracker Docker Deployment ===" -ForegroundColor Cyan
Write-Host "Image: $FullImageName`n" -ForegroundColor White

# Step 1: Verify we're in the right directory
if (-not (Test-Path "FilamentTracker/Program.cs")) {
    Write-Host "❌ Error: Not in the correct directory!" -ForegroundColor Red
    Write-Host "Please run this script from: C:\Users\Lasse\source\repos\3D-Filament-Tracker-and-Cost-Calculator\" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Found FilamentTracker project" -ForegroundColor Green

# Step 2: Check for Dockerfile
if (-not (Test-Path "Dockerfile")) {
    Write-Host "❌ Error: Dockerfile not found!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Found Dockerfile" -ForegroundColor Green

# Step 3: Build the Docker image
Write-Host "`n[1/3] Building Docker image..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Gray

try {
    docker build -t $FullImageName . 2>&1 | Out-String | Write-Host
    
    if ($LASTEXITCODE -ne 0) {
        throw "Docker build failed"
    }
    
    Write-Host "✅ Docker image built successfully" -ForegroundColor Green
}
catch {
    Write-Host "❌ Failed to build Docker image: $_" -ForegroundColor Red
    exit 1
}

# Step 4: Check Docker Hub login
Write-Host "`n[2/3] Checking Docker Hub authentication..." -ForegroundColor Yellow

try {
    $loginTest = docker info 2>&1 | Out-String
    if ($loginTest -notmatch "Username") {
        Write-Host "⚠️  Not logged in to Docker Hub. Attempting login..." -ForegroundColor Yellow
        docker login
        
        if ($LASTEXITCODE -ne 0) {
            throw "Docker login failed"
        }
    }
    
    Write-Host "✅ Authenticated with Docker Hub" -ForegroundColor Green
}
catch {
    Write-Host "❌ Docker Hub authentication failed: $_" -ForegroundColor Red
    Write-Host "Please run: docker login" -ForegroundColor Yellow
    exit 1
}

# Step 5: Push to Docker Hub
Write-Host "`n[3/3] Pushing to Docker Hub..." -ForegroundColor Yellow
Write-Host "This may take several minutes..." -ForegroundColor Gray

try {
    docker push $FullImageName 2>&1 | Out-String | Write-Host
    
    if ($LASTEXITCODE -ne 0) {
        throw "Docker push failed"
    }
    
    Write-Host "✅ Image pushed successfully to Docker Hub" -ForegroundColor Green
}
catch {
    Write-Host "❌ Failed to push to Docker Hub: $_" -ForegroundColor Red
    exit 1
}

# Success! Provide next steps
Write-Host "`n=== ✅ BUILD AND PUSH COMPLETE ===" -ForegroundColor Green
Write-Host "`nThe new image with these fixes is now available:" -ForegroundColor White
Write-Host "  • MQTT Relay settings persistence fixed" -ForegroundColor Cyan
Write-Host "  • MQTT Relay binds to 0.0.0.0 (accepts external connections)" -ForegroundColor Cyan
Write-Host "  • All settings fields properly saved and loaded" -ForegroundColor Cyan

Write-Host "`n=== NEXT STEPS: UPDATE ON SYNOLOGY ===" -ForegroundColor Yellow
Write-Host "`n1. Open Container Manager on your Synology NAS" -ForegroundColor White
Write-Host "   URL: http://192.168.10.188:5000" -ForegroundColor Gray

Write-Host "`n2. Stop the container:" -ForegroundColor White
Write-Host "   • Select 'filament-tracker' container" -ForegroundColor Gray
Write-Host "   • Click Action → Stop" -ForegroundColor Gray

Write-Host "`n3. Pull the new image:" -ForegroundColor White
Write-Host "   • Go to Registry tab" -ForegroundColor Gray
Write-Host "   • Search: gulvballe/filament-tracker" -ForegroundColor Gray
Write-Host "   • Right-click → Download this image" -ForegroundColor Gray
Write-Host "   • Select tag: latest" -ForegroundColor Gray
Write-Host "   • Wait for download to complete" -ForegroundColor Gray

Write-Host "`n4. Start the container:" -ForegroundColor White
Write-Host "   • Go back to Container tab" -ForegroundColor Gray
Write-Host "   • Select 'filament-tracker'" -ForegroundColor Gray
Write-Host "   • Click Action → Start" -ForegroundColor Gray

Write-Host "`n5. Verify the deployment:" -ForegroundColor White
Write-Host "   • Click Details → Logs" -ForegroundColor Gray
Write-Host "   • Look for: 'MQTT Server instance created, binding to 0.0.0.0:1883'" -ForegroundColor Gray
Write-Host "   • Look for: '✅ MQTT Relay Server successfully started on port 1883'" -ForegroundColor Gray

Write-Host "`n6. Test the connection from your PC:" -ForegroundColor White
Write-Host "   Test-NetConnection 192.168.10.188 -Port 1884" -ForegroundColor Cyan

Write-Host "`n=== TROUBLESHOOTING ===" -ForegroundColor Yellow
Write-Host "`nIf port 1884 still doesn't work after updating:" -ForegroundColor White
Write-Host "  1. Check container logs for MQTT Relay startup messages" -ForegroundColor Gray
Write-Host "  2. Verify MQTT Relay is enabled in Settings page" -ForegroundColor Gray
Write-Host "  3. Verify BambuLab MQTT is connected (required for relay to start)" -ForegroundColor Gray
Write-Host "  4. Add environment variable to force enable:" -ForegroundColor Gray
Write-Host "     MQTT_RELAY_ENABLED=1" -ForegroundColor Cyan

Write-Host "`n=== Image Information ===" -ForegroundColor Cyan
Write-Host "Image Name: $FullImageName" -ForegroundColor White
Write-Host "Build Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
Write-Host "`n"
