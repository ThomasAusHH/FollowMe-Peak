# FollowMe-Peak Server Deployment auf Strato VPS

## Voraussetzungen auf dem VPS
- Ubuntu Server
- Docker und Docker Compose installiert
- SSH-Zugriff zum VPS

## 1. Projekt auf VPS übertragen

```bash
# Via Git (empfohlen)
git clone <your-repo-url>
cd FollowMe-Peak/server

# Oder via SCP
scp -r ./server/ user@your-vps-ip:/home/user/followme-peak-server/
```

## 2. Umgebungsvariablen konfigurieren

```bash
# .env.production bearbeiten
nano .env.production

# WICHTIG: API Keys ändern!
API_KEY_ADMIN=ihr-sicherer-admin-key-hier
API_KEY_USER=ihr-sicherer-user-key-hier
```

## 3. Docker Container starten

```bash
# Container bauen und starten
docker-compose up -d

# Logs prüfen
docker-compose logs -f

# Status prüfen
docker-compose ps
```

## 4. Zugriff konfigurieren

### Via IP-Adresse
Ihr Server läuft dann auf: `http://VPS-IP:3000`

### Via DynDNS (empfohlen)
1. DynDNS-Service einrichten (z.B. NoIP, DynDNS)
2. Subdomain erstellen: `followme.ihredyndnsdomain.com`
3. Zugriff über: `http://followme.ihredyndnsdomain.com:3000`

### Mit Reverse Proxy (optional, für saubere URLs)
Falls Sie bereits nginx laufen haben:

```bash
# nginx config erweitern
sudo nano /etc/nginx/sites-available/default

# Location block hinzufügen:
location /followme-api/ {
    proxy_pass http://localhost:3000/;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
}

# nginx neuladen
sudo nginx -t && sudo systemctl reload nginx
```

## 5. Firewall konfigurieren

```bash
# Port 3000 öffnen
sudo ufw allow 3000/tcp

# Oder nur für bestimmte IPs
sudo ufw allow from IHRE_IP to any port 3000
```

## 6. SSL/HTTPS (optional mit Let's Encrypt)

```bash
# Certbot installieren
sudo apt install certbot python3-certbot-nginx

# SSL-Zertifikat erstellen (nur bei eigener Domain)
sudo certbot --nginx -d ihre-domain.com
```

## 7. Client-Plugin konfigurieren

In Ihrer Unity Mod die ServerConfig anpassen:
- Server URL: `http://VPS-IP:3000` oder `http://ihredyndns.com:3000`
- API Keys entsprechend setzen

## Management Commands

```bash
# Container stoppen
docker-compose down

# Container neustarten
docker-compose restart

# Updates einspielen
git pull
docker-compose down
docker-compose build
docker-compose up -d

# Logs einsehen
docker-compose logs -f followme-peak-api

# In Container einsteigen (für Debugging)
docker-compose exec followme-peak-api sh
```

## Backup

```bash
# Database backup
cp ./data/climbs.db ./backup/climbs_$(date +%Y%m%d).db

# Automatisches tägliches Backup
echo "0 2 * * * cp /path/to/followme-peak-server/data/climbs.db /path/to/backup/climbs_\$(date +\%Y\%m\%d).db" | crontab -
```

## Monitoring

```bash
# Resource usage
docker stats followme-peak-api

# Health check
curl http://localhost:3000/api/health

# Server status
systemctl status docker
docker-compose ps
```

## Troubleshooting

### Container startet nicht
```bash
docker-compose logs followme-peak-api
```

### Port bereits belegt
```bash
# Anderen Port verwenden
# In docker-compose.yml: "3001:3000"
```

### Datenbank Probleme
```bash
# Container neu bauen
docker-compose down -v
docker-compose up -d
```

### Performance Probleme
```bash
# Resource limits in docker-compose.yml anpassen
# Mehr CPU/Memory zuweisen
```