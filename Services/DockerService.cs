using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace SpeedDial.Services;

public class DockerService
{
    private readonly DockerClient _dockerClient;
    private readonly ILogger<DockerService> _logger;

    public DockerService(ILogger<DockerService> logger)
    {
        _logger = logger;
        
        // For Windows deployment, this will connect to Docker Desktop
        // For WSL development, this connects to Docker daemon
        _dockerClient = new DockerClientConfiguration()
            .CreateClient();
    }

    public async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            await _dockerClient.System.PingAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Docker is not running: {Error}", ex.Message);
            return false;
        }
    }

    public async Task<bool> AreContainersRunningAsync()
    {
        try
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool>
                    {
                        ["technitium-dns-server"] = true,
                        ["nginx-proxy-manager"] = true
                    }
                }
            });

            var technitiumRunning = containers.Any(c => 
                c.Names.Any(n => n.Contains("technitium-dns-server")) && c.State == "running");
            var nginxRunning = containers.Any(c => 
                c.Names.Any(n => n.Contains("nginx-proxy-manager")) && c.State == "running");

            return technitiumRunning && nginxRunning;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking container status: {Error}", ex.Message);
            return false;
        }
    }

    public async Task<(bool Success, string Output)> StartContainersAsync()
    {
        try
        {
            var dockerComposePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docker-compose.yml");
            
            if (!File.Exists(dockerComposePath))
            {
                return (false, "docker-compose.yml not found");
            }

            // Use docker-compose to start containers
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose -f \"{dockerComposePath}\" up -d",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return (false, "Failed to start docker compose process");
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Containers started successfully");
                return (true, output);
            }
            else
            {
                _logger.LogError("Failed to start containers: {Error}", error);
                return (false, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error starting containers: {Error}", ex.Message);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Output)> StopContainersAsync()
    {
        try
        {
            var dockerComposePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docker-compose.yml");
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose -f \"{dockerComposePath}\" down",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return (false, "Failed to start docker compose process");
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Containers stopped successfully");
                return (true, output);
            }
            else
            {
                _logger.LogError("Failed to stop containers: {Error}", error);
                return (false, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error stopping containers: {Error}", ex.Message);
            return (false, ex.Message);
        }
    }

    public async Task<List<ContainerStatus>> GetContainerStatusAsync()
    {
        var statuses = new List<ContainerStatus>();

        try
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool>
                    {
                        ["technitium-dns-server"] = true,
                        ["nginx-proxy-manager"] = true
                    }
                }
            });

            foreach (var container in containers)
            {
                statuses.Add(new ContainerStatus
                {
                    Name = container.Names.First().TrimStart('/'),
                    State = container.State,
                    Status = container.Status,
                    Image = container.Image,
                    Created = new DateTimeOffset(container.Created)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting container status: {Error}", ex.Message);
        }

        return statuses;
    }

    public void Dispose()
    {
        _dockerClient?.Dispose();
    }
}

public class ContainerStatus
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
}