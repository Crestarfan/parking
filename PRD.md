# Smart Parking Navigator – Product Requirements Document (PRD)

**Version:** 1.0  
**Status:** Draft  
**Date:** 2026-08-19

---

## Executive Summary

**Smart Parking Navigator** is a web application that helps users find available HDB car parks near their Singapore destination in real time. Rather than displaying all car parks equally, it recommends the most suitable options based on current availability, proximity, and user preferences.

The MVP delivers a destination-aware search interface, real-time availability data, condition-based filtering, and transparent freshness indicators. Future agentic capabilities are deferred to a separate phase.

---

## Target Users

- **Commuters & Drivers** searching for parking near a specific destination
- **Delivery & Commercial Drivers** optimizing for available lots and vehicle restrictions
- **Casual Visitors** unfamiliar with Singapore's parking ecosystem
- **Users requiring accessible, simple information** about parking conditions now

---

## Singapore Context

The Smart Parking Navigator operates exclusively within Singapore and leverages:

1. **HDB Car Park Registry**: A comprehensive static dataset of ~2,000 HDB car parks with:
   - Location coordinates (SVY21 system, to be converted to WGS84)
   - Parking conditions (free/paid, night availability, deck count)
   - Vehicle restrictions (entrance height, lot types for cars/vans/motorcycles)

2. **Data.gov.sg Carpark Availability API**: Real-time polling (recommended interval: 1 minute) for:
   - Total and available parking lots by type
   - Update timestamp (data freshness)
   - Current occupancy snapshot

3. **Google Maps Integration**:
   - Destination search (address & coordinates)
   - Map rendering and distance calculation
   - Browser-side only; API key never exposed

---

## User Journeys

### Journey 1: Search by Destination

1. User enters a destination (address, place name, or coordinates) in the WebApp search box
2. Google Maps autocomplete suggests matches; user selects one
3. WebApp calls ApiApp with destination coordinates and retrieves nearby car parks sorted by distance
4. Map displays nearby car parks; list view shows availability, distance, and key conditions
5. User reviews freshness indicator and chooses a car park to explore

**Acceptance**: Destination is resolved to coordinates; car parks within ~500m are ranked by distance and availability.

### Journey 2: Apply Filters & Explore Details

1. User refines results using condition-based filters:
   - **Available only**: Show only car parks with ≥1 available lot
   - **Free parking**: Show only free car parks (or paid by time)
   - **Night parking**: Show only car parks open at night
   - **Vehicle type**: Filter by lot type (car, van, motorcycle) or entrance height
   - **Car park type**: Surface, multi-storey, underground
2. Filtered results appear on both map and list
3. User clicks a car park to see full details: address, rates, operating hours, entrance restrictions, deck count, parking system type
4. User checks data update time and decides to park

**Acceptance**: Filters work independently and in combination; results update instantly.

### Journey 3: Handle Empty or Stale Results

1. Search returns no results or shows no available car parks
2. App clearly displays the situation (e.g., "No car parks with available lots within 500m")
3. Freshness indicator shows how old the data is
4. User can:
   - Relax a filter (e.g., include unavailable lots)
   - Expand search radius
   - Refresh manually

**Acceptance**: No results are presented transparently; users are never misled.

---

## MVP Scope

### In Scope – Core Features

#### 1. Destination Search
- Search box with Google Maps autocomplete
- Input validation (address, place name, or manual coordinates)
- Display selected destination on map with confirmation

#### 2. Real-Time Availability Display
- Load HDB car park registry on startup
- Poll data.gov.sg API at 1-minute intervals
- Join static (HDB) and live (data.gov.sg) records by car_park_no
- Display on map and in sortable list:
  - Car park name
  - Distance to destination (calculated by ApiApp)
  - Total and available lots (by type)
  - Occupancy rate
  - Last update time (with "stale" warning if ≥ 5 minutes old)

#### 3. Car Park Details
- Address (from HDB data)
- Parking system type (electronic or coupon-based)
- Free parking conditions and time windows
- Night parking availability
- Entrance height restriction
- Car park type (surface, multi-storey, underground)
- Number of decks
- Lot type availability (cars, heavy vehicles, motorcycles, others)

#### 4. Condition-Based Filters
- Available lots only (≥1 available in any type)
- Free parking (exclude paid car parks)
- Night parking (open 22:00–05:59)
- Vehicle type: Select one or more lot types
- Entrance height: Minimum or exact restriction
- Car park type: Checkboxes for surface / multi-storey / underground

#### 5. Data Freshness & Status
- Display last API update time on map and list
- Warn when data is ≥5 minutes old
- Handle API failures gracefully:
  - Show last successful data with timestamp
  - Display error message (e.g., "Data.gov.sg API unavailable")
  - Offer manual refresh

#### 6. Error & Empty States
- "Destination not found" – clear message, retry option
- "No car parks found" – explain filters applied, suggest relaxing them
- "API temporarily unavailable" – show last known data
- "No live data yet" – on first startup or after extended outage

#### 7. Map & List Synchronization
- Clicking a car park on the map highlights it in the list and shows details panel
- Scrolling the list keeps map viewport in sync
- Filter changes update both views simultaneously

---

## MVP Exclusions (Out of Scope)

The following are **explicitly deferred** and not part of this MVP:

- **Agentic AI / Agent-Assisted Recommendations**: Deferred to step 05 capstone
- **Occupancy Forecasting**: Requires historical data collection (multi-phase effort)
- **Availability Alerts / Notifications**: Requires user accounts and push infrastructure
- **Traffic & Weather Integration**: Beyond parking core domain
- **Favorites & Saved Locations**: Requires state management (session/account)
- **Accessibility Alerts**: Advanced filtering beyond initial scope
- **Reservations or Booking**: Not supported by data.gov.sg API
- **Parking Rates / Dynamic Pricing Calculation**: Out of scope
- **Deployment, Database, or MCP Infrastructure**: Local development only
- **Copilot Canvas Integration**: Deferred to step 04

---

## Acceptance Criteria

### Functional Acceptance

- [ ] **Destination Search**: User can search by address and confirm selection on the map
- [ ] **Live Data Integration**: ApiApp loads HDB CSV, polls data.gov.sg, and joins records safely
- [ ] **Coordinate Conversion**: SVY21 coordinates are converted to WGS84 for map display
- [ ] **Search Results**: Car parks within 500m are returned, sorted by distance, with live availability
- [ ] **Filtering**: All condition-based filters work independently and in combination
- [ ] **Details Panel**: Full car park information is accessible from map or list
- [ ] **Freshness Indicators**: Data update time is displayed; ≥5 min old data is flagged as stale
- [ ] **Error Handling**: Empty results, API failures, and stale data are displayed transparently
- [ ] **No Direct Browser Access**: WebApp never calls data.gov.sg; API key is never sent to the browser
- [ ] **Map-List Sync**: Selection, filtering, and viewport changes are synchronized

### Technical Acceptance

- [ ] **Unit Tests**: ApiApp logic (coordinate conversion, distance, filtering) is covered
- [ ] **Integration Tests**: HDB–data.gov.sg join logic and freshness handling are verified
- [ ] **Component Tests**: WebApp filters, list rendering, and error states are tested
- [ ] **End-to-End Tests**: Full flow (search → filter → view details) works in browser
- [ ] **Aspire Startup**: Complete application starts with `aspire run` and is accessible via web
- [ ] **No Committed Credentials**: data.gov.sg and Google Maps keys are in secrets/environment only
- [ ] **Graceful Degradation**: WebApp remains usable if data is stale or partially unavailable

### Documentation Acceptance

- [ ] **README.md** updated with MVP scope and user journeys
- [ ] **TRD.md** (Technical Requirements) defines architecture, API boundaries, and test strategy
- [ ] **AGENTS.md** includes verified build, run, and validation commands
- [ ] **Code Comments**: Critical data transformations (SVY21, join, freshness logic) are documented

---

## Success Metrics

- Users can find and confirm a parking decision within **30 seconds** of searching
- **≥95% of data.gov.sg car parks** are successfully joined with HDB static information
- Data freshness is clear and accurate; ≥95% of users understand whether data is current
- **Zero data.gov.sg API key exposure** in browser traffic (verified by audit)
- **All MVP acceptance criteria** are met in automated and manual testing

---

## Non-Functional Requirements

- **Performance**: Search results load within 1 second; filter updates are instant
- **Reliability**: API polling and data joins are resilient to upstream transient failures
- **Security**: Credentials are never committed; user inputs are treated as untrusted
- **Maintainability**: Code is organized by layer (WebApp, ApiApp, AppHost); each layer is independently testable
- **Accessibility**: Map and list are navigable by keyboard; error messages are clear and actionable

---

## Next Steps (Post-MVP, Deferred)

1. **Step 04**: Copilot Canvas extension (separate issue #4)
2. **Step 05**: Agentic AI assistant with agent framework (separate issue #5)
3. **Future**: Occupancy forecasting, alerts, historical analytics, traffic integration

---

## Revision History

| Version | Date | Author | Change |
|---------|------|--------|--------|
| 1.0 | 2026-08-19 | Copilot | Initial draft from IDEATION.md and issue #2 |

---

## Sign-Off

**PRD Status**: ⏳ **Awaiting Review & Sign-Off**

This document is ready for technical review and stakeholder approval before TRD and AGENTS.md are generated.
