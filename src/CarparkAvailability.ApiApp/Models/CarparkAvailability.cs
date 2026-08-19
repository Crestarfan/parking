using System.Text.Json.Serialization;

namespace CarparkAvailability.ApiApp.Models;

public sealed class CarparkAvailabilityResponse
{
    [JsonPropertyName("items")]
    public List<CarparkAvailabilityItem> Items { get; set; } = [];
}

public sealed class CarparkAvailabilityItem
{
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("carpark_data")]
    public List<CarparkData> CarparkData { get; set; } = [];
}

public sealed class CarparkData
{
    [JsonPropertyName("carpark_number")]
    public string CarparkNumber { get; set; } = string.Empty;

    [JsonPropertyName("update_datetime")]
    public DateTime? UpdateDateTime { get; set; }

    [JsonPropertyName("carpark_info")]
    public List<CarparkInfo> CarparkInfo { get; set; } = [];
}

public sealed class CarparkInfo
{
    [JsonPropertyName("total_lots")]
    public string TotalLots { get; set; } = string.Empty;

    [JsonPropertyName("lot_type")]
    public string LotType { get; set; } = string.Empty;

    [JsonPropertyName("lots_available")]
    public string LotsAvailable { get; set; } = string.Empty;

    [JsonIgnore]
    public DateTime? UpdateDateTime { get; set; }
}
