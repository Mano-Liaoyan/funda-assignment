using System.Linq;
using System.Text;
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
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
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
    }

    private async Task<string> PageUrlBuilder(string searchType = "koop", string city = "amsterdam", string[]? buitenruimtes = null, int page = 1, int pageSize = 25)
    {
        var sb = new StringBuilder(_apiUri);

        await Task.Run(() =>
        {
            sb.Append($"?type={searchType}&zo=/{city}/");

            if (buitenruimtes != null)
            {
                foreach (string buitenruimte in buitenruimtes)
                    sb.Append($"{buitenruimte}/");
            }

            sb.Append($"&page={page}&pagesize={pageSize}");
        });

        _logger.LogInformation($"API Constructed:\n{sb}");

        return sb.ToString();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataSyncService starting...");

        await InitializeDatabaseAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
                await SyncDataAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Data Sync Service is Stopping...");
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
        using IServiceScope scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MakelaarLeaderboardContext>();

        try
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);

            _logger.LogInformation("Clearing existing data from database...");
            context.Houses.RemoveRange(context.Houses);
            context.Makelaars.RemoveRange(context.Makelaars);
            await context.SaveChangesAsync(cancellationToken);

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
        using IServiceScope scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MakelaarLeaderboardContext>();

        try
        {
            ApiResponse? data = await FetchDataFromApiAsync(cancellationToken);
            ApiResponse? dataHasTuin = await FetchDataFromApiAsync(cancellationToken, ["tuin"]);

            _logger.LogInformation($"data received:\n{data}");

            if (data == null || dataHasTuin == null)
            {
                _logger.LogWarning("No data received from API");
                return;
            }

            await UpdateDatabaseAsync(context, data, cancellationToken);
            await UpdateDatabaseAsync(context, dataHasTuin, cancellationToken);

            _logger.LogInformation("Data sync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data");
        }
    }

    private async Task<ApiResponse?> FetchDataFromApiAsync(CancellationToken cancellationToken, string[]? buitenruimtes = null)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        var allHouses = new List<House>();
        var makelaarsDict = new Dictionary<int, Makelaar>();

        try
        {
            int currentPage = 1;
            int totalPages = 1;

            do
            {
                // Build URL with page parameter
                string pageUrl = await PageUrlBuilder(buitenruimtes: buitenruimtes, page: currentPage);
                _logger.LogInformation($"pageUrl:\n{pageUrl}");

                _logger.LogInformation($"Fetching page {currentPage} from API");

                HttpResponseMessage response = await httpClient.GetAsync(pageUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync(cancellationToken);

                var fundaResponse = JsonSerializer.Deserialize<FundaApiResponse>(content, _jsonSerializerOptions);

                if (fundaResponse?.Objects == null || fundaResponse.Objects.Count == 0)
                {
                    _logger.LogInformation($"No more objects found on page {currentPage}");
                    break;
                }

                if (fundaResponse.Paging != null)
                {
                    totalPages = fundaResponse.Paging.AantalPaginas;
                    _logger.LogInformation($"Total pages available: {totalPages}");
                }

                // Transform current page data
                foreach (FundaObject obj in fundaResponse.Objects)
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

                    bool hasTuin = pageUrl.Contains("tuin", StringComparison.OrdinalIgnoreCase);

                    _logger.LogInformation($"HasTuin {hasTuin}");

                    allHouses.Add(new House
                    {
                        Id = obj.Id,
                        MakelaarId = obj.MakelaarId,
                        Woonplaats = obj.Woonplaats,
                        HasTuin = hasTuin
                    });
                }

                currentPage++;

                // Add a small delay between requests to respect API rate limits
                // The funda API has a constraint of 100 req / 1 minute ==> 100 req / 60000 ms ==> 1 req / 600 ms
                // The maximum pagesize is 25, we have to do at least 213 request which has exceeded the above limitation
                // This means our delay should be at least 600ms, for convenient and some buffer, I use 610 ms here
                if (currentPage <= totalPages)
                {
                    await Task.Delay(610, cancellationToken);
                }

            } while (currentPage <= totalPages && !cancellationToken.IsCancellationRequested);

            _logger.LogInformation($"Fetched total of {allHouses.Count} houses and {makelaarsDict.Count} makelaars");

            return new ApiResponse
            {
                Makelaars = makelaarsDict.Values.ToList(),
                Houses = allHouses
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
        // Get existing data
        var existingMakelaarIds = await context.Makelaars.Select(m => m.MakelaarId).ToListAsync(cancellationToken);
        var existingHouseIds = await context.Houses.Select(h => h.Id).ToListAsync(cancellationToken);

        // Update or add Makelaars
        if (data.Makelaars != null)
        {
            foreach (Makelaar makelaar in data.Makelaars)
            {
                Makelaar? existing = await context.Makelaars.FindAsync([makelaar.MakelaarId], cancellationToken);

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
            foreach (House house in data.Houses)
            {
                House? existing = await context.Houses.FindAsync([house.Id!], cancellationToken);

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
        public List<FundaObject>? Objects { get; init; }
        public FundaPaging? Paging { get; init; }
    }

    private class FundaObject
    {
        public string? Id { get; set; }
        public int MakelaarId { get; set; }
        public string? MakelaarNaam { get; set; }
        public string? Woonplaats { get; set; }
    }

    private class FundaPaging
    {
        public int HuidigePagina { get; set; }
        public int AantalPaginas { get; set; }
    }
}