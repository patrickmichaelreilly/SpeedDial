# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SpeedDial is an ASP.NET Core 8.0 Windows service that provides unified DNS and reverse proxy management. It orchestrates Technitium DNS Server and Nginx Proxy Manager via Docker to simplify hostname mapping for internal services through a single web interface.

## Core Architecture

### Service Layer Architecture
- **ServiceOrchestrator**: Coordinates atomic operations between DNS and proxy services with rollback capabilities
- **ConfigurationService**: Manages persistent JSON-based hostname mappings with soft-delete pattern
- **TechnitiumDnsService**: Handles DNS A record management via Technitium DNS Server API
- **NginxProxyManagerService**: Manages reverse proxy host configuration via NPM API
- **DockerService**: Controls Docker Compose lifecycle and container health monitoring

### Data Flow
1. User submits hostname mapping via web UI (hostname → targetIP:port)
2. ServiceOrchestrator performs atomic operation:
   - Creates DNS A record pointing hostname to proxy server IP
   - Creates proxy host configuration in NPM
   - Saves mapping to persistent configuration
   - Rolls back all changes if any step fails

### Configuration Management
- Main app configuration: `appsettings.json` (API endpoints, credentials)
- Hostname mappings: `mappings.json` (persistent storage with soft-delete)
- Docker orchestration: `docker-compose.yml` (Technitium DNS + NPM containers)

## Development Commands

### Build and Run
```bash
# Build the application
dotnet build

# Run in development mode (port 5177)
dotnet run

# Build for Windows production deployment
dotnet publish -c Release -r win-x64 --self-contained -o publish
```

### Docker Management
```bash
# Start DNS and proxy containers
docker compose up -d

# View container status
docker compose ps

# View logs
docker compose logs dns-server
docker compose logs nginx-proxy-manager

# Stop containers
docker compose down
```

### Testing Services
The application runs on port 5555 in production, with development ports configured in `Properties/launchSettings.json`:
- Development HTTP: http://localhost:5177
- Development HTTPS: https://localhost:7172

External service interfaces:
- Technitium DNS Admin: http://localhost:5380
- Nginx Proxy Manager Admin: http://localhost:81

## Key Implementation Patterns

### Atomic Operations with Rollback
All hostname mapping operations use a transaction-like pattern in `ServiceOrchestrator:24`. If any step fails, previous changes are automatically rolled back to maintain system consistency.

### Health Monitoring
The system continuously monitors service health through HTTP endpoint checks and Docker container status. Status information is displayed in the web UI and logged for debugging.

### Soft Delete Pattern
Hostname mappings use soft deletion (IsActive flag) rather than physical removal, preserving audit trails and enabling potential recovery operations.

### Error Handling Strategy
- All service methods return tuple patterns `(bool Success, string Message)` for consistent error handling
- Comprehensive logging at Info/Warning/Error levels for operational visibility
- Graceful degradation when external services are unavailable

## Production Deployment

The application is designed to run as a Windows service installed via `install.bat`:
- Service Name: "SpeedDial"
- Installation Path: `C:\SpeedDial\`
- Automatic startup with Windows
- Integrated firewall rule management
- Docker container lifecycle management

Use `kill.bat` for complete service removal and cleanup.

## Configuration Structure

### appsettings.json
```json
{
  "SpeedDial": {
    "ProxyServerIP": "127.0.0.1",
    "TechnitiumDns": {
      "BaseUrl": "http://localhost:5380",
      "ApiToken": ""
    },
    "NginxProxyManager": {
      "BaseUrl": "http://localhost:81",
      "Email": "admin@example.com",
      "Password": "changeme",
      "Token": ""
    },
    "DockerComposePath": "docker-compose.yml"
  }
}
```

### mappings.json Structure
```json
{
  "mappings": [
    {
      "id": "dns_20250101120000_abc123",
      "hostname": "service.local",
      "targetIP": "192.168.1.100",
      "targetPort": 8080,
      "createdAt": "2025-01-01T12:00:00Z",
      "isActive": true
    }
  ],
  "lastUpdated": "2025-01-01T12:00:00Z"
}
```

## Troubleshooting

### Common Issues
- Docker not running: Service health checks will fail
- Port conflicts: Check ports 53, 80, 81, 443, 5380, 5555
- DNS/Proxy API authentication: Verify tokens in appsettings.json
- Configuration file corruption: mappings.json uses soft-delete recovery

### Log Locations
- Application logs: Windows Event Log (when running as service)
- Docker container logs: `docker compose logs [service-name]`
- Configuration file: Check write permissions on mappings.json path