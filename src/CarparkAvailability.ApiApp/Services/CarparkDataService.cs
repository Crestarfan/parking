using System.Globalization;
using System.Net.Http.Json;
using CarparkAvailability.ApiApp.Models;
using CsvHelper;

namespace CarparkAvailability.ApiApp.Services;

public sealed class CarparkDataService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<CarparkDataService> _logger;

    private IReadOnlyDictionary<string, HdbCarPark> _hdbRecords = new Dictionary<string, HdbCarPark>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, CarparkInfo[]> _liveData = new Dictionary<string, CarparkInfo[]>(StringComparer.OrdinalIgnoreCase);
    private bool _isAvailable;
    private long _lastSuccessfulPollTicks;
    private long _lastAttemptedPollTicks;

    public CarparkDataService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CarparkDataService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, HdbCarPark> HdbRecords => Volatile.Read(ref _hdbRecords);
    public IReadOnlyDictionary<string, CarparkInfo[]> LiveData => Volatile.Read(ref _liveData);
    public DateTime? LastSuccessfulPoll => ReadTimestamp(ref _lastSuccessfulPollTicks);
    public DateTime? LastAttemptedPoll => ReadTimestamp(ref _lastAttemptedPollTicks);
    public bool IsAvailable => Volatile.Read(ref _isAvailable);
    public int RecordsLoaded => Volatile.Read(ref _hdbRecords).Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LoadHdbRecords();
        await PollDataAsync(stoppingToken);

        using PeriodicTimer timer = new(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollDataAsync(stoppingToken);
        }
    }

    private void LoadHdbRecords()
    {
        string csvPath = _configuration["StaticData:CsvPath"] ?? "Data/HDBCarparkInformation.csv";
        string fullPath = Path.Combine(_hostEnvironment.ContentRootPath, csvPath);

        if (!File.Exists(fullPath))
        {
            _logger.LogError("HDB CSV data file was not found at {CsvPath}", fullPath);
            Volatile.Write(ref _hdbRecords, new Dictionary<string, HdbCarPark>(StringComparer.OrdinalIgnoreCase));
            return;
        }

        try
        {
            using StreamReader reader = new(fullPath);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
            List<HdbCarPark> records = csv.GetRecords<HdbCarPark>().ToList();

            foreach (HdbCarPark record in records)
            {
                (record.Lat, record.Lng) = Svy21Converter.Convert(record.XCoord, record.YCoord);
            }

            IReadOnlyDictionary<string, HdbCarPark> loadedRecords = records.ToDictionary(record => record.CarParkNo, StringComparer.OrdinalIgnoreCase);
            Volatile.Write(ref _hdbRecords, loadedRecords);
            _logger.LogInformation("Loaded {Count} HDB car park records from {CsvPath}", loadedRecords.Count, fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load HDB CSV data from {CsvPath}", fullPath);
            Volatile.Write(ref _hdbRecords, new Dictionary<string, HdbCarPark>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private async Task PollDataAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _lastAttemptedPollTicks, DateTime.UtcNow.Ticks);
        Exception? lastError = null;

        for (int attempt = 0; attempt < RetryDelays.Length; attempt++)
        {
            try
            {
                IReadOnlyDictionary<string, CarparkInfo[]> latest = await FetchLiveDataAsync(cancellationToken);
                Volatile.Write(ref _liveData, latest);
                Interlocked.Exchange(ref _lastSuccessfulPollTicks, DateTime.UtcNow.Ticks);
                Volatile.Write(ref _isAvailable, true);
                _logger.LogInformation("Loaded live availability for {Count} car parks", latest.Count);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Live data polling attempt {Attempt} failed", attempt + 1);
                if (attempt < RetryDelays.Length - 1)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
        }

        Volatile.Write(ref _isAvailable, false);
        _logger.LogError(lastError, "Failed to refresh live data after {Attempts} attempts. Serving cached data.", RetryDelays.Length);
    }

    private static DateTime? ReadTimestamp(ref long ticks)
        => Interlocked.Read(ref ticks) is long value && value > 0 ? new DateTime(value, DateTimeKind.Utc) : null;

    private async Task<IReadOnlyDictionary<string, CarparkInfo[]>> FetchLiveDataAsync(CancellationToken cancellationToken)
    {
        HttpClient client = _httpClientFactory.CreateClient("DataGovSg");
        using HttpResponseMessage response = await client.GetAsync("transport/carpark-availability", cancellationToken);
        response.EnsureSuccessStatusCode();

        CarparkAvailabilityResponse? payload = await response.Content.ReadFromJsonAsync<CarparkAvailabilityResponse>(cancellationToken: cancellationToken);
        if (payload?.Items is null || payload.Items.Count == 0)
        {
            return new Dictionary<string, CarparkInfo[]>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, CarparkInfo[]> liveData = payload.Items
            .SelectMany(item => item.CarparkData)
            .Where(item => !string.IsNullOrWhiteSpace(item.CarparkNumber))
            .GroupBy(item => item.CarparkNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(item => item.CarparkInfo.Select(info => new CarparkInfo
                    {
                        LotType = info.LotType,
                        LotsAvailable = info.LotsAvailable,
                        TotalLots = info.TotalLots,
                        UpdateDateTime = item.UpdateDateTime
                    }))
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return liveData;
    }
}
