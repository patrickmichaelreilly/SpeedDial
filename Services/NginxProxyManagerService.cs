using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeedDial.Services;

public class NginxProxyManagerService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NginxProxyManagerService> _logger;
    private string _bearerToken = string.Empty;

    public NginxProxyManagerService(HttpClient httpClient, IConfiguration configuration, ILogger<NginxProxyManagerService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private string BaseUrl => _configuration["SpeedDial:NginxProxyManager:BaseUrl"] ?? "http://localhost:81";

    public async Task<bool> LoginAsync()
    {
        try
        {
            // Try using configured token first
            var configToken = _configuration["SpeedDial:NginxProxyManager:Token"];
            if (!string.IsNullOrEmpty(configToken))
            {
                _bearerToken = configToken;
                return await TestTokenAsync();
            }

            // Fall back to email/password login
            var email = _configuration["SpeedDial:NginxProxyManager:Email"];
            var password = _configuration["SpeedDial:NginxProxyManager:Password"];
            
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                // Use default credentials
                email = "admin@example.com";
                password = "changeme";
            }

            var loginRequest = new
            {
                identity = email,
                secret = password
            };

            var json = JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/api/tokens", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("NPM Login Response: {Response}", responseJson);

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseJson);
                if (tokenResponse?.Token != null)
                {
                    _bearerToken = tokenResponse.Token;
                    _logger.LogInformation("Successfully authenticated with Nginx Proxy Manager");
                    return true;
                }
            }
            
            _logger.LogError("NPM login failed: {StatusCode} - {Response}", response.StatusCode, responseJson);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with Nginx Proxy Manager");
            return false;
        }
    }

    private async Task<bool> TestTokenAsync()
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
            
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/nginx/proxy-hosts");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, int? ProxyHostId)> CreateProxyHostAsync(string hostname, string targetIp, int targetPort)
    {
        try
        {
            if (string.IsNullOrEmpty(_bearerToken) && !await LoginAsync())
            {
                return (false, null);
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);

            var proxyHostRequest = new
            {
                domain_names = new[] { hostname },
                forward_scheme = "http",
                forward_host = targetIp,
                forward_port = targetPort,
                access_list_id = "0",
                certificate_id = "0",
                ssl_forced = false,
                caching_enabled = false,
                block_exploits = false,
                advanced_config = "",
                meta = new
                {
                    letsencrypt_agree = false,
                    dns_challenge = false
                },
                allow_websocket_upgrade = false,
                http2_support = false,
                hsts_enabled = false,
                hsts_subdomains = false
            };

            var json = JsonSerializer.Serialize(proxyHostRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/api/nginx/proxy-hosts", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Create Proxy Host Response: {Response}", responseJson);

            if (response.IsSuccessStatusCode)
            {
                var proxyHostResponse = JsonSerializer.Deserialize<ProxyHostResponse>(responseJson);
                _logger.LogInformation("Successfully created proxy host: {Hostname} -> {Target}:{Port}", 
                    hostname, targetIp, targetPort);
                return (true, proxyHostResponse?.Id);
            }
            
            _logger.LogError("Failed to create proxy host for {Hostname}: {StatusCode} - {Response}", 
                hostname, response.StatusCode, responseJson);
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating proxy host for {Hostname}", hostname);
            return (false, null);
        }
    }

    public async Task<bool> DeleteProxyHostAsync(int proxyHostId)
    {
        try
        {
            if (string.IsNullOrEmpty(_bearerToken) && !await LoginAsync())
            {
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);

            var response = await _httpClient.DeleteAsync($"{BaseUrl}/api/nginx/proxy-hosts/{proxyHostId}");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted proxy host ID: {ProxyHostId}", proxyHostId);
                return true;
            }
            
            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to delete proxy host {ProxyHostId}: {StatusCode} - {Response}", 
                proxyHostId, response.StatusCode, responseJson);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting proxy host {ProxyHostId}", proxyHostId);
            return false;
        }
    }

    public async Task<List<ProxyHost>> GetProxyHostsAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_bearerToken) && !await LoginAsync())
            {
                return new List<ProxyHost>();
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);

            var response = await _httpClient.GetAsync($"{BaseUrl}/api/nginx/proxy-hosts");
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var proxyHosts = JsonSerializer.Deserialize<List<ProxyHost>>(responseJson);
                return proxyHosts ?? new List<ProxyHost>();
            }
            
            return new List<ProxyHost>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting proxy hosts");
            return new List<ProxyHost>();
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/schema");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// DTOs
public class TokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
    
    [JsonPropertyName("expires")]
    public string? Expires { get; set; }
}

public class ProxyHostResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("domain_names")]
    public List<string> DomainNames { get; set; } = new();
}

public class ProxyHost
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("domain_names")]
    public List<string> DomainNames { get; set; } = new();
    
    [JsonPropertyName("forward_host")]
    public string ForwardHost { get; set; } = string.Empty;
    
    [JsonPropertyName("forward_port")]
    public int ForwardPort { get; set; }
    
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}