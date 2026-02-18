# Deploying Filament Tracker v2.0 to Synology NAS

## Prerequisites
- SSH access enabled on your Synology NAS
- Container Manager package installed on Synology DSM
- Git or file transfer method to upload files

## Option 1: Direct Deployment via SSH (Recommended)

### Step 1: Transfer Files to NAS

1. **Using rsync (from your Mac):**
   ```bash
   rsync -avz --exclude='bin' --exclude='obj' --exclude='*.db' \
     "/Users/lassesorensen/Library/CloudStorage/OneDrive-Personal/Projects/Filament Tracker v2/Tracker v2/FilamentTracker/" \
     your-username@YOUR-NAS-IP:/volume1/docker/FilamentTracker/
   ```

2. **Or using File Station:**
   - Open Synology File Station
   - Navigate to `/docker/FilamentTracker` (create folder if needed)
   - Upload all files from the FilamentTracker directory

### Step 2: SSH into Your NAS

```bash
ssh your-username@YOUR-NAS-IP
```

### Step 3: Navigate to the Project Directory

```bash
cd /volume1/docker/FilamentTracker
```

### Step 4: Build and Deploy

```bash
# Build the Docker image
docker build -t filament-tracker:latest .

# Stop and remove old container (if exists)
docker stop filament-tracker 2>/dev/null || true
docker rm filament-tracker 2>/dev/null || true

# Run the new container
docker run -d \
  --name filament-tracker \
  --restart unless-stopped \
  -p 5000:5000 \
  -v filament-data:/app/data \
  filament-tracker:latest
```

### Step 5: Verify It's Running

```bash
docker logs filament-tracker
```

### Access Your App

Open your browser to: `http://YOUR-NAS-IP:5000`

---

## Option 2: Using Docker Compose (Easier)

### Step 1: Transfer Files (same as above)

### Step 2: SSH and Navigate

```bash
ssh your-username@YOUR-NAS-IP
cd /volume1/docker/FilamentTracker
```

### Step 3: Deploy with Docker Compose

```bash
# Stop old container
docker-compose down

# Build and start new container
docker-compose up -d --build

# View logs
docker-compose logs -f
```

---

## Option 3: Using Synology Container Manager UI

### Step 1: Prepare the Image on Your Mac

Since Docker isn't in PATH, use Docker Desktop GUI:
1. Open Docker Desktop
2. Navigate to Images
3. Click "Build" and select the FilamentTracker directory
4. Tag it as `filament-tracker:latest`

Or build via Docker Desktop terminal:
```bash
/Applications/Docker.app/Contents/Resources/bin/docker build -t filament-tracker:latest .
```

### Step 2: Export the Image

```bash
/Applications/Docker.app/Contents/Resources/bin/docker save -o filament-tracker.tar filament-tracker:latest
```

### Step 3: Transfer to NAS

Upload `filament-tracker.tar` to your NAS using File Station.

### Step 4: Import in Container Manager

1. Open **Container Manager** in DSM
2. Go to **Image**
3. Click **Add** → **Add from file**
4. Select the `filament-tracker.tar` file
5. Wait for import to complete

### Step 5: Create Container

1. Select the imported image
2. Click **Launch**
3. Configure:
   - **Container name:** `filament-tracker`
   - **Port mapping:** Local Port `5000` → Container Port `5000`
   - **Volume:** Mount `/app/data` to a folder (e.g., `/docker/filament-tracker/data`)
   - **Restart policy:** Always restart
4. Click **Done**

---

## Updating to New Version

When you have updates:

### Via SSH:
```bash
ssh your-username@YOUR-NAS-IP
cd /volume1/docker/FilamentTracker

# Pull changes (if using git) or re-upload files
# Then rebuild and restart:
docker-compose down
docker-compose up -d --build
```

### Via Container Manager UI:
1. Export new image from your Mac
2. Stop old container
3. Import new image
4. Start new container

---

## Port Configuration

Default port is `5000`. To change:

**Docker Compose:** Edit `docker-compose.yml`:
```yaml
ports:
  - "8080:5000"  # Change 8080 to your desired port
```

**Docker Run:** Change the port mapping:
```bash
docker run -d \
  --name filament-tracker \
  -p 8080:5000 \
  ...
```

---

## Backing Up Your Data

Your filament data is stored in the Docker volume. To backup:

```bash
# Find the volume location
docker volume inspect filament-data

# Backup the database
docker cp filament-tracker:/app/data/filaments.db /volume1/backups/filaments-$(date +%Y%m%d).db
```

Or use the app's built-in CSV export feature from Settings.

---

## Troubleshooting

### Container won't start
```bash
docker logs filament-tracker
```

### Port already in use
Change the port in docker-compose.yml or docker run command.

### Can't access from network
Check Synology firewall settings - allow port 5000 (or your custom port).

### Permission issues
```bash
docker exec -it filament-tracker chmod -R 777 /app/data
```

---

## Quick Reference Commands

```bash
# View running containers
docker ps

# View all containers
docker ps -a

# View logs
docker logs filament-tracker
docker logs -f filament-tracker  # Follow

# Restart container
docker restart filament-tracker

# Stop container
docker stop filament-tracker

# Start container
docker start filament-tracker

# Remove container
docker stop filament-tracker && docker rm filament-tracker

# View images
docker images

# Remove old images
docker image prune -a
```

---

## What's New in This Version

✨ **Print Cost Calculator** - Professional 3D print cost estimation tool
- Bambu Lab printer profiles
- Multi-material support
- G-code import
- Batch optimization
- PDF quote export

All your existing filament data will be preserved when updating!
