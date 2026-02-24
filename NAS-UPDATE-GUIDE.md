# 🖥️ How to Update Filament Tracker on Your NAS

This guide walks through updating Filament Tracker on your Synology (or any Linux-based) NAS.

---

## Overview

There are now **three ways** to update. Docker Hub is the recommended method — once set up, updating takes a single command on the NAS.

| Method | Effort | Requires internet on NAS |
|---|---|---|
| ✅ **Method 1 — Docker Hub** (recommended) | Push to GitHub → auto-build → pull on NAS | Yes |
| Method 2 — Export `.tar.gz` image | Build on Mac, copy file, load on NAS | No |
| Method 3 — Copy source & build on NAS | Compress, copy, build on NAS | No |

Your filament data lives in a Docker **named volume** and is **never** touched during updates.

---

## ✅ Method 1 — Docker Hub (Recommended)

The GitHub repository is connected to Docker Hub via GitHub Actions.  
Every push to `main` automatically builds and publishes a new image to:

```
fuzzybootsdk/filament-tracker:latest
```

### First-time setup (one-time only)

#### 1. Create a Docker Hub account & repository

1. Go to [https://hub.docker.com](https://hub.docker.com) and sign up (free).
2. Click **Repositories → Create Repository**.
3. Name it `filament-tracker`, set it to **Public**.
4. Click **Create**.

#### 2. Create a Docker Hub access token

1. Go to **Account Settings → Security → New Access Token**.
2. Name it `github-actions`, set permissions to **Read & Write**.
3. Copy the token — you only see it once.

#### 3. Add secrets to your GitHub repository

1. Open your repository on GitHub: `https://github.com/FuzzyBootsDK/3D-Filament-Tracker-and-Cost-Calculator`
2. Go to **Settings → Secrets and variables → Actions → New repository secret**.
3. Add these two secrets:

| Secret name | Value |
|---|---|
| `DOCKERHUB_USERNAME` | Your Docker Hub username |
| `DOCKERHUB_TOKEN` | The access token you just created |

#### 4. Set up the NAS (one-time only)

SSH into your NAS and create the docker-compose file:

```bash
ssh admin@YOUR-NAS-IP

# Create a folder for the app
mkdir -p /volume1/docker/filament-tracker
cd /volume1/docker/filament-tracker

# Create the docker-compose.yml
cat > docker-compose.yml << 'EOF'
version: '3.8'

services:
  filament-tracker:
    image: fuzzybootsdk/filament-tracker:latest
    container_name: filament-tracker
    ports:
      - "5500:5000"
    volumes:
      - filament-data:/app/data
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped

volumes:
  filament-data:
    driver: local
EOF

# Start it (pulls the image automatically)
sudo docker-compose up -d
```

Open your browser to `http://YOUR-NAS-IP:5500` ✅

---

### How to update after first-time setup

Every time you push changes to `main` on GitHub:

1. GitHub Actions builds a new image and pushes it to Docker Hub automatically.
2. On your NAS, run **one command**:

```bash
cd /volume1/docker/filament-tracker
sudo docker-compose pull && sudo docker-compose up -d
```

That's it. Your data is untouched. ✅

---

### How to trigger a build manually (without pushing code)

Go to your GitHub repository → **Actions → Build & Push to Docker Hub → Run workflow → Run workflow**.

---

## Method 2 — Export Docker Image (no internet on NAS)

Use this if your NAS has no internet access.

### On your Mac

```bash
cd "/Users/lassesorensen/Library/CloudStorage/OneDrive-Personal/Projects/Filament Tracker v2/Tracker v2/FilamentTracker"

# Build
docker build -t filament-tracker:latest .

# Export
docker save filament-tracker:latest | gzip > filament-tracker.tar.gz

# Copy to NAS
scp filament-tracker.tar.gz admin@YOUR-NAS-IP:/volume1/docker/filament-tracker/
```

### On your NAS

```bash
ssh admin@YOUR-NAS-IP
cd /volume1/docker/filament-tracker

sudo docker load < filament-tracker.tar.gz
sudo docker-compose down
sudo docker-compose up -d
```

---

## Method 3 — Copy Source & Build on NAS

Use this as a last resort if neither of the above works.

### On your Mac

```bash
cd "/Users/lassesorensen/Library/CloudStorage/OneDrive-Personal/Projects/Filament Tracker v2/Tracker v2"
zip -r FilamentTracker.zip FilamentTracker

scp FilamentTracker.zip admin@YOUR-NAS-IP:/volume1/docker/
```

### On your NAS

```bash
ssh admin@YOUR-NAS-IP
cd /volume1/docker

rm -rf FilamentTracker-src
unzip FilamentTracker.zip -d FilamentTracker-src
cd FilamentTracker-src/FilamentTracker   # adjust path if needed

sudo docker stop filament-tracker
sudo docker rm filament-tracker
sudo docker-compose up -d --build
```

---

## 🔒 Your Data Is Always Safe

The database lives in a Docker **named volume** (`filament-data`), not inside the container.  
`docker-compose down` and `docker-compose up -d` **never** touch volumes unless you explicitly add `--volumes`.

Before any update, export a CSV backup just in case:
- Open the app → **Settings → Export to CSV** → save the file.

---

## 📋 Quick Reference Cheat Sheet

| Task | Command |
|---|---|
| **Update via Docker Hub** | `sudo docker-compose pull && sudo docker-compose up -d` |
| Check running containers | `sudo docker ps` |
| View live logs | `sudo docker logs -f filament-tracker` |
| Stop container | `sudo docker stop filament-tracker` |
| Clean up old images | `sudo docker image prune -f` |

---

## 🔑 Synology Docker Permissions

If you see:
```
permission denied while trying to connect to the Docker daemon socket
```

**Quick fix:** prefix all `docker` commands with `sudo`.

**Permanent fix** (run once, then log out and back in):
```bash
sudo synogroup --add docker $USER
exit
ssh admin@YOUR-NAS-IP
```

---

## 🆘 If Something Goes Wrong

**App won't start:**
```bash
sudo docker logs filament-tracker
```

**Image won't pull (Docker Hub rate limit):**
Log in on the NAS first:
```bash
sudo docker login
```

**Need to roll back to a previous version:**
```bash
# Use a specific SHA tag from Docker Hub instead of latest
sudo docker pull fuzzybootsdk/filament-tracker:sha-abc1234
# Edit docker-compose.yml, change :latest to :sha-abc1234
sudo docker-compose up -d
```

**Completely reset (last resort — deletes all data):**
```bash
sudo docker-compose down --volumes
sudo docker-compose up -d
```
⚠️ This deletes your database. Only do this if you have a CSV backup.
