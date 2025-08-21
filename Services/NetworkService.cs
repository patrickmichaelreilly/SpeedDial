using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SpeedDial.Services;

public interface INetworkService
{
    string GetServerIPAddress();
}

public class NetworkService : INetworkService
{
    private readonly ILogger<NetworkService> _logger;
    private string? _cachedIP;

    public NetworkService(ILogger<NetworkService> logger)
    {
        _logger = logger;
    }

    public string GetServerIPAddress()
    {
        if (!string.IsNullOrEmpty(_cachedIP))
            return _cachedIP;

        var detectedIP = DetectPrimaryNetworkIP();
        _cachedIP = detectedIP;
        _logger.LogInformation("Auto-detected server IP: {IP}", detectedIP);
        return detectedIP;
    }

    private string DetectPrimaryNetworkIP()
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up 
                        && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();

        // Prefer interfaces with a gateway (internet-connected)
        foreach (var networkInterface in networkInterfaces)
        {
            var ipProperties = networkInterface.GetIPProperties();
            var hasGateway = ipProperties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
            
            if (hasGateway)
            {
                var ipv4Address = ipProperties.UnicastAddresses
                    .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork 
                                         && !IPAddress.IsLoopback(ua.Address))?
                    .Address;

                if (ipv4Address != null && IsValidNetworkIP(ipv4Address))
                {
                    _logger.LogDebug("Selected IP {IP} from interface {Interface} (has gateway)", 
                        ipv4Address, networkInterface.Name);
                    return ipv4Address.ToString();
                }
            }
        }

        // Fallback: any valid IPv4 address
        foreach (var networkInterface in networkInterfaces)
        {
            var ipProperties = networkInterface.GetIPProperties();
            var ipv4Address = ipProperties.UnicastAddresses
                .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork 
                                     && !IPAddress.IsLoopback(ua.Address))?
                .Address;

            if (ipv4Address != null && IsValidNetworkIP(ipv4Address))
            {
                _logger.LogDebug("Selected IP {IP} from interface {Interface} (fallback)", 
                    ipv4Address, networkInterface.Name);
                return ipv4Address.ToString();
            }
        }

        throw new InvalidOperationException("No valid network IP address found");
    }

    private static bool IsValidNetworkIP(IPAddress address)
    {
        // Exclude loopback, link-local, and multicast addresses
        var bytes = address.GetAddressBytes();
        
        // Loopback: 127.x.x.x
        if (bytes[0] == 127)
            return false;
            
        // Link-local: 169.254.x.x
        if (bytes[0] == 169 && bytes[1] == 254)
            return false;
            
        // Multicast: 224.x.x.x - 239.x.x.x
        if (bytes[0] >= 224 && bytes[0] <= 239)
            return false;

        return true;
    }
}