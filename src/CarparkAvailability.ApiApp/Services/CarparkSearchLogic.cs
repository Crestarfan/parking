using CarparkAvailability.ApiApp.Models;

namespace CarparkAvailability.ApiApp.Services;

public static class CarparkSearchLogic
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<SearchResult> Search(
        IReadOnlyDictionary<string, HdbCarPark> hdbRecords,
        IReadOnlyDictionary<string, CarparkInfo[]> liveData,
        SearchRequest request,
        DateTime utcNow)
    {
        return hdbRecords.Values
            .Select(record => BuildSearchResult(record, liveData, request.Lat, request.Lng, null, utcNow))
            .Where(result => result.Distance <= request.MaxDistanceMetres)
            .OrderBy(result => result.Distance)
            .ToList();
    }

    public static IReadOnlyList<SearchResult> Filter(
        IReadOnlyDictionary<string, HdbCarPark> hdbRecords,
        IReadOnlyDictionary<string, CarparkInfo[]> liveData,
        FilterRequest request,
        DateTime utcNow)
    {
        HashSet<string> vehicleTypes = NormalizeSet(request.VehicleTypes);
        HashSet<string> carParkTypes = NormalizeSet(request.CarParkTypes);
        HashSet<string>? vehicleTypeFilter = vehicleTypes.Count > 0 ? vehicleTypes : null;

        return hdbRecords.Values
            .Where(record => MatchesRecordFilters(record, request, carParkTypes))
            .Select(record => BuildProjectedResult(record, liveData, request.Lat, request.Lng, vehicleTypeFilter, utcNow))
            .Where(projected => projected.Result.Distance <= request.MaxDistanceMetres)
            .Where(projected => !request.AvailableOnly || projected.Result.AvailableLots > 0)
            .Where(projected => vehicleTypeFilter is null || projected.HasMatchedInfo)
            .OrderBy(projected => projected.Result.Distance)
            .Select(projected => projected.Result)
            .ToList();
    }

    public static CarparkDetailResponse BuildDetailResponse(
        HdbCarPark record,
        IReadOnlyDictionary<string, CarparkInfo[]> liveData,
        DateTime utcNow)
    {
        SearchResult result = BuildSearchResult(record, liveData, record.Lat, record.Lng, null, utcNow);

        return new CarparkDetailResponse
        {
            CarParkNo = record.CarParkNo,
            Address = record.Address,
            XCoord = record.XCoord,
            YCoord = record.YCoord,
            CarParkType = record.CarParkType,
            TypeOfParkingSystem = record.TypeOfParkingSystem,
            ShortTermParking = record.ShortTermParking,
            FreeParking = record.FreeParking,
            NightParking = record.NightParking,
            CarParkDecks = record.CarParkDecks,
            GantryHeight = record.GantryHeight,
            CarParkBasement = record.CarParkBasement,
            Lat = record.Lat,
            Lng = record.Lng,
            AvailableLots = result.AvailableLots,
            TotalLots = result.TotalLots,
            OccupancyRate = result.OccupancyRate,
            LastUpdateTime = result.LastUpdateTime,
            IsStale = result.IsStale
        };
    }

    public static bool IsStale(DateTime? lastUpdateTime, DateTime utcNow)
        => lastUpdateTime.HasValue && utcNow - lastUpdateTime.Value.ToUniversalTime() >= StaleThreshold;

    private static bool MatchesRecordFilters(HdbCarPark record, FilterRequest request, HashSet<string> carParkTypes)
    {
        if (request.FreeParking && string.Equals(record.FreeParking, "NO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.NightParking && !string.Equals(record.NightParking, "YES", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.MinGantryHeight > 0 && record.GantryHeight < request.MinGantryHeight)
        {
            return false;
        }

        if (carParkTypes.Count > 0)
        {
            string normalizedType = NormalizeValue(record.CarParkType);
            if (!carParkTypes.Any(filter => normalizedType.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private static SearchResult BuildSearchResult(
        HdbCarPark record,
        IReadOnlyDictionary<string, CarparkInfo[]> liveData,
        double requestLat,
        double requestLng,
        HashSet<string>? vehicleTypes,
        DateTime utcNow)
    {
        AvailabilitySnapshot availability = AggregateAvailability(record.CarParkNo, liveData, vehicleTypes);
        return BuildSearchResult(record, availability, requestLat, requestLng, utcNow);
    }

    private static SearchResult BuildSearchResult(
        HdbCarPark record,
        AvailabilitySnapshot availability,
        double requestLat,
        double requestLng,
        DateTime utcNow)
    {
        double distance = HaversineDistance.Calculate(requestLat, requestLng, record.Lat, record.Lng);

        return new SearchResult
        {
            CarParkNo = record.CarParkNo,
            Address = record.Address,
            Lat = record.Lat,
            Lng = record.Lng,
            Distance = distance,
            AvailableLots = availability.AvailableLots,
            TotalLots = availability.TotalLots,
            OccupancyRate = availability.TotalLots > 0 ? (double)availability.AvailableLots / availability.TotalLots : 0,
            NightParking = string.Equals(record.NightParking, "YES", StringComparison.OrdinalIgnoreCase),
            LastUpdateTime = availability.LastUpdateTime,
            IsStale = IsStale(availability.LastUpdateTime, utcNow)
        };
    }

    private static ProjectedResult BuildProjectedResult(
        HdbCarPark record,
        IReadOnlyDictionary<string, CarparkInfo[]> liveData,
        double requestLat,
        double requestLng,
        HashSet<string>? vehicleTypes,
        DateTime utcNow)
    {
        AvailabilitySnapshot availability = AggregateAvailability(record.CarParkNo, liveData, vehicleTypes);
        SearchResult result = BuildSearchResult(record, availability, requestLat, requestLng, utcNow);
        return new ProjectedResult(result, availability.HasMatchedInfo);
    }

    private static AvailabilitySnapshot AggregateAvailability(
        string carParkNo,
        IReadOnlyDictionary<string, CarparkInfo[]> liveData,
        HashSet<string>? vehicleTypes)
    {
        if (!liveData.TryGetValue(carParkNo, out CarparkInfo[]? infos))
        {
            return new AvailabilitySnapshot(0, 0, null, false);
        }

        IEnumerable<CarparkInfo> filteredInfos = infos;
        if (vehicleTypes is not null)
        {
            filteredInfos = filteredInfos.Where(info => vehicleTypes.Contains(NormalizeValue(info.LotType)));
        }

        CarparkInfo[] selectedInfos = filteredInfos.ToArray();
        if (selectedInfos.Length == 0)
        {
            return new AvailabilitySnapshot(0, 0, null, false);
        }

        int availableLots = selectedInfos.Sum(info => ParseInt(info.LotsAvailable));
        int totalLots = selectedInfos.Sum(info => ParseInt(info.TotalLots));
        DateTime? lastUpdateTime = selectedInfos
            .Where(info => info.UpdateDateTime.HasValue)
            .Select(info => info.UpdateDateTime!.Value)
            .OrderByDescending(value => value)
            .Cast<DateTime?>()
            .FirstOrDefault();

        return new AvailabilitySnapshot(availableLots, totalLots, lastUpdateTime, true);
    }

    private static int ParseInt(string? value)
        => int.TryParse(value, out int parsed) ? parsed : 0;

    private static HashSet<string> NormalizeSet(IEnumerable<string>? values)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeValue)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
           ?? [];

    private static string NormalizeValue(string? value)
        => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed record AvailabilitySnapshot(int AvailableLots, int TotalLots, DateTime? LastUpdateTime, bool HasMatchedInfo);

    private sealed record ProjectedResult(SearchResult Result, bool HasMatchedInfo);
}
