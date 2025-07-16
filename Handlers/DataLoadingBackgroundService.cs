using CatalogExpertBot.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CatalogExpertBot.Handlers;

public class DataLoadingBackgroundService : BackgroundService
{
    private readonly IDataLoaderService _dataLoader;
    private readonly ILogger<DataLoadingBackgroundService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromHours(1); // Refresh every hour

    public DataLoadingBackgroundService(
        IDataLoaderService dataLoader, 
        ILogger<DataLoadingBackgroundService> logger)
    {
        _dataLoader = dataLoader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data loading background service started");

        // Initial load
        try
        {
            _logger.LogInformation("Starting initial catalog data load");
            await _dataLoader.LoadAllDataAsync();
            _logger.LogInformation("Initial catalog data load completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial data load");
        }

        // Periodic refresh
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_refreshInterval, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Starting scheduled catalog data refresh");
                    await _dataLoader.LoadAllDataAsync();
                    _logger.LogInformation("Scheduled catalog data refresh completed");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Data loading background service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled data refresh");
            }
        }

        _logger.LogInformation("Data loading background service stopped");
    }
}
