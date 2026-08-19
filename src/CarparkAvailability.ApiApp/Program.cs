using CarparkAvailability.ApiApp.Models;
using CarparkAvailability.ApiApp.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<CarparkDataService>();
builder.Services.AddHostedService(static serviceProvider => serviceProvider.GetRequiredService<CarparkDataService>());
builder.Services.AddHttpClient("DataGovSg", static (serviceProvider, client) =>
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    string baseUrl = configuration["DataGovSg:BaseUrl"] ?? "https://api.data.gov.sg/v1/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);

    string? apiKey = configuration["DataGovSg__ApiKey"] ?? configuration["DataGovSg:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey) && !apiKey.Contains("{{", StringComparison.Ordinal))
    {
        client.DefaultRequestHeaders.Add("api-key", apiKey);
    }
});

WebApplication app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api", (CarparkDataService dataService) =>
{
    DateTime utcNow = DateTime.UtcNow;
    bool stale = CarparkSearchLogic.IsStale(dataService.LastSuccessfulPoll, utcNow);
    bool hasStaticData = dataService.RecordsLoaded > 0;

    string status = dataService.IsAvailable && hasStaticData && !stale
        ? "operational"
        : hasStaticData
            ? "degraded"
            : "unavailable";

    string message = status switch
    {
        "operational" => "Live availability and HDB static data are available.",
        "degraded" => "Serving cached or partial data. Live availability may be stale.",
        _ => "Static HDB data is unavailable."
    };

    return Results.Ok(new StatusResponse
    {
        Status = status,
        LastDataUpdate = dataService.LastSuccessfulPoll,
        RecordsLoaded = dataService.RecordsLoaded,
        Message = message
    });
});

app.MapPost("/api/search", (SearchRequest request, CarparkDataService dataService) =>
{
    Dictionary<string, string[]>? errors = ValidateCoordinates(request.Lat, request.Lng, request.MaxDistanceMetres);
    if (errors is not null)
    {
        return Results.ValidationProblem(errors);
    }

    List<SearchResult> results = CarparkSearchLogic.Search(dataService.HdbRecords, dataService.LiveData, request, DateTime.UtcNow).ToList();
    return Results.Ok(new SearchResponse
    {
        Results = results,
        LastUpdateTime = dataService.LastSuccessfulPoll,
        TotalFound = results.Count
    });
});

app.MapGet("/api/carpark/{carParkNo}", (string carParkNo, CarparkDataService dataService) =>
{
    if (!dataService.HdbRecords.TryGetValue(carParkNo, out HdbCarPark? record))
    {
        return Results.NotFound();
    }

    CarparkDetailResponse detail = CarparkSearchLogic.BuildDetailResponse(record, dataService.LiveData, DateTime.UtcNow);
    return Results.Ok(detail);
});

app.MapPost("/api/filter", (FilterRequest request, CarparkDataService dataService) =>
{
    Dictionary<string, string[]>? errors = ValidateCoordinates(request.Lat, request.Lng, request.MaxDistanceMetres);
    if (errors is not null)
    {
        return Results.ValidationProblem(errors);
    }

    List<SearchResult> results = CarparkSearchLogic.Filter(dataService.HdbRecords, dataService.LiveData, request, DateTime.UtcNow).ToList();
    return Results.Ok(new FilterResponse
    {
        Results = results,
        LastUpdateTime = dataService.LastSuccessfulPoll,
        TotalFound = results.Count
    });
});

app.MapGet("/api/data-status", (CarparkDataService dataService) => Results.Ok(new DataStatusResponse
{
    IsAvailable = dataService.IsAvailable && dataService.RecordsLoaded > 0,
    LastSuccessfulPoll = dataService.LastSuccessfulPoll,
    LastAttemptedPoll = dataService.LastAttemptedPoll,
    RecordsLoaded = dataService.RecordsLoaded,
    IsStale = !dataService.IsAvailable || CarparkSearchLogic.IsStale(dataService.LastSuccessfulPoll, DateTime.UtcNow)
}));

app.MapDefaultEndpoints();

app.Run();
return;

static Dictionary<string, string[]>? ValidateCoordinates(double lat, double lng, double maxDistanceMetres)
{
    Dictionary<string, string[]> errors = new();

    if (lat is < 1.2 or > 1.5)
    {
        errors["lat"] = ["Latitude must be between 1.2 and 1.5."];
    }

    if (lng is < 103.6 or > 104.0)
    {
        errors["lng"] = ["Longitude must be between 103.6 and 104.0."];
    }

    if (maxDistanceMetres <= 0 || maxDistanceMetres > 500)
    {
        errors["maxDistanceMetres"] = ["Maximum distance must be greater than 0 and not exceed 500 metres."];
    }

    return errors.Count > 0 ? errors : null;
}
