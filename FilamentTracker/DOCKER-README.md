# Filament Tracker v2.0 - Docker Deployment

Easy one-click deployment of Filament Tracker using Docker.

## Prerequisites

- **Docker Desktop** installed and running
  - Windows: https://www.docker.com/products/docker-desktop
  - Mac: https://www.docker.com/products/docker-desktop
  - Linux: https://docs.docker.com/engine/install/

## Quick Start (One-Click Deployment)

### Windows

1. **Open PowerShell as Administrator** (Right-click PowerShell → Run as Administrator)
2. Navigate to the FilamentTracker folder:
   ```powershell
   cd "path\to\FilamentTracker"
   ```
3. Run the deployment script:
   ```powershell
   .\deploy.ps1
   ```

The script will:
- ✓ Check if Docker is installed and running
- ✓ Build the Docker image
- ✓ Start the container
- ✓ Open your browser automatically

**Note:** If you get a script execution error, run this first:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Alternative: Using Docker Compose

```bash
docker-compose up -d
```

This will build and start the application in detached mode.

## Accessing the Application

Once deployed, the application is available at:
- **http://localhost:5000**

## Script Options

```powershell
# Deploy without opening browser
.\deploy.ps1 -NoBrowser

# Deploy on a different port
.\deploy.ps1 -Port 8080

# Clean deployment (removes old images and containers)
.\deploy.ps1 -Clean

# Full clean (WARNING: Deletes all data!)
.\deploy.ps1 -Clean  # Then confirm when prompted
```

## Managing the Container

### View Logs
```bash
docker logs filament-tracker
```

### View Real-time Logs
```bash
docker logs -f filament-tracker
```

### Stop the Application
```bash
docker stop filament-tracker
```

### Start the Application
```bash
docker start filament-tracker
```

### Restart the Application
```bash
docker restart filament-tracker
```

### Remove the Application
```bash
docker stop filament-tracker
docker rm filament-tracker
```

### Update the Application
1. Pull latest changes or rebuild
2. Run the deploy script again:
   ```powershell
   .\deploy.ps1
   ```

## Data Persistence

Your filament data is stored in a Docker volume named `filament-tracker-data`. This means:
- ✓ Data persists even if you remove the container
- ✓ Data survives application updates
- ✓ You can backup the volume separately

### Backup Your Data

**Create a backup:**
```bash
docker run --rm -v filament-tracker-data:/data -v ${PWD}:/backup alpine tar czf /backup/filament-backup.tar.gz -C /data .
```

This creates `filament-backup.tar.gz` in your current directory.

**Restore from backup:**
```bash
docker run --rm -v filament-tracker-data:/data -v ${PWD}:/backup alpine sh -c "cd /data && tar xzf /backup/filament-backup.tar.gz"
```

### Export Data as CSV
Use the built-in export feature in the app:
1. Open the application
2. Go to **Settings**
3. Click **Export to CSV**
4. Save the file to your backup location

## Deploying to a NAS

### Synology NAS

1. Install Docker package from Package Center
2. Copy the FilamentTracker folder to your NAS
3. SSH into your NAS or use Task Scheduler:
   ```bash
   cd /volume1/docker/FilamentTracker
   docker-compose up -d
   ```
4. Access via: `http://YOUR-NAS-IP:5000`

### QNAP NAS

1. Install Container Station
2. Copy files to NAS
3. Use Container Station UI to:
   - Create container from `filament-tracker` image
   - Map port 5000
   - Add volume for `/app/data`

### Accessing from Other Devices

To access from other devices on your network:
1. Find your server's IP address:
   - Windows: `ipconfig`
   - Mac/Linux: `ifconfig` or `ip addr`
2. Access from any device: `http://SERVER-IP:5000`

**Example:** `http://192.168.1.100:5000`

## Troubleshooting

### Docker is not running
**Error:** "Docker daemon is not running"
**Fix:** Start Docker Desktop and wait for it to fully start.

### Port already in use
**Error:** "port is already allocated"
**Fix:** Use a different port:
```powershell
.\deploy.ps1 -Port 8080
```

### Container won't start
**Check logs:**
```bash
docker logs filament-tracker
```

### Permission denied (Linux/Mac)
**Fix:** Run with sudo or add your user to docker group:
```bash
sudo usermod -aG docker $USER
```
Then log out and back in.

### Database is locked
This can happen if you have multiple instances running.
**Fix:**
```bash
docker stop filament-tracker
docker start filament-tracker
```

### Need to reset everything
```powershell
.\deploy.ps1 -Clean
```
Then confirm volume deletion when prompted.

## Security Notes

- The application runs on HTTP by default (not HTTPS)
- For production/external access, consider setting up a reverse proxy with HTTPS
- Database is stored in a Docker volume with default permissions
- No authentication is built-in - suitable for local/trusted network use

## Advanced Configuration

### Custom Environment Variables

Edit `docker-compose.yml` to add environment variables:
```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - ConnectionStrings__DefaultConnection=Data Source=/app/data/filaments.db
```

### Custom Port Mapping

Edit `docker-compose.yml`:
```yaml
ports:
  - "8080:5000"  # Access on port 8080
```

### Using External Database Location

Mount a local folder instead of a Docker volume:
```yaml
volumes:
  - ./data:/app/data  # Uses ./data folder in current directory
```

## Support

For issues or questions:
1. Check the logs: `docker logs filament-tracker`
2. Review this README
3. Check Docker Desktop is running with sufficient resources

## What's Inside

- **Dockerfile**: Multi-stage build configuration
- **docker-compose.yml**: Orchestration configuration
- **deploy.ps1**: Automated deployment script
- **.dockerignore**: Files to exclude from build

## Performance

**Image Size:** ~210MB (optimized with multi-stage build)
**Memory Usage:** ~100-150MB
**Startup Time:** ~3-5 seconds

## Technical Details

- **Base Image:** mcr.microsoft.com/dotnet/aspnet:8.0
- **SDK Image:** mcr.microsoft.com/dotnet/sdk:8.0
- **Database:** SQLite (file-based)
- **Framework:** Blazor Server (.NET 8)
- **Port:** 5000 (HTTP)
