using SpeedDial.Models;

namespace SpeedDial.Services;

public class ServiceOrchestrator
{
    private readonly ConfigurationService _configService;
    private readonly TechnitiumDnsService _dnsService;
    private readonly NginxProxyManagerService _proxyService;
    private readonly ILogger<ServiceOrchestrator> _logger;

    public ServiceOrchestrator(
        ConfigurationService configService,
        TechnitiumDnsService dnsService,
        NginxProxyManagerService proxyService,
        ILogger<ServiceOrchestrator> logger)
    {
        _configService = configService;
        _dnsService = dnsService;
        _proxyService = proxyService;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> AddHostnameMappingAsync(string hostname, string targetIp, int targetPort)
    {
        _logger.LogInformation("Adding hostname mapping: {Hostname} -> {TargetIp}:{TargetPort}", hostname, targetIp, targetPort);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(hostname))
            return (false, "Hostname is required");
        
        if (string.IsNullOrWhiteSpace(targetIp))
            return (false, "Target IP is required");
        
        if (targetPort <= 0 || targetPort > 65535)
            return (false, "Target port must be between 1 and 65535");

        // Check if mapping already exists
        var existingMapping = _configService.GetMappingByHostname(hostname);
        if (existingMapping != null)
        {
            return (false, $"Hostname '{hostname}' is already mapped");
        }

        var mapping = new HostnameMapping
        {
            Hostname = hostname.ToLowerInvariant(),
            TargetIP = targetIp,
            TargetPort = targetPort
        };

        bool dnsSuccess = false;
        int? proxyHostId = null;

        try
        {
            // Step 1: Create DNS A record
            _logger.LogInformation("STEP 1: Creating DNS A record for {Hostname} -> {TargetIp}", hostname, targetIp);
            dnsSuccess = await _dnsService.AddARecordAsync(hostname, targetIp);
            _logger.LogInformation("DNS A record creation result: {Success}", dnsSuccess);
            
            if (!dnsSuccess)
            {
                _logger.LogError("DNS A record creation failed for {Hostname}", hostname);
                return (false, "Failed to create DNS record");
            }

            // Step 2: Create proxy host
            _logger.LogInformation("STEP 2: Creating proxy host for {Hostname} -> {TargetIp}:{TargetPort}", hostname, targetIp, targetPort);
            var (proxySuccess, createdProxyHostId) = await _proxyService.CreateProxyHostAsync(hostname, targetIp, targetPort);
            _logger.LogInformation("Proxy host creation result: Success={Success}, ID={ProxyHostId}", proxySuccess, createdProxyHostId);
            
            if (!proxySuccess)
            {
                // Rollback DNS record
                _logger.LogError("Proxy host creation failed, rolling back DNS record for {Hostname}", hostname);
                await _dnsService.DeleteARecordAsync(hostname);
                return (false, "Failed to create proxy host");
            }

            proxyHostId = createdProxyHostId;

            // Step 3: Save to configuration
            _logger.LogInformation("STEP 3: Saving mapping to configuration file");
            mapping.Id = $"dns_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..24];
            _configService.AddMapping(mapping);
            _logger.LogInformation("Configuration saved with mapping ID: {MappingId}", mapping.Id);

            _logger.LogInformation("Successfully added hostname mapping: {Hostname} -> {TargetIp}:{TargetPort}", 
                hostname, targetIp, targetPort);

            return (true, "Hostname mapping added successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding hostname mapping for {Hostname}", hostname);

            // Attempt rollback
            if (dnsSuccess)
            {
                try
                {
                    await _dnsService.DeleteARecordAsync(hostname);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback DNS record for {Hostname}", hostname);
                }
            }

            if (proxyHostId.HasValue)
            {
                try
                {
                    await _proxyService.DeleteProxyHostAsync(proxyHostId.Value);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback proxy host for {Hostname}", hostname);
                }
            }

            return (false, $"Error adding hostname mapping: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> RemoveHostnameMappingAsync(string mappingId)
    {
        _logger.LogInformation("Removing hostname mapping: {MappingId}", mappingId);

        var mapping = _configService.GetMapping(mappingId);
        if (mapping == null)
        {
            return (false, "Mapping not found");
        }

        return await RemoveHostnameMappingByHostnameAsync(mapping.Hostname);
    }

    public async Task<(bool Success, string Message)> RemoveHostnameMappingByHostnameAsync(string hostname)
    {
        _logger.LogInformation("Removing hostname mapping by hostname: {Hostname}", hostname);

        var mapping = _configService.GetMappingByHostname(hostname);
        if (mapping == null)
        {
            return (false, "Mapping not found");
        }

        bool dnsDeleted = false;
        bool proxyDeleted = false;
        var errors = new List<string>();

        try
        {
            // Step 1: Delete DNS A record
            _logger.LogDebug("Deleting DNS A record for {Hostname}", hostname);
            dnsDeleted = await _dnsService.DeleteARecordAsync(hostname);
            if (!dnsDeleted)
            {
                errors.Add("Failed to delete DNS record");
            }

            // Step 2: Find and delete proxy host
            _logger.LogDebug("Finding and deleting proxy host for {Hostname}", hostname);
            var proxyHosts = await _proxyService.GetProxyHostsAsync();
            var targetProxyHost = proxyHosts.FirstOrDefault(p => 
                p.DomainNames.Any(d => d.Equals(hostname, StringComparison.OrdinalIgnoreCase)));

            if (targetProxyHost != null)
            {
                proxyDeleted = await _proxyService.DeleteProxyHostAsync(targetProxyHost.Id);
                if (!proxyDeleted)
                {
                    errors.Add("Failed to delete proxy host");
                }
            }
            else
            {
                _logger.LogWarning("Proxy host not found for {Hostname}", hostname);
                proxyDeleted = true; // Consider it successful if not found
            }

            // Step 3: Remove from configuration
            _configService.RemoveMappingByHostname(hostname);

            if (errors.Any())
            {
                _logger.LogWarning("Hostname mapping removed with warnings for {Hostname}: {Errors}", 
                    hostname, string.Join(", ", errors));
                return (true, $"Mapping removed with warnings: {string.Join(", ", errors)}");
            }

            _logger.LogInformation("Successfully removed hostname mapping: {Hostname}", hostname);
            return (true, "Hostname mapping removed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing hostname mapping for {Hostname}", hostname);
            return (false, $"Error removing hostname mapping: {ex.Message}");
        }
    }

    public Task<List<HostnameMapping>> GetAllMappingsAsync()
    {
        try
        {
            return Task.FromResult(_configService.GetMappings());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hostname mappings");
            return Task.FromResult(new List<HostnameMapping>());
        }
    }

    public async Task<(bool DnsHealthy, bool ProxyHealthy, bool DockerHealthy)> GetServiceHealthAsync()
    {
        try
        {
            var dnsHealthTask = _dnsService.IsHealthyAsync();
            var proxyHealthTask = _proxyService.IsHealthyAsync();

            await Task.WhenAll(dnsHealthTask, proxyHealthTask);

            return (
                DnsHealthy: await dnsHealthTask,
                ProxyHealthy: await proxyHealthTask,
                DockerHealthy: true // Will be updated when we add Docker health check
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service health");
            return (false, false, false);
        }
    }
}