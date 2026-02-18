# Filament Tracker v2 - Deployment Guide for NAS

## Date: February 18, 2026

## ✅ Pre-Deployment Checklist

### Changes Included in This Version:
- ✅ Help menu updated with all new features
- ✅ Weighted average pricing system
- ✅ Smart spool ordering with "Next to use" indicators
- ✅ Improved usage recording (per-spool in Manage Spool section)
- ✅ Currency selection (24 currencies including DKK, USD, EUR, GBP, SEK, NOK)
- ✅ Inventory integration in Print Cost Calculator
- ✅ Docker configuration set to port 5500
- ✅ English localization complete
- ✅ Real-time price updates in detail modal

---

## 🐋 Docker Configuration

### Port Settings:
- **Internal Port:** 5500 (configured in Dockerfile)
- **External Port:** Map to your desired port (recommended: 5500)

### Dockerfile Verification:
```dockerfile
ENV ASPNETCORE_URLS=http://+:5500
EXPOSE 5500
```
✅ **Confirmed:** Port is set to 5500

---

## 📦 Deployment Steps on Synology NAS

### 1. Transfer Files to NAS

**Option A: Using SSH/SCP**
```bash
# From your Mac, transfer the entire project folder
scp -r "/Users/lassesorensen/Library/CloudStorage/OneDrive-Personal/Projects/Filament Tracker v2/Tracker v2/FilamentTracker" \
  username@your-nas-ip:/volume1/docker/FilamentTracker/
```

**Option B: Using File Station**
1. Open Synology File Station
2. Navigate to `/docker/FilamentTracker/` (create folder if needed)
3. Upload all files from your local FilamentTracker folder
4. Ensure all files including Dockerfile are uploaded

**Option C: Using Git (Recommended)**
```bash
# SSH into your NAS
ssh username@your-nas-ip

# Navigate to docker folder
cd /volume1/docker/

# Clone or pull your repository
git clone https://github.com/yourusername/filament-tracker.git FilamentTracker
# OR if already exists:
cd FilamentTracker
git pull origin main
```

---

### 2. Stop Existing Container

**SSH into your NAS:**
```bash
ssh username@your-nas-ip
```

**Stop and remove the old container:**
```bash
# Stop the running container
sudo docker stop filament-tracker

# Remove the container (keeps the image and volume)
sudo docker rm filament-tracker
```

**Alternative: Stop via Synology Docker UI**
1. Open Docker package in DSM
2. Go to Container tab
3. Select "filament-tracker"
4. Click Stop
5. Click Action → Delete (keeps volumes)

---

### 3. Remove Old Image (Optional but Recommended)

**To force a fresh build:**
```bash
# List images to find the old one
sudo docker images

# Remove old image
sudo docker rmi filament-tracker:latest
# OR
sudo docker rmi <IMAGE_ID>
```

---

### 4. Build New Docker Image

**Navigate to project directory:**
```bash
cd /volume1/docker/FilamentTracker
```

**Build the image:**
```bash
sudo docker build -t filament-tracker:latest .
```

**Expected output:**
```
[+] Building 45.2s (18/18) FINISHED
 => [internal] load build definition from Dockerfile
 => => transferring dockerfile: 1.2kB
 => [internal] load .dockerignore
 => ...
 => exporting to image
 => => naming to docker.io/library/filament-tracker:latest
```

**Verify the build:**
```bash
sudo docker images | grep filament-tracker
```

You should see:
```
filament-tracker    latest    <IMAGE_ID>    X minutes ago    XXX MB
```

---

### 5. Run New Container

**Option A: Command Line (with data persistence)**
```bash
sudo docker run -d \
  --name filament-tracker \
  --restart unless-stopped \
  -p 5500:5500 \
  -v /volume1/docker/FilamentTracker/data:/app/data \
  filament-tracker:latest
```

**Option B: With Custom Port Mapping**
```bash
# Map to a different external port (e.g., 8080)
sudo docker run -d \
  --name filament-tracker \
  --restart unless-stopped \
  -p 8080:5500 \
  -v /volume1/docker/FilamentTracker/data:/app/data \
  filament-tracker:latest
```

**Option C: Using Synology Docker UI**
1. Open Docker package in DSM
2. Go to Image tab
3. Select "filament-tracker:latest"
4. Click Launch
5. Configure container:
   - **Container Name:** filament-tracker
   - **Port Settings:**
     - Local Port: 5500 (or your choice)
     - Container Port: 5500
     - Type: TCP
   - **Volume Settings:**
     - Add → Mount Path: `/app/data`
     - Mount Path: `/volume1/docker/FilamentTracker/data`
   - **Environment:**
     - Auto-restart: Yes
6. Click Apply

---

### 6. Verify Container is Running

**Check container status:**
```bash
sudo docker ps | grep filament-tracker
```

**Expected output:**
```
CONTAINER_ID   IMAGE                       STATUS         PORTS                    NAMES
abc123def456   filament-tracker:latest     Up 2 seconds   0.0.0.0:5500->5500/tcp   filament-tracker
```

**Check container logs:**
```bash
sudo docker logs filament-tracker
```

**Expected to see:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5500
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

### 7. Test the Application

**From your network:**
```
http://your-nas-ip:5500
```

**From outside your network (if configured):**
```
http://your-domain:5500
```

**Test checklist:**
- ✅ Home page loads
- ✅ Inventory page shows your filaments
- ✅ Add Filament page works
- ✅ Settings page loads
- ✅ Calculator page loads
- ✅ Help page shows updated content
- ✅ Currency selector works
- ✅ Database persists after container restart

---

## 🔄 Complete Deployment Command Sequence

**For quick copy-paste (adjust paths as needed):**

```bash
# SSH into NAS
ssh username@your-nas-ip

# Stop old container
sudo docker stop filament-tracker
sudo docker rm filament-tracker

# Navigate to project directory
cd /volume1/docker/FilamentTracker

# Pull latest changes (if using Git)
git pull origin main

# Build new image
sudo docker build -t filament-tracker:latest .

# Run new container
sudo docker run -d \
  --name filament-tracker \
  --restart unless-stopped \
  -p 5500:5500 \
  -v /volume1/docker/FilamentTracker/data:/app/data \
  filament-tracker:latest

# Verify it's running
sudo docker ps | grep filament-tracker
sudo docker logs filament-tracker

# Test in browser
echo "Open http://$(hostname -I | awk '{print $1}'):5500 in your browser"
```

---

## 🛠️ Troubleshooting Commands

### View Real-time Logs
```bash
sudo docker logs -f filament-tracker
```
Press `Ctrl+C` to exit

### Check Container Health
```bash
sudo docker inspect filament-tracker | grep -A 10 State
```

### Restart Container
```bash
sudo docker restart filament-tracker
```

### Enter Container Shell (for debugging)
```bash
sudo docker exec -it filament-tracker /bin/bash
```

### Check Port Binding
```bash
sudo netstat -tulpn | grep 5500
```

### Check Disk Space
```bash
df -h /volume1/docker/
```

---

## 💾 Data Persistence

### Database Location
- **Host:** `/volume1/docker/FilamentTracker/data/filaments.db`
- **Container:** `/app/data/filaments.db`

### Backup Your Database
```bash
# Create backup
sudo cp /volume1/docker/FilamentTracker/data/filaments.db \
     /volume1/docker/FilamentTracker/data/filaments.db.backup-$(date +%Y%m%d)

# Or export via the app
# Settings → Export to CSV
```

### Restore from Backup
```bash
# Stop container
sudo docker stop filament-tracker

# Restore database
sudo cp /volume1/docker/FilamentTracker/data/filaments.db.backup-20260218 \
     /volume1/docker/FilamentTracker/data/filaments.db

# Start container
sudo docker start filament-tracker
```

---

## 🔐 Security Recommendations

### Reverse Proxy (Optional)
If using Nginx Proxy Manager or similar:
```nginx
location / {
    proxy_pass http://localhost:5500;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

### Firewall Rules
- Only expose port 5500 to your local network
- Use VPN for remote access
- Consider using Synology's built-in VPN Server package

---

## 📊 Monitoring

### Check Resource Usage
```bash
sudo docker stats filament-tracker
```

### Container Size
```bash
sudo docker images | grep filament-tracker
```

### Log Rotation
Synology Docker handles log rotation automatically, but you can configure it:
```bash
sudo docker inspect filament-tracker | grep -A 5 LogConfig
```

---

## 🎊 Deployment Complete!

Your Filament Tracker v2 should now be running with all the latest features:
- ✅ Weighted average pricing
- ✅ Smart spool ordering
- ✅ Currency selection
- ✅ Inventory-integrated calculator
- ✅ Improved usage tracking
- ✅ Updated help documentation

**Access your app at:** `http://your-nas-ip:5500`

---

## 📞 Quick Reference

**Start Container:**
```bash
sudo docker start filament-tracker
```

**Stop Container:**
```bash
sudo docker stop filament-tracker
```

**Restart Container:**
```bash
sudo docker restart filament-tracker
```

**View Logs:**
```bash
sudo docker logs filament-tracker
```

**Rebuild & Restart:**
```bash
sudo docker stop filament-tracker && \
sudo docker rm filament-tracker && \
sudo docker build -t filament-tracker:latest . && \
sudo docker run -d --name filament-tracker --restart unless-stopped \
  -p 5500:5500 -v /volume1/docker/FilamentTracker/data:/app/data \
  filament-tracker:latest
```

Enjoy your updated Filament Tracker! 🎉

