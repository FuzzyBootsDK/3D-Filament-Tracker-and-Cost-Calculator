# Filament Tracker - Docker Deployment Script
# This script builds and deploys the Filament Tracker app in Docker

param(
    [switch]$NoBrowser,
    [switch]$Clean,
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }

# Banner
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                                                        ║" -ForegroundColor Cyan
Write-Host "║          Filament Tracker v2.0 - Docker Deploy         ║" -ForegroundColor Cyan
Write-Host "║                                                        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is installed
Write-Info "🔍 Checking Docker installation..."
try {
    $dockerVersion = docker --version
    Write-Success "✓ Docker is installed: $dockerVersion"
} catch {
    Write-Error "✗ Docker is not installed or not in PATH"
    Write-Warning ""
    Write-Warning "Please install Docker Desktop from:"
    Write-Warning "https://www.docker.com/products/docker-desktop"
    Write-Warning ""
    exit 1
}

# Check if Docker is running
Write-Info "🔍 Checking if Docker daemon is running..."
try {
    docker ps | Out-Null
    Write-Success "✓ Docker daemon is running"
} catch {
    Write-Error "✗ Docker daemon is not running"
    Write-Warning ""
    Write-Warning "Please start Docker Desktop and try again."
    Write-Warning ""
    exit 1
}

# Get script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Container and image names
$imageName = "filament-tracker"
$containerName = "filament-tracker"
$volumeName = "filament-tracker-data"

# Clean up old containers/images if requested
if ($Clean) {
    Write-Info "🧹 Cleaning up old containers and images..."
    
    # Stop and remove container
    $existingContainer = docker ps -a --filter "name=$containerName" --format "{{.Names}}"
    if ($existingContainer -eq $containerName) {
        Write-Info "   Stopping container: $containerName"
        docker stop $containerName | Out-Null
        Write-Info "   Removing container: $containerName"
        docker rm $containerName | Out-Null
        Write-Success "✓ Removed old container"
    }
    
    # Remove old images
    $existingImage = docker images --filter "reference=$imageName" --format "{{.Repository}}"
    if ($existingImage) {
        Write-Info "   Removing old image: $imageName"
        docker rmi $imageName --force | Out-Null
        Write-Success "✓ Removed old image"
    }
    
    # Remove volume (WARNING: This deletes all data!)
    Write-Warning "⚠️  Remove database volume? This will DELETE ALL DATA! (y/N)"
    $confirmation = Read-Host
    if ($confirmation -eq 'y' -or $confirmation -eq 'Y') {
        docker volume rm $volumeName 2>$null | Out-Null
        Write-Success "✓ Removed database volume"
    }
} else {
    # Stop and remove existing container (but keep data volume)
    $existingContainer = docker ps -a --filter "name=$containerName" --format "{{.Names}}"
    if ($existingContainer -eq $containerName) {
        Write-Info "🛑 Stopping existing container..."
        docker stop $containerName | Out-Null
        Write-Info "🗑️  Removing old container..."
        docker rm $containerName | Out-Null
        Write-Success "✓ Removed old container (data preserved)"
    }
}

Write-Host ""
Write-Info "🏗️  Building Docker image..."
Write-Info "   This may take a few minutes on first run..."
Write-Host ""

# Build the Docker image
try {
    docker build -t $imageName .
    Write-Success "✓ Docker image built successfully"
} catch {
    Write-Error "✗ Failed to build Docker image"
    Write-Error $_.Exception.Message
    exit 1
}

Write-Host ""
Write-Info "🚀 Starting container..."

# Create volume if it doesn't exist
docker volume create $volumeName | Out-Null

# Run the container
try {
    docker run -d `
        --name $containerName `
        -p "${Port}:5000" `
        -v "${volumeName}:/app/data" `
        --restart unless-stopped `
        $imageName | Out-Null
    
    Write-Success "✓ Container started successfully"
} catch {
    Write-Error "✗ Failed to start container"
    Write-Error $_.Exception.Message
    exit 1
}

# Wait a moment for the container to start
Write-Info "⏳ Waiting for application to start..."
Start-Sleep -Seconds 3

# Check if container is running
$containerStatus = docker ps --filter "name=$containerName" --filter "status=running" --format "{{.Names}}"
if ($containerStatus -eq $containerName) {
    Write-Success "✓ Container is running"
} else {
    Write-Error "✗ Container failed to start. Checking logs..."
    docker logs $containerName
    exit 1
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                                                        ║" -ForegroundColor Green
Write-Host "║              🎉 Deployment Successful! 🎉              ║" -ForegroundColor Green
Write-Host "║                                                        ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Success "📱 Filament Tracker is now running at:"
Write-Host ""
Write-Host "   http://localhost:$Port" -ForegroundColor Yellow
Write-Host ""
Write-Info "📝 Useful Docker commands:"
Write-Host "   View logs:      docker logs $containerName"
Write-Host "   Stop app:       docker stop $containerName"
Write-Host "   Start app:      docker start $containerName"
Write-Host "   Remove app:     docker rm -f $containerName"
Write-Host "   View data:      docker volume inspect $volumeName"
Write-Host ""
Write-Info "💾 Your data is stored in Docker volume: $volumeName"
Write-Info "   To backup: docker run --rm -v ${volumeName}:/data -v `"`${PWD}:/backup`" alpine tar czf /backup/filament-backup.tar.gz -C /data ."
Write-Host ""

# Open browser unless -NoBrowser flag is set
if (-not $NoBrowser) {
    Write-Info "🌐 Opening browser..."
    Start-Sleep -Seconds 2
    Start-Process "http://localhost:$Port"
}

Write-Success "✓ Deployment complete!"
Write-Host ""
