using Microsoft.AspNetCore.Mvc;
using SpeedDial.Models;
using SpeedDial.Services;

namespace SpeedDial.Controllers;

public class HomeController : Controller
{
    private readonly ServiceOrchestrator _orchestrator;
    private readonly DockerService _dockerService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ServiceOrchestrator orchestrator, DockerService dockerService, ILogger<HomeController> logger)
    {
        _orchestrator = orchestrator;
        _dockerService = dockerService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var mappings = await _orchestrator.GetAllMappingsAsync();
            var health = await _orchestrator.GetServiceHealthAsync();
            var dockerRunning = await _dockerService.IsDockerRunningAsync();
            var containersRunning = await _dockerService.AreContainersRunningAsync();

            var viewModel = new HomeViewModel
            {
                Mappings = mappings,
                DnsHealthy = health.DnsHealthy,
                ProxyHealthy = health.ProxyHealthy,
                DockerRunning = dockerRunning,
                ContainersRunning = containersRunning
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading home page");
            return View(new HomeViewModel
            {
                ErrorMessage = $"Error loading data: {ex.Message}"
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddMapping(string hostname, string targetIp, int targetPort)
    {
        try
        {
            _logger.LogInformation("AddMapping called with: Hostname='{Hostname}', TargetIP='{TargetIp}', TargetPort={TargetPort}", 
                hostname, targetIp, targetPort);
                
            if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(targetIp) || targetPort <= 0)
            {
                _logger.LogWarning("AddMapping validation failed: empty fields or invalid port");
                TempData["ErrorMessage"] = "All fields are required and port must be greater than 0";
                return RedirectToAction("Index");
            }

            var (success, message) = await _orchestrator.AddHostnameMappingAsync(hostname, targetIp, targetPort);
            
            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding mapping");
            TempData["ErrorMessage"] = $"Error adding mapping: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> RemoveMapping(string mappingId)
    {
        try
        {
            var (success, message) = await _orchestrator.RemoveHostnameMappingAsync(mappingId);
            
            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing mapping");
            TempData["ErrorMessage"] = $"Error removing mapping: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> StartContainers()
    {
        try
        {
            var (success, output) = await _dockerService.StartContainersAsync();
            
            if (success)
            {
                TempData["SuccessMessage"] = "Containers started successfully";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to start containers: {output}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting containers");
            TempData["ErrorMessage"] = $"Error starting containers: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> StopContainers()
    {
        try
        {
            var (success, output) = await _dockerService.StopContainersAsync();
            
            if (success)
            {
                TempData["SuccessMessage"] = "Containers stopped successfully";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to stop containers: {output}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping containers");
            TempData["ErrorMessage"] = $"Error stopping containers: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Status()
    {
        try
        {
            var health = await _orchestrator.GetServiceHealthAsync();
            var dockerRunning = await _dockerService.IsDockerRunningAsync();
            var containersRunning = await _dockerService.AreContainersRunningAsync();
            var containerStatuses = await _dockerService.GetContainerStatusAsync();

            var statusModel = new StatusViewModel
            {
                DnsHealthy = health.DnsHealthy,
                ProxyHealthy = health.ProxyHealthy,
                DockerRunning = dockerRunning,
                ContainersRunning = containersRunning,
                ContainerStatuses = containerStatuses
            };

            return View(statusModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading status page");
            return View(new StatusViewModel
            {
                ErrorMessage = $"Error loading status: {ex.Message}"
            });
        }
    }
}