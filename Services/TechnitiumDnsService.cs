using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeedDial.Services;

public class TechnitiumDnsService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TechnitiumDnsService> _logger;
    private string _token = string.Empty;

    public TechnitiumDnsService(HttpClient httpClient, IConfiguration configuration, ILogger<TechnitiumDnsService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private string BaseUrl => _configuration["SpeedDial:TechnitiumDns:BaseUrl"] ?? "http://localhost:5380";

    public async Task<bool> LoginAsync()
    {
        try
        {
            // Try using configured API token first
            var configToken = _configuration["SpeedDial:TechnitiumDns:ApiToken"];
            if (!string.IsNullOrEmpty(configToken))
            {
                _token = configToken;
                return await TestTokenAsync();
            }

            // Fall back to username/password login
            var url = $"{BaseUrl}/api/user/login?user=admin&pass=admin123";
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("DNS Login Response Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("DNS Login Response Body: {Response}", json);
            
            var result = JsonSerializer.Deserialize<LoginResponse>(json);
            _logger.LogInformation("Parsed login result - Status: {Status}, HasToken: {HasToken}", 
                result?.Status, !string.IsNullOrEmpty(result?.Token));
            
            if (result?.Status == "ok" && !string.IsNullOrEmpty(result.Token))
            {
                _token = result.Token;
                _logger.LogInformation("Successfully authenticated with Technitium DNS");
                return true;
            }
            
            _logger.LogError("DNS login failed: {Message}", result?.ErrorMessage ?? "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with Technitium DNS");
            return false;
        }
    }

    private async Task<bool> TestTokenAsync()
    {
        try
        {
            var url = $"{BaseUrl}/api/zones/list?token={_token}";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddARecordAsync(string hostname, string targetIpAddress)
    {
        try
        {
            if (string.IsNullOrEmpty(_token) && !await LoginAsync())
            {
                return false;
            }

            // First, ensure the zone exists
            var zone = GetZoneFromHostname(hostname);
            var zoneCreated = await EnsureZoneExistsAsync(zone);
            if (!zoneCreated)
            {
                _logger.LogError("Failed to create or verify zone: {Zone}", zone);
                return false;
            }

            var proxyServerIP = _configuration["SpeedDial:ProxyServerIP"] ?? "127.0.0.1";
            var url = $"{BaseUrl}/api/zones/records/add?token={_token}&domain={hostname}&type=A&ipAddress={proxyServerIP}&ttl=3600&overwrite=true";
            
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Add A Record Response: {Response}", json);
            
            var result = JsonSerializer.Deserialize<ApiResponse>(json);
            
            if (result?.Status == "ok")
            {
                _logger.LogInformation("Successfully added A record: {Hostname} -> {IP}", hostname, proxyServerIP);
                return true;
            }
            
            _logger.LogError("Failed to add A record for {Hostname}: {Message}", hostname, result?.ErrorMessage ?? "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding A record for {Hostname}", hostname);
            return false;
        }
    }

    public async Task<bool> DeleteARecordAsync(string hostname)
    {
        try
        {
            if (string.IsNullOrEmpty(_token) && !await LoginAsync())
            {
                return false;
            }

            var proxyServerIP = _configuration["SpeedDial:ProxyServerIP"] ?? "127.0.0.1";
            var url = $"{BaseUrl}/api/zones/records/delete?token={_token}&domain={hostname}&type=A&ipAddress={proxyServerIP}";
            
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Delete A Record Response: {Response}", json);
            
            var result = JsonSerializer.Deserialize<ApiResponse>(json);
            
            if (result?.Status == "ok")
            {
                _logger.LogInformation("Successfully deleted A record: {Hostname}", hostname);
                return true;
            }
            
            _logger.LogError("Failed to delete A record for {Hostname}: {Message}", hostname, result?.ErrorMessage ?? "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting A record for {Hostname}", hostname);
            return false;
        }
    }

    public async Task<List<DnsRecord>> GetARecordsAsync(string? zone = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_token) && !await LoginAsync())
            {
                return new List<DnsRecord>();
            }

            var url = string.IsNullOrEmpty(zone) 
                ? $"{BaseUrl}/api/zones/records/get?token={_token}&type=A" 
                : $"{BaseUrl}/api/zones/records/get?token={_token}&zone={zone}&type=A";
            
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            
            var result = JsonSerializer.Deserialize<GetRecordsResponse>(json);
            
            if (result?.Status == "ok" && result.Response?.Records != null)
            {
                return result.Response.Records.Where(r => r.Type == "A").ToList();
            }
            
            return new List<DnsRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting A records");
            return new List<DnsRecord>();
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/version");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string GetZoneFromHostname(string hostname)
    {
        // For local hostnames, create zone based on the TLD
        // e.g., "shopboss.local" -> "local"
        var parts = hostname.Split('.');
        if (parts.Length >= 2)
        {
            return string.Join(".", parts.Skip(parts.Length - 2));
        }
        return hostname;
    }

    private async Task<bool> EnsureZoneExistsAsync(string zone)
    {
        try
        {
            // Check if zone exists
            var listUrl = $"{BaseUrl}/api/zones/list?token={_token}";
            var listResponse = await _httpClient.GetAsync(listUrl);
            var listJson = await listResponse.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Zone list response: {Response}", listJson);
            
            var listResult = JsonSerializer.Deserialize<ZoneListResponse>(listJson);
            if (listResult?.Status == "ok" && listResult.Response?.Zones != null)
            {
                var existingZone = listResult.Response.Zones.FirstOrDefault(z => 
                    z.Name.Equals(zone, StringComparison.OrdinalIgnoreCase));
                
                if (existingZone != null)
                {
                    _logger.LogDebug("Zone {Zone} already exists", zone);
                    return true;
                }
            }

            // Create zone if it doesn't exist
            _logger.LogInformation("Creating zone: {Zone}", zone);
            var createUrl = $"{BaseUrl}/api/zones/create?token={_token}&zone={zone}&type=Primary";
            var createResponse = await _httpClient.GetAsync(createUrl);
            var createJson = await createResponse.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Zone creation response: {Response}", createJson);
            
            var createResult = JsonSerializer.Deserialize<ApiResponse>(createJson);
            
            if (createResult?.Status == "ok")
            {
                _logger.LogInformation("Successfully created zone: {Zone}", zone);
                return true;
            }
            
            _logger.LogError("Failed to create zone {Zone}: {Message}", zone, createResult?.ErrorMessage ?? "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring zone exists: {Zone}", zone);
            return false;
        }
    }
}

// Response DTOs
public class LoginResponse
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
    
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class ApiResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class GetRecordsResponse
{
    [JsonPropertyName("response")]
    public GetRecordsResponseData? Response { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class GetRecordsResponseData
{
    [JsonPropertyName("records")]
    public List<DnsRecord> Records { get; set; } = new();
}

public class DnsRecord
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("ttl")]
    public int Ttl { get; set; }
    
    [JsonPropertyName("rData")]
    public DnsRecordData? RData { get; set; }
}

public class DnsRecordData
{
    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }
}

public class ZoneListResponse
{
    [JsonPropertyName("response")]
    public ZoneListResponseData? Response { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class ZoneListResponseData
{
    [JsonPropertyName("zones")]
    public List<DnsZone> Zones { get; set; } = new();
}

public class DnsZone
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}