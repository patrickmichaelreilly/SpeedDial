namespace SpeedDial.Models;

public class HostnameMapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Hostname { get; set; } = string.Empty;
    public string TargetIP { get; set; } = string.Empty;
    public int TargetPort { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class HostnameMappingConfig
{
    public List<HostnameMapping> Mappings { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}