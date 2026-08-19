window.smartParkingMaps = (() => {
    const state = {
        map: null,
        markers: new Map(),
        destinationMarker: null,
        selectedMarker: null,
        dotNetRef: null,
        autocomplete: null,
        searchInputHooked: false,
        destinationLocation: null,
        infoWindow: null,
        scriptPromise: null
    };

    function ensureGoogleLoaded(apiKey) {
        if (window.google?.maps?.Map) {
            return Promise.resolve();
        }

        if (state.scriptPromise) {
            return state.scriptPromise;
        }

        state.scriptPromise = new Promise((resolve, reject) => {
            if (!apiKey) {
                waitForGoogleMaps(resolve, reject);
                return;
            }

            const existing = document.querySelector('script[data-google-maps="true"]');
            if (existing) {
                waitForGoogleMaps(resolve, reject);
                return;
            }

            const script = document.createElement('script');
            script.dataset.googleMaps = 'true';
            script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`;
            script.async = true;
            script.defer = true;
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });

        return state.scriptPromise;
    }

    function waitForGoogleMaps(resolve, reject, startedAt = Date.now()) {
        if (window.google?.maps?.Map) {
            resolve();
            return;
        }

        if (Date.now() - startedAt > 15000) {
            reject(new Error('Google Maps failed to load within 15 seconds.'));
            return;
        }

        setTimeout(() => waitForGoogleMaps(resolve, reject, startedAt), 200);
    }

    async function initMap(apiKey, lat, lng, dotNetRef) {
        await ensureGoogleLoaded(apiKey);

        const element = document.getElementById('map');
        if (!element || !window.google?.maps?.Map) {
            return;
        }

        state.dotNetRef = dotNetRef;
        state.infoWindow = state.infoWindow || new google.maps.InfoWindow();
        const center = { lat, lng };

        if (!state.map) {
            state.map = new google.maps.Map(element, {
                center,
                zoom: 16,
                mapTypeControl: false,
                streetViewControl: false,
                fullscreenControl: false
            });
        } else {
            state.map.setCenter(center);
        }

        setDestinationMarker(lat, lng);
        setupAutocomplete();
    }

    function setupAutocomplete() {
        if (state.searchInputHooked || !state.map || !google.maps.places?.Autocomplete) {
            return;
        }

        const input = document.getElementById('destination-search');
        if (!input) {
            return;
        }

        state.autocomplete = new google.maps.places.Autocomplete(input, {
            componentRestrictions: { country: 'sg' },
            fields: ['formatted_address', 'geometry', 'name']
        });

        state.autocomplete.addListener('place_changed', () => {
            const place = state.autocomplete.getPlace();
            const location = place?.geometry?.location;
            if (!location) {
                return;
            }

            const address = place.formatted_address || place.name || input.value;
            const lat = location.lat();
            const lng = location.lng();
            state.destinationLocation = { lat, lng };
            setDestinationMarker(lat, lng);
            state.map.panTo({ lat, lng });

            if (state.dotNetRef) {
                state.dotNetRef.invokeMethodAsync('OnDestinationSelected', address, lat, lng);
            }
        });

        state.searchInputHooked = true;
    }

    function setMarkers(carParks) {
        if (!state.map || !Array.isArray(carParks)) {
            return;
        }

        for (const marker of state.markers.values()) {
            marker.setMap(null);
        }
        state.markers.clear();

        const bounds = new google.maps.LatLngBounds();
        let hasMarkers = false;

        for (const carPark of carParks) {
            const marker = new google.maps.Marker({
                map: state.map,
                position: { lat: carPark.lat, lng: carPark.lng },
                title: carPark.carParkNo
            });

            marker.addListener('click', () => {
                highlightMarker(carPark.carParkNo);
                state.infoWindow?.setContent(`<strong>${carPark.carParkNo}</strong>`);
                state.infoWindow?.open({ anchor: marker, map: state.map });

                if (state.dotNetRef) {
                    state.dotNetRef.invokeMethodAsync('OnMarkerClick', carPark.carParkNo);
                }
            });

            state.markers.set(carPark.carParkNo, marker);
            bounds.extend(marker.getPosition());
            hasMarkers = true;
        }

        if (state.destinationMarker?.getPosition()) {
            bounds.extend(state.destinationMarker.getPosition());
            hasMarkers = true;
        }

        if (hasMarkers) {
            state.map.fitBounds(bounds, 64);
        }
    }

    function highlightMarker(carParkNo) {
        if (state.selectedMarker) {
            state.selectedMarker.setAnimation(null);
        }

        const marker = state.markers.get(carParkNo);
        if (!marker) {
            return;
        }

        marker.setAnimation(google.maps.Animation.BOUNCE);
        window.setTimeout(() => marker.setAnimation(null), 1400);
        state.selectedMarker = marker;
        state.map?.panTo(marker.getPosition());
    }

    function setDestinationMarker(lat, lng) {
        if (!state.map || !window.google?.maps?.Marker) {
            return;
        }

        const position = { lat, lng };
        if (!state.destinationMarker) {
            state.destinationMarker = new google.maps.Marker({
                map: state.map,
                position,
                title: 'Destination',
                icon: {
                    path: google.maps.SymbolPath.CIRCLE,
                    fillColor: '#0f6b4f',
                    fillOpacity: 1,
                    strokeColor: '#ffffff',
                    strokeWeight: 2,
                    scale: 8
                }
            });
        } else {
            state.destinationMarker.setPosition(position);
        }

        state.map.panTo(position);
    }

    return {
        initMap,
        setMarkers,
        highlightMarker,
        setDestinationMarker
    };
})();
