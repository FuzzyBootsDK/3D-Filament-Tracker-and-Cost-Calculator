@echo off
REM Filament Tracker - Simple Docker Deployment (Batch Alternative)
REM Double-click this file to deploy the app

echo.
echo =====================================================
echo    Filament Tracker v2.0 - Docker Deploy
echo =====================================================
echo.

REM Check if Docker is installed
docker --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not installed or not in PATH
    echo.
    echo Please install Docker Desktop from:
    echo https://www.docker.com/products/docker-desktop
    echo.
    pause
    exit /b 1
)

echo [OK] Docker is installed
echo.

REM Check if Docker is running
docker ps >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker daemon is not running
    echo.
    echo Please start Docker Desktop and try again.
    echo.
    pause
    exit /b 1
)

echo [OK] Docker is running
echo.

REM Stop and remove existing container
echo Checking for existing container...
docker stop filament-tracker >nul 2>&1
docker rm filament-tracker >nul 2>&1
echo.

REM Build the image
echo Building Docker image...
echo This may take a few minutes on first run...
echo.
docker build -t filament-tracker .
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to build Docker image
    pause
    exit /b 1
)

echo.
echo [OK] Image built successfully
echo.

REM Create volume
docker volume create filament-tracker-data >nul 2>&1

REM Run the container
echo Starting container...
docker run -d --name filament-tracker -p 5000:5000 -v filament-tracker-data:/app/data --restart unless-stopped filament-tracker >nul
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to start container
    pause
    exit /b 1
)

echo.
echo [OK] Container started
echo.

REM Wait for startup
echo Waiting for application to start...
timeout /t 3 /nobreak >nul

echo.
echo =====================================================
echo        Deployment Successful!
echo =====================================================
echo.
echo App is running at: http://localhost:5000
echo.
echo Opening browser...
echo.

REM Open browser
start http://localhost:5000

echo.
echo Useful commands:
echo   View logs:   docker logs filament-tracker
echo   Stop app:    docker stop filament-tracker
echo   Start app:   docker start filament-tracker
echo.
echo Press any key to exit...
pause >nul
