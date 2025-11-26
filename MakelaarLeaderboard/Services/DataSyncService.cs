using System.Linq;
using System.Text.Json;
using MakelaarLeaderboard.Data;
using MakelaarLeaderboard.Models;
using Microsoft.EntityFrameworkCore;

namespace MakelaarLeaderboard.Services;

public class DataSyncService : BackgroundService
{
    private readonly string _apiUri;
    private readonly ILogger<DataSyncService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(1);
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DataSyncService(
        IServiceProvider serviceProvider,
        ILogger<DataSyncService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _apiUri = configuration["DataSync:ApiUri"] ?? throw new Exception("DataSync:ApiUri not configured");
        _apiUri += "/?type=koop&zo=/amsterdam/balkon/dakterras/tuin/&page=2&pagesize=25";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataSyncService starting...");

        // Initial database setup and data fetch
        await InitializeDatabaseAsync(stoppingToken);

        // Periodic sync every 1 minute
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
                await SyncDataAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("DataSyncService is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data sync");
            }
        }
    }

    private async Task InitializeDatabaseAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MakelaarLeaderboardContext>();

        try
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync(cancellationToken);

            // Clear all existing data on startup
            _logger.LogInformation("Clearing existing data from database...");
            context.Houses.RemoveRange(context.Houses);
            context.Makelaars.RemoveRange(context.Makelaars);
            await context.SaveChangesAsync(cancellationToken);

            // Fetch fresh data
            await SyncDataAsync(cancellationToken);

            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }

    private async Task SyncDataAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MakelaarLeaderboardContext>();

        try
        {
            _logger.LogInformation($"Fetching data from API: {_apiUri}");

            var data = await FetchDataFromApiAsync(cancellationToken);

            _logger.LogInformation($"data: {data}");

            if (data == null)
            {
                _logger.LogWarning("No data received from API");
                return;
            }

            await UpdateDatabaseAsync(context, data, cancellationToken);

            _logger.LogInformation("Data sync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data");
        }
    }

    private async Task<ApiResponse?> FetchDataFromApiAsync(CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
            
            try
            {
                var response = await httpClient.GetAsync(_apiUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var fundaResponse = JsonSerializer.Deserialize<FundaApiResponse>(content, _jsonSerializerOptions);

                if (fundaResponse?.Objects == null)
                {
                    return null;
                }

                // Transform Funda API response to our domain model
                var houses = new List<House>();
                var makelaarsDict = new Dictionary<int, Makelaar>();

                foreach (var obj in fundaResponse.Objects)
                {
                    // Extract Makelaar (unique by MakelaarId)
                    if (!makelaarsDict.ContainsKey(obj.MakelaarId))
                    {
                        makelaarsDict[obj.MakelaarId] = new Makelaar
                        {
                            MakelaarId = obj.MakelaarId,
                            MakelaarNaam = obj.MakelaarNaam
                        };
                    }

                    // Determine if the house has a garden (Tuin)
                    bool hasTuin = obj.Perceeloppervlakte > 0;

                    // Create House entity
                    houses.Add(new House
                    {
                        Id = obj.Id,
                        MakelaarId = obj.MakelaarId,
                        Woonplaats = obj.Woonplaats,
                        HasTuin = hasTuin
                    });
                }

                return new ApiResponse
                {
                    Makelaars = makelaarsDict.Values.ToList(),
                    Houses = houses
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error fetching data from API");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error");
                return null;
            }
    }

    private async Task UpdateDatabaseAsync(MakelaarLeaderboardContext context, ApiResponse data, CancellationToken cancellationToken)
    {
        // Get existing data for comparison
        var existingMakelaarIds = await context.Makelaars.Select(m => m.MakelaarId).ToListAsync(cancellationToken);
        var existingHouseIds = await context.Houses.Select(h => h.Id).ToListAsync(cancellationToken);

        // Update or add Makelaars
        if (data.Makelaars != null)
        {
            foreach (var makelaar in data.Makelaars)
            {
                var existing = await context.Makelaars.FindAsync([makelaar.MakelaarId], cancellationToken);

                if (existing != null)
                {
                    // Update existing
                    existing.MakelaarNaam = makelaar.MakelaarNaam;
                    context.Makelaars.Update(existing);
                }
                else
                {
                    // Add new
                    context.Makelaars.Add(makelaar);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Update or add Houses
        if (data.Houses != null)
        {
            foreach (var house in data.Houses)
            {
                var existing = await context.Houses.FindAsync([house.Id!], cancellationToken);

                if (existing != null)
                {
                    // Update existing
                    existing.MakelaarId = house.MakelaarId;
                    existing.Woonplaats = house.Woonplaats;
                    existing.HasTuin = house.HasTuin;
                    context.Houses.Update(existing);
                }
                else
                {
                    // Add new
                    context.Houses.Add(house);
                }
            }
        }

        // Remove deleted items (optional - items not in the API response)
        var newMakelaarIds = data.Makelaars?.Select(m => m.MakelaarId).ToList() ?? [];
        var makelaarsToRemove = existingMakelaarIds.Except(newMakelaarIds).ToList();

        if (makelaarsToRemove.Count != 0)
        {
            var toRemove = context.Makelaars.Where(m => makelaarsToRemove.Contains(m.MakelaarId));
            context.Makelaars.RemoveRange(toRemove);
        }

        var newHouseIds = data.Houses?.Select(h => h.Id).ToList() ?? [];
        var housesToRemove = existingHouseIds.Except(newHouseIds).ToList();

        if (housesToRemove.Count != 0)
        {
            var toRemove = context.Houses.Where(h => housesToRemove.Contains(h.Id));
            context.Houses.RemoveRange(toRemove);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    // Helper class to deserialize API response
    private class ApiResponse
    {
        public List<Makelaar>? Makelaars { get; init; }
        public List<House>? Houses { get; init; }
    }

    private class FundaApiResponse
    {
        public List<FundaObject>? Objects { get; set; }
    }

    private class FundaObject
    {
        public string? Id { get; set; }
        public int MakelaarId { get; set; }
        public string? MakelaarNaam { get; set; }
        public string? Woonplaats { get; set; }
        public int? Perceeloppervlakte { get; set; } // Plot area - used to determine if has garden
    }
}