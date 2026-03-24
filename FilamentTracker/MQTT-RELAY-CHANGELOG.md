# MQTT Relay Feature - Changelog

## Version 2.0 - MQTT Relay Implementation

**Date:** 2024  
**Feature:** MQTT Relay/Rebroadcaster for ESP32 and other MQTT clients

---

## 🎯 Purpose

Solves connection stability issues when multiple devices (Filament Tracker, ESP32, Home Assistant, etc.) try to connect directly to a BambuLab printer's MQTT broker. The printer's embedded MQTT broker has limited concurrent connection slots, causing frequent disconnections and instability.

---

## ✨ What's New

### **MQTT Relay Server**
- In-process MQTT broker that rebroadcasts all printer messages
- Single connection to printer → unlimited clients to relay
- Optional username/password authentication
- Automatic startup with configurable settings
- Real-time connection monitoring and logging

### **Settings UI**
- New configuration section in Settings page
- Enable/disable toggle with status display
- Port configuration (default: 1883)
- Optional authentication fields
- Auto-generated ESP32 connection code with server IP
- Connection status and error messages

### **Docker Support**
- Port 1883 exposed in docker-compose.yml
- Environment variable configuration support
- Comprehensive Docker networking documentation
- Firewall configuration guides

---

## 📦 Files Added

1. **FilamentTracker/Services/MqttRelayService.cs**
   - New service implementing MQTT relay functionality
   - Event-based message rebroadcasting
   - Client connection/disconnection logging
   - Authentication validation

2. **FilamentTracker/MQTT-RELAY-SETUP.md**
   - Complete setup guide
   - Docker networking explanation
   - ESP32 sample code (basic and advanced)
   - Troubleshooting guide with Docker-specific solutions
   - Architecture diagrams
   - FAQ section

3. **FilamentTracker/MQTT-RELAY-CHANGELOG.md**
   - This file - feature changelog and migration notes

---

## 🔧 Files Modified

### **1. FilamentTracker/Models/AppSettings.cs**
Added relay configuration properties:
```csharp
public bool MqttRelayEnabled { get; set; } = false;
public int MqttRelayPort { get; set; } = 1883;
public string? MqttRelayUsername { get; set; }
public string? MqttRelayPassword { get; set; }
```

### **2. FilamentTracker/Program.cs**
- Registered `MqttRelayService` as singleton
- Added database migration for relay settings columns
- Added environment variable support for relay configuration
- Added relay auto-start on application startup

### **3. FilamentTracker/Components/SettingsPage.razor**
- Injected `MqttRelayService`
- Added relay configuration UI section
- Added `SaveMqttRelaySettings()` method
- Added `GetServerIpAddress()` helper method
- Added relay status fields and validation

### **4. FilamentTracker/FilamentTracker.csproj**
Added package reference:
```xml
<PackageReference Include="MQTTnet.Server" Version="5.1.0.1559"/>
```

### **5. FilamentTracker/docker-compose.yml**
Added port mapping:
```yaml
ports:
  - "${MQTT_RELAY_PORT:-1883}:1883"  # MQTT Relay port
```

### **6. FilamentTracker/Dockerfile**
Added port exposure:
```
EXPOSE 1883
```

### **7. FilamentTracker/README.md**
Updated configuration section to mention MQTT Relay

---

## 🗄️ Database Changes

### **New Columns in AppSettings Table:**

| Column | Type | Default | Description |
|--------|------|---------|-------------|
| `MqttRelayEnabled` | INTEGER | 0 | Enable/disable relay server |
| `MqttRelayPort` | INTEGER | 1883 | Port for relay server |
| `MqttRelayUsername` | TEXT | NULL | Optional authentication username |
| `MqttRelayPassword` | TEXT | NULL | Optional authentication password |

**Migration:** Automatic on application startup via `Program.cs`

---

## 🚀 Deployment Instructions

### **For Docker Users (Recommended):**

1. **Stop existing container:**
   ```bash
   docker-compose down
   ```

2. **Pull/rebuild with updates:**
   ```bash
   docker-compose build --no-cache
   docker-compose up -d
   ```

3. **Verify ports are exposed:**
   ```bash
   docker ps | grep filament-tracker
   ```
   
   Should show: `0.0.0.0:1883->1883/tcp`

4. **Enable in Settings:**
   - Access web UI at `http://<nas-ip>:5500`
   - Go to Settings
   - Enable MQTT Relay Server
   - Configure port and authentication (if desired)
   - Save settings

5. **Update your ESP32 code:**
   - Change MQTT server IP to your NAS/server IP
   - Keep the same topic structure
   - See MQTT-RELAY-SETUP.md for complete example

### **For Local .NET Users:**

1. **Restore packages:**
   ```bash
   dotnet restore
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

3. **Database migrates automatically** on first run

4. **Enable relay in Settings** (same as Docker steps 4-5 above)

---

## 🔄 Migration from Previous Version

### **Existing Users:**

No action required! Your existing setup will continue to work:

- ✅ Existing database is automatically migrated
- ✅ Relay is disabled by default
- ✅ BambuLab connection works exactly as before
- ✅ All existing features remain unchanged

### **To Enable Relay:**

1. Ensure BambuLab MQTT is connected and working
2. Go to Settings → MQTT Relay Server
3. Enable and configure
4. Update your ESP32 to point to relay

### **If Using Environment Variables:**

Add to your `.env` file (optional):
```env
MQTT_RELAY_ENABLED=true
MQTT_RELAY_PORT=1883
MQTT_RELAY_USERNAME=optional_username
MQTT_RELAY_PASSWORD=optional_password
```

---

## 📊 Architecture Changes

### **Before (Direct Connections):**
```
Printer ← FilamentTracker
        ← ESP32
        ← Other Clients
```
Issues:
- Limited concurrent connections
- Frequent disconnections
- Printer resource strain

### **After (With Relay):**
```
Printer ← FilamentTracker (MQTT Relay) ← ESP32
                                      ← Other Clients
```
Benefits:
- ✅ Single printer connection
- ✅ Unlimited relay clients
- ✅ Better stability
- ✅ Optional authentication

---

## 🧪 Testing Checklist

- [x] Builds successfully on .NET 10
- [x] Docker container builds and runs
- [x] Database migration works on clean install
- [x] Database migration works on existing database
- [x] Settings UI loads and saves relay configuration
- [x] Relay starts when enabled
- [x] Relay stops when disabled
- [x] Port conflicts are handled gracefully
- [x] Authentication works when configured
- [x] Messages are rebroadcast to connected clients
- [x] Connection logging works
- [x] Auto-restart on container restart
- [x] Environment variables override settings

---

## 📝 Known Limitations

1. **Relay requires BambuLab connection:**
   - Relay cannot start if printer is not connected
   - This is by design - there's nothing to relay without printer data

2. **Port conflicts:**
   - If port 1883 is in use (e.g., by Mosquitto), you must use a different port
   - Solution documented in troubleshooting guide

3. **Docker networking:**
   - ESP32 must use host machine IP, not container internal IP
   - This is standard Docker behavior and fully documented

4. **No retained messages:**
   - Messages are not retained for new subscribers
   - Clients receive messages only after they connect

---

## 🐛 Troubleshooting

See the comprehensive troubleshooting section in:
**FilamentTracker/MQTT-RELAY-SETUP.md**

Common issues covered:
- Port already in use
- ESP32 can't connect
- Docker networking
- Firewall configuration
- Authentication problems
- Connection drops

---

## 📚 Documentation

| Document | Purpose |
|----------|---------|
| `MQTT-RELAY-SETUP.md` | Complete setup guide with Docker instructions |
| `MQTT-RELAY-CHANGELOG.md` | This file - feature overview and changes |
| `README.md` | Updated with relay feature mention |

---

## 🤝 Credits

Feature developed to solve connection stability issues when running multiple MQTT clients against BambuLab printers.

Special thanks to the 3D printing community for feedback and testing!

---

## 📄 License

MIT License - Same as the main project

---

**Made for the 3D printing community** 🖨️
