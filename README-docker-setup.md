# Docker Compose Setup: Technitium DNS Server + Nginx Proxy Manager

This docker-compose.yml file deploys and manages both Technitium DNS Server and Nginx Proxy Manager with proper networking and persistent storage.

## Services Overview

### 1. Technitium DNS Server
- **Official Image**: `technitium/dns-server:latest`
- **Container Name**: `technitium-dns-server`
- **Web Interface**: http://localhost:5380
- **Function**: Privacy-focused DNS server with ad-blocking capabilities

### 2. Nginx Proxy Manager
- **Official Image**: `jc21/nginx-proxy-manager:latest`
- **Container Name**: `nginx-proxy-manager`
- **Admin Interface**: http://localhost:81
- **Function**: Web-based nginx proxy manager with Let's Encrypt integration

## Port Mappings

### Technitium DNS Server
- **5380/tcp**: DNS web console (HTTP)
- **53/udp & 53/tcp**: Standard DNS service ports

### Nginx Proxy Manager
- **80/tcp**: Public HTTP port
- **443/tcp**: Public HTTPS port
- **81/tcp**: Admin web interface

## Volume Configuration

### Technitium DNS Server
- `dns_config:/etc/dns` - Persistent DNS server configuration and zone files

### Nginx Proxy Manager
- `npm_data:/data` - Application data, configurations, and settings
- `npm_letsencrypt:/etc/letsencrypt` - SSL certificates from Let's Encrypt

## Networking

- **Custom Bridge Network**: `dns_proxy_network` (172.20.0.0/16)
- **Container Communication**: Both containers can communicate by service name
- **DNS Resolution**: Automatic service discovery between containers

## Quick Start

1. **Deploy the stack**:
   ```bash
   docker compose up -d
   ```

2. **Access Technitium DNS Server**:
   - Open http://localhost:5380
   - Complete initial setup wizard
   - Configure DNS zones and forwarders as needed

3. **Access Nginx Proxy Manager**:
   - Open http://localhost:81
   - Default credentials:
     - Email: `admin@example.com`
     - Password: `changeme`
   - Change credentials immediately after first login

## Environment Variables

### Technitium DNS Server
- `DNS_SERVER_DOMAIN`: Primary domain name for the DNS server
- `DNS_SERVER_ENABLE_BLOCKING`: Enable ad-blocking functionality
- `DNS_SERVER_FORWARDERS`: Upstream DNS servers (1.1.1.1, 8.8.8.8)
- `DNS_SERVER_FORWARDER_PROTOCOL`: Protocol for upstream queries
- `DNS_SERVER_LOG_USING_LOCAL_TIME`: Use local timezone for logs

### Nginx Proxy Manager
- `DISABLE_IPV6`: Disable IPv6 if not supported on host
- `X_FRAME_OPTIONS`: Security header configuration
- `DB_SQLITE_FILE`: SQLite database location (default setup)

## Optional Configurations

### Extended DNS Ports (Uncomment if needed)
- **53443/tcp**: DNS web console (HTTPS)
- **853/udp & 853/tcp**: DNS-over-QUIC and DNS-over-TLS
- **443/udp & 443/tcp**: DNS-over-HTTPS (HTTP/3 and HTTP/1.1/2)
- **80/tcp & 8053/tcp**: DNS-over-HTTP
- **67/udp**: DHCP service

### MariaDB Database (Alternative to SQLite)
Uncomment the `npm-db` service section and related environment variables in the Nginx Proxy Manager service to use MariaDB instead of SQLite.

### Host Network Mode
For DHCP functionality, uncomment `network_mode: host` in the DNS server service and remove port mappings.

## Management Commands

```bash
# Start services
docker compose up -d

# Stop services
docker compose down

# View logs
docker compose logs -f dns-server
docker compose logs -f nginx-proxy-manager

# Update images
docker compose pull
docker compose up -d

# Backup data
docker run --rm -v dns_config:/data -v $(pwd):/backup alpine tar czf /backup/dns-backup.tar.gz -C /data .
docker run --rm -v npm_data:/data -v $(pwd):/backup alpine tar czf /backup/npm-backup.tar.gz -C /data .
```

## Security Considerations

1. **Change default passwords** immediately after setup
2. **Configure firewall rules** for DNS (port 53) and web interfaces
3. **Use strong passwords** for admin accounts
4. **Regular updates** of Docker images
5. **Monitor logs** for suspicious activities

## Integration Usage

### Using Technitium as Local DNS
1. Configure your router or devices to use the server IP as DNS
2. Set up custom DNS zones for local services
3. Configure ad-blocking lists for enhanced privacy

### Nginx Proxy Manager Setup
1. Create proxy hosts for internal services
2. Enable SSL certificates via Let's Encrypt
3. Configure access lists for security
4. Set up custom locations and redirects

## Troubleshooting

### DNS Server Issues
- Check container logs: `docker compose logs dns-server`
- Verify port 53 is not used by system resolver
- Ensure proper firewall configuration

### Proxy Manager Issues
- Check container logs: `docker compose logs nginx-proxy-manager`
- Verify port 80/443 are not used by other services
- Check SSL certificate generation in logs

### Network Issues
- Verify custom network creation: `docker network ls`
- Test container communication: `docker compose exec nginx-proxy-manager ping dns-server`
- Check port conflicts with: `netstat -tlnp`

## Architecture Notes

This setup creates an isolated network environment where:
- Both services can communicate securely
- DNS server provides local resolution
- Proxy manager handles external SSL termination
- Persistent storage ensures data survives container restarts
- Optional database scaling available with MariaDB