using CarparkAvailability.ApiApp.Models;
using CarparkAvailability.ApiApp.Services;

namespace CarparkAvailability.Tests;

public sealed class FilteringTests
{
    private static readonly DateTime UtcNow = new(2026, 8, 19, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Filter_AppliesAvailabilityAndNightParking()
    {
        IReadOnlyDictionary<string, HdbCarPark> hdbRecords = CreateHdbRecords();
        IReadOnlyDictionary<string, CarparkInfo[]> liveData = CreateLiveData();
        FilterRequest request = new()
        {
            Lat = 1.3010,
            Lng = 103.8518,
            MaxDistanceMetres = 500,
            AvailableOnly = true,
            NightParking = true
        };

        IReadOnlyList<SearchResult> results = CarparkSearchLogic.Filter(hdbRecords, liveData, request, UtcNow);

        Assert.Single(results);
        Assert.Equal("ACB", results[0].CarParkNo);
    }

    [Fact]
    public void Filter_RestrictsVehicleTypes()
    {
        IReadOnlyDictionary<string, HdbCarPark> hdbRecords = CreateHdbRecords();
        IReadOnlyDictionary<string, CarparkInfo[]> liveData = CreateLiveData();
        FilterRequest request = new()
        {
            Lat = 1.3010,
            Lng = 103.8518,
            MaxDistanceMetres = 500,
            VehicleTypes = ["M"]
        };

        IReadOnlyList<SearchResult> results = CarparkSearchLogic.Filter(hdbRecords, liveData, request, UtcNow);

        Assert.Single(results);
        Assert.Equal("ACB", results[0].CarParkNo);
        Assert.Equal(12, results[0].AvailableLots);
        Assert.Equal(20, results[0].TotalLots);
    }

    [Fact]
    public void Filter_AppliesCarParkTypeAndGantryHeight()
    {
        IReadOnlyDictionary<string, HdbCarPark> hdbRecords = CreateHdbRecords();
        IReadOnlyDictionary<string, CarparkInfo[]> liveData = CreateLiveData();
        FilterRequest request = new()
        {
            Lat = 1.3209,
            Lng = 103.8833,
            MaxDistanceMetres = 500,
            CarParkTypes = ["MULTI-STOREY"],
            MinGantryHeight = 2.0
        };

        IReadOnlyList<SearchResult> results = CarparkSearchLogic.Filter(hdbRecords, liveData, request, UtcNow);

        Assert.Single(results);
        Assert.Equal("ACM", results[0].CarParkNo);
    }

    private static IReadOnlyDictionary<string, HdbCarPark> CreateHdbRecords()
        => new Dictionary<string, HdbCarPark>(StringComparer.OrdinalIgnoreCase)
        {
            ["ACB"] = new()
            {
                CarParkNo = "ACB",
                Address = "Albert Centre",
                CarParkType = "BASEMENT CAR PARK",
                FreeParking = "NO",
                NightParking = "YES",
                GantryHeight = 1.8,
                Lat = 1.3010,
                Lng = 103.8518
            },
            ["ACM"] = new()
            {
                CarParkNo = "ACM",
                Address = "Aljunied Crescent",
                CarParkType = "MULTI-STOREY CAR PARK",
                FreeParking = "SUN & PH FR 7AM-10.30PM",
                NightParking = "YES",
                GantryHeight = 2.1,
                Lat = 1.3209,
                Lng = 103.8833
            },
            ["AK19"] = new()
            {
                CarParkNo = "AK19",
                Address = "Ang Mo Kio Street 21",
                CarParkType = "SURFACE CAR PARK",
                FreeParking = "NO",
                NightParking = "NO",
                GantryHeight = 0,
                Lat = 1.3689,
                Lng = 103.8450
            }
        };

    private static IReadOnlyDictionary<string, CarparkInfo[]> CreateLiveData()
        => new Dictionary<string, CarparkInfo[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ACB"] =
            [
                new CarparkInfo { LotType = "C", LotsAvailable = "30", TotalLots = "50", UpdateDateTime = UtcNow },
                new CarparkInfo { LotType = "M", LotsAvailable = "12", TotalLots = "20", UpdateDateTime = UtcNow }
            ],
            ["ACM"] =
            [
                new CarparkInfo { LotType = "C", LotsAvailable = "5", TotalLots = "80", UpdateDateTime = UtcNow }
            ],
            ["AK19"] =
            [
                new CarparkInfo { LotType = "C", LotsAvailable = "0", TotalLots = "30", UpdateDateTime = UtcNow }
            ]
        };
}
