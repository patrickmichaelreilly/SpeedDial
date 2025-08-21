# SpeedDial - DNS & Proxy Manager MVP

## What It Does
Windows service that deploys and manages Technitium DNS Server and Nginx Proxy Manager via Docker, providing a single web UI to configure hostname mappings that automatically set up both DNS records and reverse proxy rules.

## Problem
Need to configure DNS and reverse proxy separately for each internal service. SpeedDial does both automatically through one simple form.

## Technical Stack
- ASP.NET Core 8.0 Windows Service
- Docker Compose for Technitium DNS + Nginx Proxy Manager
- Bootstrap 5 UI on http://localhost:5555
- Installation via install.bat

## Files in C:\SpeedDial
- SpeedDial.exe (main service)
- appsettings.json (config)
- docker-compose.yml (containers)
- install.bat (installer)

## MVP Features
1. One-command installation (install.bat)
2. Auto-deploys both services as Docker containers
3. Web UI to add/delete hostname mappings
4. Atomic operations (both services or rollback)
5. Survives Windows restart

## User Workflow
1. Run install.bat as Administrator → Everything deploys
2. Browse to http://localhost:5555
3. Add mapping: hostname + target IP + port → Click Add
4. Access services by hostname without port numbers

## Development Plan
**Phase 1:** ASP.NET service, Windows service hosting, Docker Compose setup
**Phase 2:** API integrations, UI, installer script

## Requirements
- Windows 10/11 Pro or Server 2019/2022
- Docker Desktop installed
- Ports: 53, 80, 81, 443, 5380, 5555

## Success = 
User can map "printer.local → 192.168.1.50:8080" in 30 seconds with zero configuration.