namespace CarparkAvailability.ApiApp.Models;

public sealed class StatusResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime? LastDataUpdate { get; set; }
    public int RecordsLoaded { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SearchRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double MaxDistanceMetres { get; set; } = 500;
}

public sealed class SearchResult
{
    public string CarParkNo { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Distance { get; set; }
    public int AvailableLots { get; set; }
    public int TotalLots { get; set; }
    public double OccupancyRate { get; set; }
    public bool NightParking { get; set; }
    public DateTime? LastUpdateTime { get; set; }
    public bool IsStale { get; set; }
}

public sealed class SearchResponse
{
    public List<SearchResult> Results { get; set; } = [];
    public DateTime? LastUpdateTime { get; set; }
    public int TotalFound { get; set; }
}

public sealed class CarparkDetailResponse
{
    public string CarParkNo { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double XCoord { get; set; }
    public double YCoord { get; set; }
    public string CarParkType { get; set; } = string.Empty;
    public string TypeOfParkingSystem { get; set; } = string.Empty;
    public string ShortTermParking { get; set; } = string.Empty;
    public string FreeParking { get; set; } = string.Empty;
    public string NightParking { get; set; } = string.Empty;
    public int CarParkDecks { get; set; }
    public double GantryHeight { get; set; }
    public string CarParkBasement { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int AvailableLots { get; set; }
    public int TotalLots { get; set; }
    public double OccupancyRate { get; set; }
    public bool IsStale { get; set; }
    public DateTime? LastUpdateTime { get; set; }
}

public sealed class FilterRequest
{
    public bool AvailableOnly { get; set; }
    public bool FreeParking { get; set; }
    public bool NightParking { get; set; }
    public List<string> VehicleTypes { get; set; } = [];
    public double MinGantryHeight { get; set; }
    public List<string> CarParkTypes { get; set; } = [];
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double MaxDistanceMetres { get; set; } = 500;
}

public sealed class FilterResponse
{
    public List<SearchResult> Results { get; set; } = [];
    public DateTime? LastUpdateTime { get; set; }
    public int TotalFound { get; set; }
}

public sealed class DataStatusResponse
{
    public bool IsAvailable { get; set; }
    public DateTime? LastSuccessfulPoll { get; set; }
    public DateTime? LastAttemptedPoll { get; set; }
    public int RecordsLoaded { get; set; }
    public bool IsStale { get; set; }
}
