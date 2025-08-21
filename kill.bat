@echo off

:: Save the current directory to return to it later
set "ORIGINAL_DIR=%CD%"

echo ========================================
echo SpeedDial Kill Script
echo ========================================
echo.

:: Check if running as administrator
net session >nul 2>&1
if errorlevel 1 (
    echo ERROR: This script must be run as Administrator!
    echo Please right-click and select "Run as administrator"
    pause
    exit /b 1
)

echo [1/4] Stopping SpeedDial service...
sc stop SpeedDial >nul 2>&1
timeout /t 3 /nobreak >nul

echo [2/4] Stopping Docker containers...
cd /d "C:\SpeedDial"
docker compose down >nul 2>&1

echo [3/4] Removing SpeedDial service...
sc delete SpeedDial >nul 2>&1

echo [4/4] Removing firewall rules...
netsh advfirewall firewall delete rule name="SpeedDial-Web" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-DNS-UDP" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-DNS-TCP" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-HTTP" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-HTTPS" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-NPM-Admin" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-DNS-Admin" >nul 2>&1

echo.
echo ========================================
echo Kill Complete!
echo ========================================
echo.
echo SpeedDial service stopped and removed
echo Docker containers stopped
echo Firewall rules removed
echo.
echo NOTE: C:\SpeedDial directory and Docker volumes remain
echo To completely remove, manually delete:
echo - C:\SpeedDial directory
echo - Docker volumes: docker volume prune
echo.

:: Return to original directory
cd /d "%ORIGINAL_DIR%" >nul 2>&1

pause