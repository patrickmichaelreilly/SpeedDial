using SpeedDial.Services;

namespace SpeedDial.Models;

public class HomeViewModel
{
    public List<HostnameMapping> Mappings { get; set; } = new();
    public bool DnsHealthy { get; set; }
    public bool ProxyHealthy { get; set; }
    public bool DockerRunning { get; set; }
    public bool ContainersRunning { get; set; }
    public List<ContainerStatus> ContainerStatuses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

