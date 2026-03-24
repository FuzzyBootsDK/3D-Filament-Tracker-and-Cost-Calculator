# MQTT Relay Quick Reference Card

## 🚀 Quick Setup (Docker)

```bash
# 1. Update and restart container
docker-compose down
docker-compose up -d --build

# 2. Verify port is exposed
docker ps | grep filament-tracker
# Should show: 0.0.0.0:1883->1883/tcp

# 3. Enable in Settings UI
# - Go to http://<nas-ip>:5500
# - Settings → MQTT Relay Server
# - Check "Enable MQTT Relay Server"
# - Click "Save Relay Settings"

# 4. Update ESP32 code to use your NAS IP
```

---

## 📝 ESP32 Minimal Example

```cpp
#include <WiFi.h>
#include <PubSubClient.h>

const char* mqtt_server = "192.168.1.50";  // Your NAS IP
const int mqtt_port = 1883;

WiFiClient espClient;
PubSubClient client(espClient);

void callback(char* topic, byte* payload, unsigned int length) {
  // Process message
}

void setup() {
  Serial.begin(115200);
  WiFi.begin("SSID", "PASSWORD");
  while (WiFi.status() != WL_CONNECTED) delay(500);
  
  client.setServer(mqtt_server, mqtt_port);
  client.setCallback(callback);
}

void loop() {
  if (!client.connected()) {
    if (client.connect("ESP32Client")) {
      client.subscribe("device/YOUR_SERIAL/report");
    }
  }
  client.loop();
}
```

---

## 🔍 Troubleshooting Commands

```bash
# Check if port is accessible
telnet <nas-ip> 1883

# View container logs
docker logs -f filament-tracker

# Check port mapping
docker port filament-tracker

# Find your NAS IP
hostname -I | awk '{print $1}'

# Test MQTT connection
mosquitto_sub -h <nas-ip> -p 1883 -t "device/+/report" -v
```

---

## ⚙️ Configuration Options

### Settings UI
- **Port:** Default 1883 (change if needed)
- **Username:** Optional (leave empty for no auth)
- **Password:** Optional (required if username is set)

### Environment Variables (.env file)
```env
MQTT_RELAY_ENABLED=true
MQTT_RELAY_PORT=1883
MQTT_RELAY_USERNAME=optional
MQTT_RELAY_PASSWORD=optional
```

---

## 🚨 Common Issues

| Issue | Quick Fix |
|-------|-----------|
| Port in use | Change port to 1884 in Settings |
| ESP32 can't connect | Use NAS IP, not `localhost` or container IP |
| No messages | Check BambuLab MQTT is connected |
| Firewall blocks | Open port 1883: `sudo ufw allow 1883/tcp` |

---

## 📊 Status Indicators

**In Filament Tracker Settings:**
- 🟢 Green = Relay running and accepting connections
- 🔴 Red = Error (check logs)
- ⚠️ Yellow = Warning (e.g., printer not connected)

**In Docker logs:**
```
[MqttRelay] MQTT Relay Server started on port 1883  ← Success
[MqttRelay] Client ESP32-1234 connected             ← Client joined
[MqttRelay] Relayed message to 1 client(s)          ← Working
```

---

## 🔗 Full Documentation

- **Setup Guide:** `MQTT-RELAY-SETUP.md`
- **Changelog:** `MQTT-RELAY-CHANGELOG.md`
- **Main README:** `README.md`

---

## 💡 Pro Tips

1. **Use static IP for NAS** - Prevents ESP32 reconnection issues
2. **Add reconnection logic to ESP32** - See full example in setup guide
3. **Monitor relay logs** - `docker logs -f filament-tracker | grep MqttRelay`
4. **Test with mosquitto tools first** - Before debugging ESP32
5. **Check firewall on first setup** - Most connection issues are firewall-related

---

## 🎯 What This Solves

**Problem:** Multiple devices connecting to printer → connection drops

**Solution:** All devices connect to relay → stable connections

```
Before:  Printer ← Device1, Device2, Device3 (unstable)
After:   Printer ← Relay ← Device1, Device2, Device3 (stable)
```

---

**Need help?** See the full troubleshooting guide in `MQTT-RELAY-SETUP.md`
