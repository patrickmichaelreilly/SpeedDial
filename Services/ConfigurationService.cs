using System.Text.Json;
using SpeedDial.Models;

namespace SpeedDial.Services;

public class ConfigurationService
{
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private HostnameMappingConfig _config;

    public ConfigurationService()
    {
        // In production this will be C:\SpeedDial\mappings.json
        // In development/WSL this will be in the current directory
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _config = LoadConfiguration();
    }

    public List<HostnameMapping> GetMappings()
    {
        return _config.Mappings.Where(m => m.IsActive).ToList();
    }

    public HostnameMapping? GetMapping(string id)
    {
        return _config.Mappings.FirstOrDefault(m => m.Id == id && m.IsActive);
    }

    public HostnameMapping? GetMappingByHostname(string hostname)
    {
        return _config.Mappings.FirstOrDefault(m => 
            m.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase) && m.IsActive);
    }

    public void AddMapping(HostnameMapping mapping)
    {
        // Remove any existing mapping with the same hostname
        var existingMapping = GetMappingByHostname(mapping.Hostname);
        if (existingMapping != null)
        {
            existingMapping.IsActive = false;
        }

        _config.Mappings.Add(mapping);
        _config.LastUpdated = DateTime.UtcNow;
        SaveConfiguration();
    }

    public bool RemoveMapping(string id)
    {
        var mapping = GetMapping(id);
        if (mapping != null)
        {
            mapping.IsActive = false;
            _config.LastUpdated = DateTime.UtcNow;
            SaveConfiguration();
            return true;
        }
        return false;
    }

    public bool RemoveMappingByHostname(string hostname)
    {
        var mapping = GetMappingByHostname(hostname);
        if (mapping != null)
        {
            mapping.IsActive = false;
            _config.LastUpdated = DateTime.UtcNow;
            SaveConfiguration();
            return true;
        }
        return false;
    }

    private HostnameMappingConfig LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<HostnameMappingConfig>(json, _jsonOptions);
                return config ?? new HostnameMappingConfig();
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with empty config
            Console.WriteLine($"Error loading configuration: {ex.Message}");
        }

        return new HostnameMappingConfig();
    }

    private void SaveConfiguration()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config, _jsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error saving configuration: {ex.Message}");
            throw;
        }
    }

    public string GetConfigFilePath() => _configFilePath;
}