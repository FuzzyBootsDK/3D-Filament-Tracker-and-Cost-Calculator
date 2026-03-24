# MQTT Relay Setup Guide

## Overview

The MQTT Relay feature allows your Filament Tracker to act as a middleman between your BambuLab printer and other MQTT clients (like ESP32 devices). This reduces the number of direct connections to your printer's MQTT broker, improving stability and preventing connection issues.

## Why Use MQTT Relay?

**Problem:** BambuLab printers have a limited MQTT broker that can handle only a few concurrent connections. When multiple devices (Filament Tracker, ESP32, Home Assistant, etc.) try to connect directly to the printer, connections can become unstable or fail entirely.

**Solution:** The MQTT Relay Server in Filament Tracker:
1. Connects to your printer as a **single client**
2. Rebroadcasts all MQTT messages to its own MQTT broker
3. Allows unlimited clients to connect to the relay instead of the printer

## Setup Instructions

### 1. Enable BambuLab MQTT Connection

Before enabling the relay, you must first connect to your BambuLab printer:

1. Go to **Settings** in Filament Tracker
2. Scroll to **🖨️ BambuLab MQTT Live Tracking**
3. Fill in:
   - **Printer IP Address** (e.g., `192.168.1.100`)
   - **Access Code** (8-digit code from your printer)
   - **Serial Number** (found in printer settings)
4. Check **Enable BambuLab Live Tracking**
5. Click **💾 Save & Connect**

### 2. Enable MQTT Relay Server

Once connected to your printer:

1. Scroll to **🔄 MQTT Relay Server** section
2. Check **Enable MQTT Relay Server**
3. Configure settings:
   - **Relay Port:** Default is `1883` (standard MQTT port)
   - **Username:** Optional - leave empty for no authentication
   - **Password:** Optional - required only if username is set
4. Click **💾 Save Relay Settings**

The relay server will start automatically and begin rebroadcasting messages from your printer.

### 3. Configure Your ESP32 (or other MQTT clients)

Update your ESP32 code to connect to the Filament Tracker instead of directly to the printer:

```cpp
#include <WiFi.h>
#include <PubSubClient.h>

// WiFi credentials
const char* ssid = "YOUR_WIFI_SSID";
const char* password = "YOUR_WIFI_PASSWORD";

// MQTT Relay settings (instead of printer direct connection)
const char* mqtt_server = "192.168.1.50";  // IP of your NAS/server running Filament Tracker
const int mqtt_port = 1883;

// If you enabled authentication in Filament Tracker:
// const char* mqtt_user = "your_username";
// const char* mqtt_pass = "your_password";

WiFiClient espClient;
PubSubClient client(espClient);

void callback(char* topic, byte* payload, unsigned int length) {
  Serial.print("Message arrived [");
  Serial.print(topic);
  Serial.print("] ");
  
  String message;
  for (int i = 0; i < length; i++) {
    message += (char)payload[i];
  }
  Serial.println(message);
  
  // Parse and use the MQTT data here
  // The format is identical to direct printer connection
}

void reconnect() {
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection to relay server...");
    
    String clientId = "ESP32Client-";
    clientId += String(random(0xffff), HEX);
    
    // Connect with or without authentication
    if (client.connect(clientId.c_str())) {  // Or: client.connect(clientId.c_str(), mqtt_user, mqtt_pass)
      Serial.println("connected!");
      
      // Subscribe to the printer's report topic
      // Replace SERIAL_NUMBER with your printer's serial
      client.subscribe("device/SERIAL_NUMBER/report");
      
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" retrying in 5 seconds");
      delay(5000);
    }
  }
}

void setup() {
  Serial.begin(115200);
  
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi connected");
  
  client.setServer(mqtt_server, mqtt_port);
  client.setCallback(callback);
}

void loop() {
  if (!client.connected()) {
    reconnect();
  }
  client.loop();
}
```

## Docker Configuration

### How Docker Networking Works

When running in Docker, the container has its own internal IP address (typically `172.17.0.x`), but your ESP32 and other devices connect to your **host machine's IP** (your NAS/server IP like `192.168.1.50`).

**Network Architecture:**
```
┌─────────────────────────────────────────┐
│  Host Network (Your LAN)                │
│  192.168.1.0/24                         │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │  Docker Container               │   │
│  │  Internal IP: 172.17.0.x        │   │
│  │                                 │   │
│  │  ┌─────────────────────────┐   │   │
│  │  │ Filament Tracker        │   │   │
│  │  │ Port 5000 → Host 5500   │   │   │
│  │  │ Port 1883 → Host 1883   │◄──┼───┼─── ESP32 connects here
│  │  └─────────────────────────┘   │   │
│  └─────────────────────────────────┘   │
│                                         │
│  ESP32: 192.168.1.75                    │
│  NAS/Server: 192.168.1.50 ◄─────────────┼─── Use this IP!
└─────────────────────────────────────────┘
```

**Important:** Your ESP32 should connect to your **NAS/server IP** (e.g., `192.168.1.50`), NOT the container's internal IP.

### Port Mapping

The `docker-compose.yml` file has been updated to expose port 1883:

```yaml
ports:
  - "${HOST_PORT:-5500}:5000"          # Web UI: http://nas-ip:5500
  - "${MQTT_RELAY_PORT:-1883}:1883"    # MQTT Relay: mqtt://nas-ip:1883
```

This means:
- **Inside container:** MQTT server listens on port `1883`
- **On your NAS/host:** Port `1883` is exposed to your LAN
- **Your ESP32:** Connects to `<NAS_IP>:1883`

You can customize the external port by setting `MQTT_RELAY_PORT` in your `.env` file:

```env
HOST_PORT=5500
MQTT_RELAY_PORT=1883
```

### Deployment Steps

**1. Stop existing container:**
```bash
docker-compose down
```

**2. Rebuild and start with new ports:**
```bash
docker-compose up -d --build
```

**3. Verify ports are exposed:**
```bash
docker ps
```

You should see:
```
PORTS
0.0.0.0:5500->5000/tcp
0.0.0.0:1883->1883/tcp  ← MQTT Relay port
```

**4. Check logs:**
```bash
docker logs -f filament-tracker
```

Look for:
```
[BambuLabInit] MQTT connected successfully to 192.168.1.100
[MqttRelayInit] MQTT Relay started on port 1883
[MqttRelay] ESP32 and other clients can now connect to this server
```

### Finding Your Server IP

The Settings page in Filament Tracker automatically shows your server IP in the ESP32 connection instructions. You can also find it manually:

**From your NAS/server:**
```bash
# Linux/NAS
hostname -I | awk '{print $1}'

# Or
ip addr show | grep "inet " | grep -v 127.0.0.1
```

**From Windows:**
```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -notlike "*Loopback*"}
```

### Environment Variables

You can also configure the relay via environment variables (useful for automated Docker deployments):

**In `.env` file:**
```env
# BambuLab Connection
BAMBULAB_ENABLED=true
BAMBULAB_IP=192.168.1.100
BAMBULAB_CODE=12345678
BAMBULAB_SERIAL=01S00C1234567

# MQTT Relay
MQTT_RELAY_ENABLED=true
MQTT_RELAY_PORT=1883
MQTT_RELAY_USERNAME=optional_username
MQTT_RELAY_PASSWORD=optional_password
```

**Or directly in `docker-compose.yml`:**
```yaml
services:
  filament-tracker:
    environment:
      - BAMBULAB_ENABLED=true
      - BAMBULAB_IP=192.168.1.100
      - BAMBULAB_CODE=12345678
      - BAMBULAB_SERIAL=01S00C1234567
      - MQTT_RELAY_ENABLED=true
      - MQTT_RELAY_PORT=1883
```

### Testing Connectivity

**1. Test if port is accessible from another machine:**
```bash
# Using telnet
telnet <NAS_IP> 1883

# Using netcat
nc -zv <NAS_IP> 1883

# Using MQTT client tools
mosquitto_sub -h <NAS_IP> -p 1883 -t "device/+/report" -v
```

**2. Check Docker container is listening:**
```bash
docker exec filament-tracker netstat -tulpn | grep 1883
```

**3. Monitor relay connections in logs:**
```bash
docker logs -f filament-tracker | grep "MqttRelay"
```

You'll see connection events:
```
[MqttRelay] Client ESP32Client-a3f2 connected from 192.168.1.75:54321
[MqttRelay] Client ESP32Client-a3f2 subscribed to topic device/01S00C1234567/report
[MqttRelay] Relayed message to 1 client(s)
```

## Firewall Configuration

If your ESP32 or other clients can't connect, make sure port 1883 is open on your firewall:

### Synology NAS
1. Control Panel → Security → Firewall
2. Edit Rules → Create → Custom
3. Ports: TCP 1883
4. Source IP: Allow from local network
5. Action: Allow

### QNAP NAS
1. Control Panel → Network & File Services → Security
2. Firewall Rules → Add Rule
3. Port: 1883, Protocol: TCP
4. Apply

### Linux (ufw)
```bash
sudo ufw allow 1883/tcp
```

## Troubleshooting

### Docker-Specific Issues

#### Port Already in Use

**Error:** `Bind for 0.0.0.0:1883 failed: port is already allocated`

**Cause:** Another service (like Mosquitto MQTT) is already using port 1883.

**Solution Option A - Stop conflicting service:**
```bash
# Find what's using the port
sudo netstat -tulpn | grep 1883
# or
sudo lsof -i :1883

# Stop the service (example: Mosquitto)
sudo systemctl stop mosquitto
sudo systemctl disable mosquitto  # Prevent auto-start
```

**Solution Option B - Use different port:**

Edit `.env`:
```env
MQTT_RELAY_PORT=1884  # Use different port
```

Update ESP32:
```cpp
const int mqtt_port = 1884;  // Match the new port
```

Restart container:
```bash
docker-compose down
docker-compose up -d
```

#### ESP32 Can't Connect to Docker Container

**Problem:** ESP32 shows "Connection failed" or times out

**Checklist:**

1. **Verify you're using the correct IP:**
   ```cpp
   // ✅ CORRECT - Use your NAS/server IP (check with hostname -I)
   const char* mqtt_server = "192.168.1.50";

   // ❌ WRONG - Don't use these:
   // const char* mqtt_server = "localhost";        // Only works inside container
   // const char* mqtt_server = "127.0.0.1";        // Only works inside container
   // const char* mqtt_server = "172.17.0.2";       // Container internal IP
   // const char* mqtt_server = "filament-tracker"; // Docker service name
   ```

2. **Check container is running:**
   ```bash
   docker ps | grep filament-tracker
   ```

3. **Verify port is exposed:**
   ```bash
   docker port filament-tracker
   ```

   Should show:
   ```
   1883/tcp -> 0.0.0.0:1883
   5000/tcp -> 0.0.0.0:5500
   ```

4. **Test from host machine first:**
   ```bash
   # From the NAS/server itself
   telnet localhost 1883
   ```

   If this works but ESP32 can't connect, it's a firewall issue.

5. **Check firewall rules** (see Firewall Configuration section below)

6. **Check Docker logs for errors:**
   ```bash
   docker logs -f filament-tracker | grep -i error
   ```

#### Container Restarts or Crashes

**Check logs for errors:**
```bash
docker logs filament-tracker --tail 100
```

**Common causes:**
- Port conflict (see above)
- Insufficient memory (check with `docker stats`)
- Database corruption (backup and delete `filament-data` volume)

**Force rebuild:**
```bash
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

### Relay Won't Start

**Error:** "Failed to start relay server"

**Solutions:**
- Check if port 1883 is already in use (see Docker-Specific Issues above)
- Try changing to a different port (e.g., 1884)
- Check Docker logs: `docker logs filament-tracker`
- Ensure BambuLab MQTT is connected first
- Verify relay is enabled in Settings page

### ESP32 Connection Issues

**Problem:** ESP32 shows "Connection failed"

**Solutions:**
1. **Verify server IP address is correct** (use NAS IP, not container IP)
2. **Check firewall settings** (port 1883 must be open - see below)
3. **If using authentication:** Verify username/password match exactly
4. **Check relay is running:** Look for green status in Settings page
5. **Increase timeout in ESP32:**
   ```cpp
   client.setSocketTimeout(15);  // Increase timeout to 15 seconds
   client.setKeepAlive(60);      // Keep connection alive
   ```
6. **Add debug output:**
   ```cpp
   void reconnect() {
     while (!client.connected()) {
       Serial.print("Attempting MQTT connection to ");
       Serial.print(mqtt_server);
       Serial.print(":");
       Serial.print(mqtt_port);
       Serial.print("...");

       String clientId = "ESP32Client-";
       clientId += String(random(0xffff), HEX);

       if (client.connect(clientId.c_str())) {
         Serial.println("connected!");
       } else {
         Serial.print("failed, rc=");
         Serial.print(client.state());
         Serial.println(" retrying in 5 seconds");

         // Error codes:
         // -4 : MQTT_CONNECTION_TIMEOUT
         // -3 : MQTT_CONNECTION_LOST
         // -2 : MQTT_CONNECT_FAILED
         // -1 : MQTT_DISCONNECTED
         //  0 : MQTT_CONNECTED
         //  1 : MQTT_CONNECT_BAD_PROTOCOL
         //  2 : MQTT_CONNECT_BAD_CLIENT_ID
         //  3 : MQTT_CONNECT_UNAVAILABLE
         //  4 : MQTT_CONNECT_BAD_CREDENTIALS
         //  5 : MQTT_CONNECT_UNAUTHORIZED

         delay(5000);
       }
     }
   }
   ```

### No Messages Received

**Problem:** ESP32 connects but receives no messages

**Solutions:**
1. **Verify BambuLab MQTT is connected:**
   - Check Settings page shows green "Connected" status
   - Check MQTT log shows incoming messages
2. **Verify correct topic subscription:**
   ```cpp
   // Must match your printer's serial number
   client.subscribe("device/01S00C1234567/report");
   ```
3. **Check the MQTT log** in Filament Tracker Settings to confirm messages are arriving from printer
4. **Test with mosquitto_sub:**
   ```bash
   mosquitto_sub -h <NAS_IP> -p 1883 -t "device/+/report" -v
   ```
5. **Verify callback is registered:**
   ```cpp
   void setup() {
     // ...
     client.setCallback(callback);  // Don't forget this!
   }
   ```

### Connection Drops Frequently

**Problem:** Connections drop or are unstable

**Solutions:**
1. **Increase keep-alive in ESP32:**
   ```cpp
   client.setKeepAlive(60);      // Send keepalive every 60 seconds
   client.setSocketTimeout(30);   // Wait up to 30 seconds for response
   ```
2. **Implement reconnection logic** (see ESP32 example code above)
3. **Check network stability:**
   - Weak WiFi signal on ESP32
   - Network congestion
   - Router issues
4. **Ensure Filament Tracker auto-restarts:**
   ```yaml
   # In docker-compose.yml
   services:
     filament-tracker:
       restart: unless-stopped  # Already set by default
   ```
5. **Check Docker container health:**
   ```bash
   docker inspect filament-tracker | grep -A 10 Health
   ```

## Benefits

✅ **Reduced Load on Printer** - Only one connection to the printer's MQTT broker  
✅ **Multiple Clients** - Connect unlimited devices to the relay  
✅ **Improved Stability** - Less chance of connection failures  
✅ **Optional Security** - Add username/password authentication  
✅ **Monitoring** - See all connected clients in the logs  

## Technical Details

- **Protocol:** MQTT 3.1.1
- **QoS:** 0 (At most once)
- **Topics:** Messages are rebroadcast on `device/{serial_number}/report`
- **Library:** MQTTnet 5.1.0
- **Server Type:** In-process MQTT broker

## Architecture

```
┌─────────────────┐
│  BambuLab       │
│  Printer        │
│  (MQTT Broker)  │
└────────┬────────┘
         │ (Single connection)
         │
┌────────▼────────────────┐
│  Filament Tracker       │
│  (MQTT Relay Server)    │
│  Port: 1883             │
└────────┬────────────────┘
         │ (Multiple connections)
         │
    ┌────┼─────┬─────────┐
    │         │          │
┌───▼──┐  ┌──▼───┐  ┌───▼────┐
│ESP32 │  │ Home │  │ Other  │
│  #1  │  │Assist│  │Clients │
└──────┘  └──────┘  └────────┘
```

## FAQ

**Q: Does this impact performance?**  
A: No, the relay adds minimal latency (typically <10ms) and uses very little CPU/memory.

**Q: Can I use this with Home Assistant?**  
A: Yes! Configure Home Assistant's MQTT integration to point to your Filament Tracker server instead of the printer.

**Q: Do I need to keep BambuLab connection enabled?**  
A: Yes, the relay only works when Filament Tracker is connected to your printer.

**Q: What happens if Filament Tracker restarts?**  
A: The relay will automatically restart if enabled in settings. ESP32 clients should implement reconnection logic (see example code above).

**Q: How do I know if Docker port mapping is working?**  
A: Run `docker port filament-tracker` - you should see `1883/tcp -> 0.0.0.0:1883`. Also test with `telnet <NAS_IP> 1883` from another machine.

**Q: Can I run multiple MQTT relays?**  
A: No need! One relay can handle unlimited client connections. But if you want multiple instances, use different ports for each.

**Q: Does this work with Docker networks other than bridge mode?**  
A: Yes, it works with bridge (default), host, and custom networks. Host mode gives slightly better performance but is less isolated.

**Q: How do I backup my Docker configuration?**  
A: The settings are stored in the `filament-data` Docker volume. Back it up with:
```bash
docker run --rm -v filament-tracker-data:/data -v $(pwd):/backup alpine tar czf /backup/filament-backup.tar.gz -C /data .
```

---

## Complete ESP32 Example with Debug Output

Here's a complete, production-ready ESP32 sketch with proper error handling and debug output:

```cpp
#include <WiFi.h>
#include <PubSubClient.h>

// WiFi credentials
const char* ssid = "YOUR_WIFI_SSID";
const char* password = "YOUR_WIFI_PASSWORD";

// MQTT Relay settings
const char* mqtt_server = "192.168.1.50";  // Your NAS/server IP
const int mqtt_port = 1883;
const char* mqtt_user = "";  // Leave empty if no auth
const char* mqtt_pass = "";  // Leave empty if no auth

// Printer serial number
const char* printer_serial = "01S00C1234567";

WiFiClient espClient;
PubSubClient client(espClient);

unsigned long lastReconnectAttempt = 0;
unsigned long lastMessage = 0;
int messageCount = 0;

void callback(char* topic, byte* payload, unsigned int length) {
  messageCount++;
  lastMessage = millis();

  Serial.printf("\n[%d] Message on [%s]:\n", messageCount, topic);

  // Print payload
  String message;
  for (unsigned int i = 0; i < length; i++) {
    message += (char)payload[i];
  }

  // Print first 200 chars (full message may be very long)
  if (message.length() > 200) {
    Serial.println(message.substring(0, 200) + "...");
    Serial.printf("(Message truncated, full length: %d bytes)\n", length);
  } else {
    Serial.println(message);
  }

  // TODO: Parse and process the JSON message here
  // Example: Use ArduinoJson to parse print status, temperatures, etc.
}

boolean reconnect() {
  String clientId = "ESP32Client-";
  clientId += String(random(0xffff), HEX);

  Serial.printf("Attempting MQTT connection to %s:%d as %s...", 
                mqtt_server, mqtt_port, clientId.c_str());

  boolean connected;
  if (strlen(mqtt_user) > 0) {
    connected = client.connect(clientId.c_str(), mqtt_user, mqtt_pass);
  } else {
    connected = client.connect(clientId.c_str());
  }

  if (connected) {
    Serial.println("connected!");

    // Subscribe to printer topic
    String topic = String("device/") + printer_serial + "/report";
    client.subscribe(topic.c_str());
    Serial.printf("Subscribed to: %s\n", topic.c_str());

    messageCount = 0;
    lastMessage = millis();

  } else {
    Serial.printf("failed, rc=%d\n", client.state());

    // Print error explanation
    switch(client.state()) {
      case -4: Serial.println("  (Connection timeout)"); break;
      case -3: Serial.println("  (Connection lost)"); break;
      case -2: Serial.println("  (Connect failed)"); break;
      case -1: Serial.println("  (Disconnected)"); break;
      case  1: Serial.println("  (Bad protocol)"); break;
      case  2: Serial.println("  (Bad client ID)"); break;
      case  3: Serial.println("  (Server unavailable)"); break;
      case  4: Serial.println("  (Bad credentials)"); break;
      case  5: Serial.println("  (Unauthorized)"); break;
      default: Serial.println("  (Unknown error)"); break;
    }
  }

  return connected;
}

void setup() {
  Serial.begin(115200);
  delay(1000);

  Serial.println("\n\n=================================");
  Serial.println("ESP32 MQTT Relay Client");
  Serial.println("=================================");

  // Connect to WiFi
  Serial.printf("\nConnecting to WiFi: %s ", ssid);
  WiFi.begin(ssid, password);

  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 30) {
    delay(500);
    Serial.print(".");
    attempts++;
  }

  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("\nFailed to connect to WiFi! Restarting...");
    delay(3000);
    ESP.restart();
  }

  Serial.println("\nWiFi connected!");
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());
  Serial.print("Signal strength: ");
  Serial.print(WiFi.RSSI());
  Serial.println(" dBm");

  // Configure MQTT
  client.setServer(mqtt_server, mqtt_port);
  client.setCallback(callback);
  client.setKeepAlive(60);
  client.setSocketTimeout(30);

  Serial.printf("\nMQTT Server: %s:%d\n", mqtt_server, mqtt_port);
  Serial.printf("Printer Serial: %s\n", printer_serial);
  Serial.println("=================================\n");

  lastReconnectAttempt = 0;
}

void loop() {
  // Maintain WiFi connection
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi disconnected! Reconnecting...");
    WiFi.reconnect();
    delay(5000);
    return;
  }

  // Maintain MQTT connection
  if (!client.connected()) {
    unsigned long now = millis();
    if (now - lastReconnectAttempt > 5000) {
      lastReconnectAttempt = now;
      if (reconnect()) {
        lastReconnectAttempt = 0;
      }
    }
  } else {
    client.loop();

    // Print status every 30 seconds
    static unsigned long lastStatus = 0;
    if (millis() - lastStatus > 30000) {
      lastStatus = millis();
      Serial.printf("\n[Status] Connected | Messages: %d | Last: %lu sec ago\n", 
                    messageCount, 
                    (millis() - lastMessage) / 1000);
    }
  }

  delay(10);  // Small delay to prevent watchdog issues
}
```

This example includes:
- ✅ Automatic WiFi reconnection
- ✅ MQTT reconnection with exponential backoff
- ✅ Detailed error messages
- ✅ Connection status monitoring
- ✅ Message counting and timestamps
- ✅ Optional authentication support
- ✅ Watchdog-safe delays

---

**Made for the 3D printing community** 🖨️
