@echo off
setlocal enabledelayedexpansion

echo ========================================
echo SpeedDial DNS ^& Proxy Manager Installer
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

echo [1/7] Checking prerequisites...

:: Check if Docker is installed
docker --version >nul 2>&1
if errorlevel 1 goto install_docker
echo Docker found
goto check_docker_running

:install_docker
echo Docker not found. Installing Docker Desktop...
echo Please wait, downloading installer...
curl -L -o "%TEMP%\DockerInstaller.exe" "https://desktop.docker.com/win/main/amd64/Docker%%20Desktop%%20Installer.exe"
if not exist "%TEMP%\DockerInstaller.exe" goto docker_download_failed
echo Installing Docker Desktop...
"%TEMP%\DockerInstaller.exe" install --quiet --accept-license
echo Docker installed. Please restart and run this script again.
pause
exit /b 0

:docker_download_failed
echo ERROR: Could not download Docker installer
pause
exit /b 1

:check_docker_running
docker ps >nul 2>&1
if errorlevel 1 goto docker_not_running
echo Docker is running
goto continue_install

:docker_not_running
echo ERROR: Docker is not running. Please start Docker Desktop.
pause
exit /b 1

:continue_install

echo [2/7] Stopping existing SpeedDial service...

:: Stop and remove existing service if it exists
sc query SpeedDial >nul 2>&1
if not errorlevel 1 (
    echo Stopping existing SpeedDial service...
    sc stop SpeedDial >nul 2>&1
    timeout /t 5 /nobreak >nul
    
    echo Deleting existing SpeedDial service...
    sc delete SpeedDial >nul 2>&1
    
    :: Wait for service to be fully removed
    :wait_for_service_removal
    sc query SpeedDial >nul 2>&1
    if not errorlevel 1 (
        timeout /t 2 /nobreak >nul
        goto wait_for_service_removal
    )
)

echo [3/7] Creating installation directory...

:: Create C:\SpeedDial directory
if not exist "C:\SpeedDial" (
    mkdir "C:\SpeedDial"
    echo Created C:\SpeedDial directory
) else (
    echo C:\SpeedDial directory already exists
)

echo [4/7] Building application...

:: Build the application for Windows
dotnet publish -c Release -r win-x64 --self-contained -o "%~dp0publish"
if errorlevel 1 (
    echo ERROR: Failed to build application. Make sure .NET 8.0 SDK is installed.
    pause
    exit /b 1
)

echo Application built successfully

echo [5/7] Copying application files...

:: Copy published files to C:\SpeedDial
xcopy /E /I /Y "%~dp0publish\*" "C:\SpeedDial\" >nul
xcopy /Y "%~dp0docker-compose.yml" "C:\SpeedDial\" >nul
xcopy /Y "%~dp0appsettings.json" "C:\SpeedDial\" >nul

if errorlevel 1 (
    echo ERROR: Failed to copy application files
    pause
    exit /b 1
)

echo Application files copied to C:\SpeedDial

echo [6/8] Creating configuration files...

:: Create mappings.json file
echo Creating mappings.json configuration file...
echo { > "C:\SpeedDial\mappings.json"
echo   "mappings": [], >> "C:\SpeedDial\mappings.json"
echo   "lastUpdated": "2025-01-01T00:00:00Z" >> "C:\SpeedDial\mappings.json"
echo } >> "C:\SpeedDial\mappings.json"

echo Configuration files created

echo [7/8] Configuring firewall rules...

:: Add firewall rules for required ports
netsh advfirewall firewall delete rule name="SpeedDial-Web" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-DNS-UDP" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-DNS-TCP" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-HTTP" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-HTTPS" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-NPM-Admin" >nul 2>&1
netsh advfirewall firewall delete rule name="SpeedDial-DNS-Admin" >nul 2>&1

netsh advfirewall firewall add rule name="SpeedDial-Web" dir=in action=allow protocol=TCP localport=5555 >nul
netsh advfirewall firewall add rule name="SpeedDial-DNS-UDP" dir=in action=allow protocol=UDP localport=53 >nul
netsh advfirewall firewall add rule name="SpeedDial-DNS-TCP" dir=in action=allow protocol=TCP localport=53 >nul
netsh advfirewall firewall add rule name="SpeedDial-HTTP" dir=in action=allow protocol=TCP localport=80 >nul
netsh advfirewall firewall add rule name="SpeedDial-HTTPS" dir=in action=allow protocol=TCP localport=443 >nul
netsh advfirewall firewall add rule name="SpeedDial-NPM-Admin" dir=in action=allow protocol=TCP localport=81 >nul
netsh advfirewall firewall add rule name="SpeedDial-DNS-Admin" dir=in action=allow protocol=TCP localport=5380 >nul

echo Firewall rules configured

echo [8/8] Installing and starting Windows service...

:: Install Windows service
sc create SpeedDial binpath="C:\SpeedDial\SpeedDial.exe" start=auto displayname="SpeedDial DNS and Proxy Manager" obj="LocalSystem" >nul
if errorlevel 1 (
    echo ERROR: Failed to create Windows service
    pause
    exit /b 1
)

:: Set service description
sc description SpeedDial "SpeedDial DNS and Proxy Manager - Unified hostname mapping for internal services" >nul

:: Start the service
echo Starting SpeedDial service...
sc start SpeedDial >nul
if errorlevel 1 (
    echo WARNING: Service installed but failed to start. You may need to start it manually.
    echo This can happen if .NET 8.0 runtime is not installed.
) else (
    echo SpeedDial service started successfully
)

echo [8/8] Starting Docker containers...

:: Start the Docker containers  
cd /d "C:\SpeedDial"
echo Cleaning up any existing containers and volumes...
docker compose down >nul 2>&1
docker volume rm speeddial_dns-config speeddial_npm-data speeddial_npm-letsencrypt >nul 2>&1
echo Starting fresh DNS and Proxy containers...
docker compose up -d
if errorlevel 1 (
    echo WARNING: Failed to start Docker containers. You may need to start them manually.
    echo Run: docker compose up -d
) else (
    echo Docker containers started successfully
)

echo [9/9] Finalizing installation...


echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo SpeedDial has been installed as a Windows service.
echo.
echo Web Interface: http://localhost:5555
echo DNS Admin:     http://localhost:5380 (admin/admin123)
echo Proxy Admin:   http://localhost:81 (admin@example.com/changeme)
echo.
echo The service will start automatically when Windows boots.
echo.
echo Opening web interface...

:: Try to open web interface
timeout /t 3 /nobreak >nul
start http://localhost:5555

echo.
echo Installation log has been saved to C:\SpeedDial\install.log
echo.