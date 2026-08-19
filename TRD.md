# Smart Parking Navigator – Technical Requirements Document (TRD)

**Version:** 1.0  
**Status:** Draft  
**Date:** 2026-08-19  
**Aligned with PRD v1.0**

---

## Executive Summary

This document defines the technical implementation strategy for the Smart Parking Navigator MVP described in PRD.md. It assigns clear responsibilities to each component within the prepared Aspire solution, specifies data processing requirements, establishes API boundaries, and defines test coverage.

The application is composed of four .NET projects:

1. **ApiApp**: ASP.NET Core backend service (data.gov.sg integration, HDB CSV loading, coordinate conversion, filtering, distance calculation)
2. **WebApp**: Blazor frontend (destination search, real-time display, filtering UI, map-list synchronization)
3. **AppHost**: .NET Aspire orchestration (service configuration and startup)
4. **ServiceDefaults**: Shared defaults (logging, resilience, health checks)

---

## Architecture Overview

### Component Responsibilities

#### ApiApp – Backend API Service

**Purpose**: Authoritative source for parking data, coordinate conversion, filtering, and distance calculations.

**Responsibilities**:
- Load HDB car park static registry (`HDBCarparkInformation.csv`) on startup
- Poll data.gov.sg Carpark Availability API at 1-minute intervals (configurable)
- Join HDB static records with live data.gov.sg records by `car_park_no`
- Convert SVY21 coordinates to WGS84 for map display
- Expose typed REST endpoints for search, detail, and data-status queries
- Perform distance calculations and occupancy filtering
- Track data freshness and API failure states
- Provide valid error responses with last-known data

**Data Ownership**:
- Holds and maintains the data.gov.sg API key (never exposed)
- Caches HDB CSV in memory or local storage
- Manages real-time cache of data.gov.sg responses
- Never allows browser-side calls to data.gov.sg

**Constraints**:
- No database; data is in-memory or volatile cache only
- No persistence across restarts
- Does not call Google Maps API (WebApp-only)
- SVY21 conversion is deterministic and testable

#### WebApp – Blazor Frontend

**Purpose**: User interface for destination search, results exploration, and filtering.

**Responsibilities**:
- Render destination search box with Google Maps autocomplete integration
- Display car parks on an interactive map
- Render car park list with sort and filter controls
- Show car park details on selection
- Render freshness indicators and error/empty states
- Synchronize map and list selections
- Call ApiApp exclusively for parking data (never data.gov.sg)
- Treat all upstream text (addresses, names) as untrusted

**Data Ownership**:
- Holds Google Maps API key (browser-safe)
- Receives car park data exclusively from ApiApp
- Stores no persistent user state (MVP scope)

**Constraints**:
- Never calls data.gov.sg directly
- Never commits credentials
- Keyboard accessible
- All user inputs validated before sending to ApiApp

#### AppHost – Aspire Orchestration

**Purpose**: Service discovery, configuration, and local development orchestration.

**Responsibilities**:
- Configure ApiApp and WebApp services
- Manage environment variables and secrets
- Expose service endpoints for local testing
- Launch and coordinate service startup
- Provide Aspire dashboard integration

**Constraints**:
- Development-only (local execution via `aspire run`)
- No production deployment included
- ServiceDefaults are applied to all services

#### ServiceDefaults – Shared Configuration

**Purpose**: Common resilience, logging, and health-check defaults.

**Responsibilities**:
- Configure OpenTelemetry and logging
- Set up resilience policies (retries, timeouts)
- Define health-check endpoints
- Provide shared constants and utilities

---

## Data Pipeline

### Phase 1: Startup

**Step 1a: Load HDB Car Park Registry**

- **Source**: `data/HDBCarparkInformation.csv` (included in ApiApp output)
- **Format**: CSV with columns:
  - `car_park_no` (string, primary key)
  - `address` (string)
  - `x_coord` (string, SVY21)
  - `y_coord` (string, SVY21)
  - `car_park_type` (enum: SURFACE, MULTI-STOREY, BASEMENT)
  - `type_of_parking_system` (enum: ELECTRONIC, COUPON)
  - `short_term_parking` (string: WHOLE DAY, 7AM-7PM, NO, etc.)
  - `free_parking` (string: YES, NO, SUN & PH FR 7AM-10.30PM, etc.)
  - `night_parking` (string: YES, NO)
  - `car_park_decks` (numeric string)
  - `gantry_height` (numeric string, metres)
  - `car_park_basement` (Y/N)

**Processing**:
- Parse CSV into in-memory lookup (map by `car_park_no`)
- Validate required fields; log warnings for missing optional fields
- Convert numeric strings to typed values (safe parsing with fallback)
- Store ~2,000 HDB records (current dataset has ~2,016 real-time car parks)

**Data Quality**:
- Accept backward-compatible CSV additions (unknown columns)
- Report missing required fields as startup errors
- If parsing fails on a record, log and skip (graceful degradation)

**Step 1b: Initialize Data.gov.sg Polling**

- Configure polling interval (default: 60 seconds, configurable)
- Perform initial API call to populate cache
- Log startup status (success/failure) and timestamp

### Phase 2: Continuous Data Refresh

**Step 2a: Poll data.gov.sg Carpark Availability API**

- **Endpoint**: https://api.data.gov.sg/v1/transport/carpark-availability
- **Method**: GET (no authentication required for public API)
- **Response Schema**:
  ```json
  {
    "items": [
      {
        "carpark_info": [
          {
            "total_lots": "123",
            "lot_type": "C",
            "available_lots": "45"
          }
        ],
        "carpark_number": "ACB",
        "update_datetime": "2026-08-19T12:34:56"
      }
    ]
  }
  ```

**Processing**:
- Validate response against published schema
- Extract `carpark_number`, `update_datetime`, and lot types (C, H, M, Y for car, heavy, motorcycle, other)
- Perform deterministic parsing with strong typing (no free-form string pass-through to frontend)
- Track API poll timestamp and success/failure state

**Data Freshness**:
- Store `update_datetime` from API response
- Calculate staleness: current time - `update_datetime`
- Log and cache latest successful response
- On API failure, keep previous cached response with "unavailable" flag

**Schema Validation**:
- Treat the published OpenAPI spec as the contract
- For unknown optional fields: accept silently (backward-compatible)
- For missing required fields: log validation error and flag car park as partial data
- For type mismatches: log and attempt safe conversion, or mark as unavailable
- If live response diverges from spec: document, validate, and confirm before parser changes

### Phase 3: Data Join & Enrichment

**Step 3: Join HDB Static + data.gov.sg Live Data**

- **Join Key**: `car_park_no` (HDB) ← → `carpark_number` (data.gov.sg)
- **Match Rate**: ~2,008 of ~2,016 records successfully match
- **Handling Unmatched**:
  - HDB car parks with no live data: Excluded from results (live availability is required)
  - data.gov.sg records with no HDB static data: Logged as orphan, excluded from results
- **Handling Partial Data**: Car parks with missing optional HDB fields (e.g., `gantry_height`) are still included but marked as partial

**Joined Record Schema**:

```typescript
interface CarparkSnapshot {
  carParkNo: string;
  address: string;
  lat: number;           // WGS84, from SVY21 conversion
  lng: number;           // WGS84, from SVY21 conversion
  carParkType: string;   // SURFACE, MULTI-STOREY, BASEMENT
  parkingSystem: string; // ELECTRONIC, COUPON
  shortTermParking: string;
  freeParking: string;
  nightParking: boolean;
  carParkDecks: number;
  gantryHeight: number;  // metres, nullable if missing
  basement: boolean;
  // Live data
  totalLots: number;
  availableLots: number;
  occupancyRate: number; // percentage, 0-100
  lotTypes: {
    car: { total: number; available: number; };
    heavy: { total: number; available: number; };
    motorcycle: { total: number; available: number; };
    other: { total: number; available: number; };
  };
  lastUpdateTime: DateTime;
  dataFreshness: {
    isStale: boolean;      // true if ≥ 5 minutes old
    minutesSinceUpdate: number;
    apiFailureState: 'success' | 'unavailable' | 'partial';
  };
}
```

### Phase 4: Coordinate Conversion (SVY21 → WGS84)

**Purpose**: Convert HDB static coordinates to map-usable WGS84 format.

**Algorithm**: 
- Use standard SVY21-to-WGS84 conversion formula
- Input: `x_coord`, `y_coord` (numeric strings from CSV, must parse safely)
- Output: `lat`, `lng` (WGS84 decimal degrees)

**Implementation Notes**:
- Validate numeric parsing (log non-numeric values, exclude record)
- Pre-compute coordinates at startup and cache (do not convert on every search)
- Round to 6 decimal places for map precision
- Unit test: verify at least 3 known conversion pairs

**Reference Implementation**:
- Use a vetted SVY21 library (e.g., from NAS or SVET)
- Alternatively, implement with published government formula
- Document source and any assumptions

---

## API Boundaries

### ApiApp REST Endpoints

#### 1. GET `/api`

**Status Endpoint**

**Response**:
```json
{
  "name": "Smart Parking Navigator API",
  "status": "operational" | "degraded" | "unavailable",
  "lastDataUpdate": "2026-08-19T12:34:56Z",
  "dataFreshness": {
    "isStale": false,
    "minutesSinceUpdate": 1
  },
  "recordsLoaded": 2008,
  "apiHealth": "success" | "failure"
}
```

**Purpose**: Health check and overall system status.

---

#### 2. POST `/api/search`

**Search by Destination**

**Request**:
```json
{
  "latitude": 1.3521,
  "longitude": 103.8198,
  "maxDistanceMetres": 500
}
```

**Response**:
```json
{
  "destination": {
    "latitude": 1.3521,
    "longitude": 103.8198
  },
  "searchRadiusMetres": 500,
  "resultsCount": 15,
  "results": [
    {
      "carParkNo": "ACB",
      "address": "BLK 270/271 ALBERT CENTRE BASEMENT CAR PARK",
      "distance": 145.3,
      "lat": 1.3456,
      "lng": 103.8234,
      "carParkType": "BASEMENT",
      "availableLots": 8,
      "totalLots": 150,
      "occupancyRate": 94.67,
      "nightParking": true,
      "lastUpdateTime": "2026-08-19T12:34:56Z",
      "isStale": false,
      "minutesSinceUpdate": 1
    }
  ],
  "lastDataUpdate": "2026-08-19T12:34:56Z",
  "recordsMatched": 2008
}
```

**Validation**:
- Reject invalid lat/lng (outside Singapore bounds or non-numeric)
- Enforce max distance ≤ 500 metres (specified in PRD)
- Sort results by distance (ascending)

**Distance Calculation**:
- Use Haversine formula for great-circle distance
- Input: destination lat/lng, car park lat/lng
- Output: distance in metres

---

#### 3. GET `/api/carpark/{carParkNo}`

**Carpark Details**

**Response**:
```json
{
  "carParkNo": "ACB",
  "address": "BLK 270/271 ALBERT CENTRE BASEMENT CAR PARK",
  "lat": 1.3456,
  "lng": 103.8234,
  "carParkType": "BASEMENT",
  "parkingSystem": "ELECTRONIC",
  "shortTermParking": "WHOLE DAY",
  "freeParking": "NO",
  "nightParking": true,
  "carParkDecks": 1,
  "gantryHeight": 1.8,
  "basement": true,
  "totalLots": 150,
  "availableLots": 8,
  "occupancyRate": 94.67,
  "lotTypes": {
    "car": { "total": 100, "available": 5 },
    "heavy": { "total": 30, "available": 2 },
    "motorcycle": { "total": 20, "available": 1 },
    "other": { "total": 0, "available": 0 }
  },
  "lastUpdateTime": "2026-08-19T12:34:56Z",
  "isStale": false,
  "minutesSinceUpdate": 1,
  "apiFailureState": "success"
}
```

**Error Response** (404 if not found):
```json
{
  "error": "Car park not found",
  "carParkNo": "UNKNOWN"
}
```

---

#### 4. POST `/api/filter`

**Apply Filters**

**Request**:
```json
{
  "carparks": ["ACB", "ACM", "AH1"],
  "filters": {
    "availableOnly": true,
    "freeParking": false,
    "nightParking": true,
    "vehicleTypes": ["C", "H"],
    "minGantryHeight": 1.8,
    "carParkTypes": ["SURFACE", "MULTI-STOREY"]
  }
}
```

**Response**:
```json
{
  "inputCount": 3,
  "filteredCount": 2,
  "results": [
    {
      "carParkNo": "ACB",
      "address": "...",
      "availableLots": 8,
      "matchReason": "Meets all filters"
    }
  ]
}
```

**Filter Logic**:
- **Available Only**: `availableLots > 0`
- **Free Parking**: `freeParking == "YES"` or time-based check
- **Night Parking**: `nightParking == true`
- **Vehicle Type**: Lot type has available lots (e.g., car, heavy)
- **Gantry Height**: `gantryHeight >= minGantryHeight` (handle nulls)
- **Car Park Type**: `carParkType` matches selection

---

#### 5. GET `/api/data-status`

**Data Freshness & Health**

**Response**:
```json
{
  "lastApiPoll": "2026-08-19T12:35:00Z",
  "lastSuccessfulUpdate": "2026-08-19T12:34:56Z",
  "minutesSinceUpdate": 1,
  "isStale": false,
  "apiStatus": "success",
  "recordsLoaded": 2008,
  "totalRecordsMapped": 2000,
  "unmappedHdbRecords": 8,
  "unmappedLiveRecords": 16,
  "errors": []
}
```

**Purpose**: Transparency about data currency and API health.

---

### WebApp Integration

**WebApp Responsibilities** (do not implement in ApiApp):
- Destination search input box and Google Maps autocomplete
- Map rendering and viewport management
- List rendering and sorting
- Filter UI (checkboxes, sliders, dropdowns)
- Details panel display
- Error state rendering
- Freshness badge styling

**WebApp Calls ApiApp**:
1. On app load: `GET /api` (status check)
2. On destination selected: `POST /api/search` (get nearby car parks)
3. On filter change: `POST /api/filter` (refine results)
4. On car park selection: `GET /api/carpark/{carParkNo}` (details)
5. Periodically: `GET /api/data-status` (refresh freshness badge)

---

## Data Quality & Error Handling

### HDB CSV Ingestion

| Scenario | Handling |
|----------|----------|
| Missing required field | Log error, skip record |
| Non-numeric coordinate | Log warning, exclude from search |
| Unrecognized car park type | Accept, display as-is |
| Duplicate `car_park_no` | Use first occurrence, log warning |
| Encoding issue | Assume UTF-8, log non-ASCII safely |

### data.gov.sg API Errors

| Scenario | Handling |
|----------|----------|
| Transient timeout (< 30s) | Retry with exponential backoff |
| HTTP 5xx | Return last cached data, mark as unavailable |
| Malformed response | Log sanitized example, skip update |
| Schema mismatch | Validate, log specific field, flag as partial |
| Network failure | Use last cached data, set failure state |

### Join Errors

| Scenario | Handling |
|----------|----------|
| HDB record, no live data | Exclude from results |
| Live data, no HDB record | Log as orphan, exclude |
| Stale data ≥ 5 min old | Include but flag as stale |
| Partial data (missing optional) | Include but mark field as unavailable |

### Search Errors

| Scenario | Handling |
|----------|----------|
| Invalid lat/lng | Return 400 Bad Request with message |
| No results | Return empty array, no error |
| API failure during search | Return last cached snapshot, flag unavailable |

### Coordinate Conversion Errors

| Scenario | Handling |
|----------|----------|
| Non-numeric x/y | Log, exclude record from map |
| Out-of-bounds Singapore | Log, exclude |
| Conversion failure | Log, exclude record |

---

## Testing Strategy

### Unit Tests (ApiApp)

**Coordinate Conversion**:
- [ ] Test SVY21→WGS84 with 3+ known pairs (e.g., specific HDB locations)
- [ ] Test boundary conditions (Singapore NW/SE corners)
- [ ] Test numeric string parsing (valid, invalid, edge cases)

**Distance Calculation**:
- [ ] Haversine formula with known pairs (verify ±1% accuracy)
- [ ] Zero distance (same point)
- [ ] Test max distance filtering (≤ 500m)

**Filtering Logic**:
- [ ] Available only (availableLots > 0)
- [ ] Free parking detection
- [ ] Night parking flag
- [ ] Vehicle type (car, heavy, motorcycle, other)
- [ ] Gantry height comparison (handle nulls)
- [ ] Car park type matching

**Data Parsing**:
- [ ] CSV parsing: valid, malformed, missing fields
- [ ] data.gov.sg API schema validation
- [ ] Numeric string conversion (safe fallback)

### Integration Tests (ApiApp)

**HDB–data.gov.sg Join**:
- [ ] Load sample HDB CSV, sample data.gov.sg JSON
- [ ] Verify ~95% match rate (aligned with real data: 2,008/2,016)
- [ ] Verify unmatched records are logged
- [ ] Verify joined record has all expected fields

**API Response Structure**:
- [ ] `/api/search` returns sorted list by distance
- [ ] `/api/filter` removes non-matching records
- [ ] `/api/carpark/{carParkNo}` returns full details
- [ ] `/api/data-status` reflects accurate timestamps

**Error Handling**:
- [ ] Graceful degradation on HDB CSV parse error
- [ ] Last-known data served on API failure
- [ ] Invalid requests return 400/404 with clear messages

### Component Tests (WebApp)

**Destination Search**:
- [ ] Google Maps autocomplete integration
- [ ] Input validation (reject non-coordinate text)
- [ ] Confirmation displays on map

**Filtering**:
- [ ] Each filter type (available, free, night, vehicle, height, type) works independently
- [ ] Combinations of filters produce correct results
- [ ] Results update instantly (no delay)

**Map–List Sync**:
- [ ] Clicking map pin highlights list row
- [ ] Scrolling list keeps map viewport in sync
- [ ] Filter changes update both views

**Error States**:
- [ ] "Destination not found" displays clearly
- [ ] "No results" message is helpful and actionable
- [ ] Stale data warning is visible
- [ ] API failure state shows last-known data

### End-to-End Tests

**User Journey 1: Search by Destination**:
1. Enter destination in search box
2. Confirm selection on map
3. Verify nearby car parks display on map and list within 2 seconds

**User Journey 2: Apply Filters**:
1. Apply "available only" filter
2. Verify only car parks with ≥1 lot display
3. Apply "night parking" filter
4. Verify results respect both filters

**User Journey 3: View Details**:
1. Click a car park on map
2. Verify details panel displays full information
3. Verify freshness indicator is accurate

**User Journey 4: Handle Stale/Missing Data**:
1. Simulate data.gov.sg delay or failure
2. Verify last-known data is displayed with stale flag
3. Verify manual refresh option is available

### Automated Test Coverage

- [ ] Unit tests: ≥80% code coverage (coordinate, distance, filter logic)
- [ ] Integration tests: All API endpoints with sample data
- [ ] Component tests: All UI states and interactions
- [ ] E2E tests: All three user journeys via Selenium/Playwright

---

## Configuration & Secrets

### ApiApp Configuration

**appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": { "Default": "Information" }
  },
  "DataGovSg": {
    "BaseUrl": "https://api.data.gov.sg/v1/transport",
    "PollingIntervalSeconds": 60,
    "TimeoutSeconds": 10,
    "RetryAttempts": 3
  },
  "Coordinates": {
    "MaxDistanceMetres": 500,
    "StaleThresholdMinutes": 5
  }
}
```

**Secrets (user-secrets or environment variables)**:
```
DataGovSg:ApiKey=<your-api-key>
```

**Note**: The public data.gov.sg Carpark Availability endpoint does not require an API key. If future endpoints require authentication, credentials must be stored as secrets, never committed.

### WebApp Configuration

**appsettings.json**:
```json
{
  "GoogleMaps": {
    "ApiKeyProperty": "AIzaSy...",
    "DefaultZoom": 13
  },
  "ApiServer": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

**Notes**:
- Google Maps API key is browser-safe (restriction-based)
- ApiServer URL is injected by Aspire at runtime
- No data.gov.sg key in WebApp configuration

### Aspire Configuration

**AppHost Program.cs**:
- Register ApiApp and WebApp services
- Expose ports and environment variables
- Pass secrets from `dotnet user-secrets` to services
- Define health checks and logging

---

## Build, Run, and Validation

### Prerequisites

- .NET 9.0 or later (per `global.json`)
- Visual Studio or VS Code with C# extension
- Docker (optional, for Aspire Dashboard)

### Build

```bash
dotnet restore CarparkAvailability.slnx
dotnet build CarparkAvailability.slnx --no-restore
```

**Expected Output**:
- All projects build without warnings (or approved warnings only)
- `src/*/bin/Release/net9.0/` binaries exist

### Run

```bash
# Set secrets (one time)
dotnet user-secrets set "DataGovSg:ApiKey" "<your-api-key>" \
  --project src/CarparkAvailability.AppHost

# Run application
aspire run
```

**Expected Behavior**:
- Aspire orchestration dashboard launches (usually http://localhost:8080)
- ApiApp and WebApp services start
- HDB CSV loads into ApiApp
- data.gov.sg API polling begins
- WebApp is accessible (usually http://localhost:5173 or configured port)
- No errors in console or dashboard logs

### Validation Checks

**ApiApp Health**:
```bash
curl http://localhost:5000/api
# Expected: { "name": "Smart Parking Navigator API", "status": "operational" }
```

**WebApp Accessibility**:
- Open browser to WebApp URL
- Verify destination search box is interactive
- Verify map loads
- Perform a test search (enter destination, confirm on map)

**Data Freshness**:
```bash
curl http://localhost:5000/api/data-status
# Expected: isStale = false, minutesSinceUpdate < 5
```

**Coordinate Conversion**:
```bash
curl -X POST http://localhost:5000/api/search \
  -H "Content-Type: application/json" \
  -d '{"latitude": 1.3521, "longitude": 103.8198, "maxDistanceMetres": 500}'
# Expected: results array with lat/lng in WGS84 range (1.2–1.5 lat, 103.6–104.0 lng)
```

---

## Performance & Reliability

### Performance Targets

- **Search Response**: ≤1 second (≤500m, ~15 car parks)
- **Filter Updates**: Instant (in-memory filtering)
- **Map Render**: ≤2 seconds for 30+ markers
- **Data Poll**: 1 per 60 seconds (background, non-blocking)

### Reliability Targets

- **API Uptime**: ≥99% during polling (resilience policy handles transients)
- **Data Freshness**: ≤5 minutes stale (or marked as stale)
- **Join Success Rate**: ≥95% (expect 2,008/2,016 matched)
- **Zero Data.gov.sg Key Exposure**: Verified by code review and audit

### Resilience Policies

- **Polling Timeout**: 10 seconds with exponential backoff (3 retries)
- **Search Timeout**: 5 seconds
- **Last-Known Data Fallback**: Serve cached snapshot if API fails
- **Graceful Degradation**: Exclude unmatched/partial records; do not fail entire search

---

## Project Boundaries & Out of Scope

**In Scope**:
- Local development and testing via Aspire
- In-memory data caching
- Real-time polling at 1-minute interval
- Typed API contracts (no free-form data to frontend)
- Coordinate conversion and distance calculation
- Condition-based filtering
- Error and stale-data states

**Out of Scope** (Deferred):
- Production deployment (cloud, orchestration, databases)
- Persistent data storage
- User accounts or authentication
- Occupancy forecasting or historical analysis
- Agentic AI or natural-language interpretation (step 05)
- Copilot Canvas integration (step 04)
- Traffic, weather, or booking APIs

---

## Dependencies & Packages

### ApiApp

- `Microsoft.AspNetCore.OpenApi` (Swagger/OpenAPI)
- `CsvHelper` or `CsvDataReader` (CSV parsing, if chosen)
- `ServiceDefaults` project (shared)

### WebApp

- `Blazor` (ASP.NET Core Blazor WebAssembly or Server)
- `GoogleMapsAPI` JavaScript/SDK
- `ServiceDefaults` project (shared)

### AppHost

- `Microsoft.Extensions.ServiceDiscovery`
- `Microsoft.Extensions.Hosting`
- `ServiceDefaults` project (shared)

### ServiceDefaults

- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.Diagnostics`
- `OpenTelemetry.*` (if configured)

**Note**: Versions are managed in `Directory.Packages.props`; no direct version pinning in csproj files.

---

## Documentation & Inline Code Comments

**Required Inline Comments**:
- SVY21→WGS84 conversion algorithm (with reference to formula)
- HDB–data.gov.sg join logic (including match rate expectations)
- Stale data thresholds and handling
- Distance calculation and max-distance enforcement
- API failure recovery and last-known-data fallback
- Filter combination logic
- Numeric string parsing with safe conversion

**README.md Updates** (in separate commit):
- Summarize MVP scope
- Link to PRD.md and TRD.md
- Include verified build and run commands
- Note credential configuration
- Link to Aspire documentation

---

## Sign-Off & Acceptance

**TRD Status**: ⏳ **Awaiting Review & Sign-Off**

This document is ready for technical review before ApiApp, WebApp, and AppHost implementation begins.

---

## Revision History

| Version | Date | Author | Change |
|---------|------|--------|--------|
| 1.0 | 2026-08-19 | Copilot | Initial draft aligned with PRD v1.0 |
