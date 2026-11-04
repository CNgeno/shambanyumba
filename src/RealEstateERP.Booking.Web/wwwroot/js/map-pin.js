/*
 * Property map pins (Leaflet + OpenStreetMap, no API key).
 *
 * Picker, on property forms:
 *   <div class="map-pin-picker" data-lat-input="Latitude" data-lng-input="Longitude" data-address-inputs="Location,County"></div>
 *   Click or drag to pin, search a place, use the phone's GPS, or paste coordinates / a Google Maps link.
 *
 * Viewer, on listing and stay pages:
 *   <div class="map-pin-view" data-lat="-1.2" data-lng="36.8" data-title="Bethlehem Stays"></div>
 *
 * Kept identical in RealEstateERP.Web and RealEstateERP.Booking.Web (wwwroot/js/map-pin.js).
 */
(function () {
    'use strict';
    var LEAFLET = 'https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/';
    var KENYA = [0.2, 37.9], KENYA_ZOOM = 6, PIN_ZOOM = 16;

    function loadLeaflet(done) {
        if (window.L) return done();
        var css = document.createElement('link');
        css.rel = 'stylesheet'; css.href = LEAFLET + 'leaflet.css';
        document.head.appendChild(css);
        var js = document.createElement('script');
        js.src = LEAFLET + 'leaflet.js';
        js.onload = done;
        js.onerror = function () {
            document.querySelectorAll('.map-pin-picker, .map-pin-view').forEach(function (el) {
                el.innerHTML = '<p class="map-pin-error">The map could not load. Check your connection and refresh.</p>';
            });
        };
        document.head.appendChild(js);
    }

    function layers(map) {
        var street = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        });
        var satellite = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19, attribution: 'Imagery &copy; Esri'
        });
        street.addTo(map);
        L.control.layers({ 'Map': street, 'Satellite': satellite }, null, { position: 'topright' }).addTo(map);
    }

    function fix(n) { return Math.round(n * 1e6) / 1e6; }

    /** "-1.28, 36.82", or a Google Maps / OSM link containing coordinates. */
    function parseCoordinates(text) {
        text = (text || '').trim();
        var patterns = [
            /@(-?\d{1,2}\.\d+),\s*(-?\d{1,3}\.\d+)/,                 // google.com/maps/@-1.28,36.82,15z
            /[?&](?:q|query|ll|destination)=(-?\d{1,2}\.\d+)(?:,|%2C)\s*(-?\d{1,3}\.\d+)/i,
            /!3d(-?\d{1,2}\.\d+)!4d(-?\d{1,3}\.\d+)/,                // place links
            /^(-?\d{1,2}(?:\.\d+)?)\s*[, ]\s*(-?\d{1,3}(?:\.\d+)?)$/  // plain "lat, lng"
        ];
        for (var i = 0; i < patterns.length; i++) {
            var m = text.match(patterns[i]);
            if (m) {
                var lat = parseFloat(m[1]), lng = parseFloat(m[2]);
                if (Math.abs(lat) <= 90 && Math.abs(lng) <= 180) return [lat, lng];
            }
        }
        return null;
    }

    function initPicker(root) {
        var latInput = document.getElementById(root.dataset.latInput);
        var lngInput = document.getElementById(root.dataset.lngInput);
        if (!latInput || !lngInput) return;
        var addressInputs = (root.dataset.addressInputs || '').split(',').map(function (id) { return document.getElementById(id.trim()); }).filter(Boolean);

        root.innerHTML =
            '<div class="map-pin-tools">' +
            '  <input type="search" class="map-pin-search" placeholder="Search a place, or paste coordinates / a Google Maps link" aria-label="Search the map" />' +
            '  <button type="button" class="map-pin-btn" data-act="find">Find</button>' +
            '  <button type="button" class="map-pin-btn" data-act="gps" title="Use this phone\'s location (stand at the property)">&#x1F4CD; My location</button>' +
            '</div>' +
            '<div class="map-pin-canvas"></div>' +
            '<div class="map-pin-status"><span class="map-pin-readout"></span> <button type="button" class="map-pin-link" data-act="clear">Remove pin</button></div>' +
            '<div class="map-pin-results" hidden></div>';

        var search = root.querySelector('.map-pin-search');
        var readout = root.querySelector('.map-pin-readout');
        var clearBtn = root.querySelector('[data-act="clear"]');
        var results = root.querySelector('.map-pin-results');

        var start = latInput.value && lngInput.value ? [parseFloat(latInput.value), parseFloat(lngInput.value)] : null;
        var map = L.map(root.querySelector('.map-pin-canvas'), { scrollWheelZoom: false }).setView(start || KENYA, start ? PIN_ZOOM : KENYA_ZOOM);
        layers(map);
        var marker = null;

        function show() {
            if (marker) {
                var p = marker.getLatLng();
                readout.textContent = 'Pinned at ' + fix(p.lat).toFixed(6) + ', ' + fix(p.lng).toFixed(6) + '. Drag the pin to adjust.';
                clearBtn.hidden = false;
            } else {
                readout.textContent = 'No pin yet. Click the map where the property is.';
                clearBtn.hidden = true;
            }
        }
        function setPin(latlng, zoom) {
            var ll = L.latLng(latlng);
            if (!marker) {
                marker = L.marker(ll, { draggable: true, autoPan: true }).addTo(map);
                marker.on('dragend', function () { write(); });
            } else marker.setLatLng(ll);
            if (zoom) map.setView(ll, Math.max(map.getZoom(), zoom));
            write();
        }
        function write() {
            if (marker) {
                var p = marker.getLatLng();
                latInput.value = fix(p.lat).toFixed(6);
                lngInput.value = fix(p.lng).toFixed(6);
            } else { latInput.value = ''; lngInput.value = ''; }
            show();
        }

        if (start) setPin(start);
        show();
        map.on('click', function (e) { setPin(e.latlng); });

        function find() {
            results.hidden = true;
            var text = search.value.trim();
            if (!text) {
                text = addressInputs.map(function (i) { return i.value; }).filter(Boolean).join(', ');
                search.value = text;
            }
            if (!text) return;
            var coords = parseCoordinates(text);
            if (coords) return setPin(coords, PIN_ZOOM);
            if (/maps\.app\.goo\.gl|goo\.gl\/maps/i.test(text)) {
                readout.textContent = 'Short Google links can\'t be read here. Open the link, then copy the coordinates (e.g. -1.2864, 36.8172) and paste them.';
                return;
            }
            readout.textContent = 'Searching...';
            fetch('https://nominatim.openstreetmap.org/search?format=json&limit=5&countrycodes=ke&q=' + encodeURIComponent(text), { headers: { 'Accept-Language': 'en' } })
                .then(function (r) { return r.json(); })
                .then(function (list) {
                    if (!list.length) { readout.textContent = 'Nothing found for "' + text + '". Try a nearby town, or click the map.'; return; }
                    if (list.length === 1) { map.setView([+list[0].lat, +list[0].lon], 15); show(); return; }
                    results.innerHTML = '';
                    list.forEach(function (r) {
                        var b = document.createElement('button');
                        b.type = 'button'; b.className = 'map-pin-result'; b.textContent = r.display_name;
                        b.addEventListener('click', function () { results.hidden = true; map.setView([+r.lat, +r.lon], 15); show(); });
                        results.appendChild(b);
                    });
                    results.hidden = false;
                    show();
                })
                .catch(function () { readout.textContent = 'Search isn\'t available right now. Zoom in and click the map instead.'; });
        }

        root.querySelector('[data-act="find"]').addEventListener('click', find);
        search.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); find(); } });
        search.addEventListener('paste', function () { setTimeout(function () { if (parseCoordinates(search.value)) find(); }, 0); });
        clearBtn.addEventListener('click', function () { if (marker) { map.removeLayer(marker); marker = null; } write(); });
        root.querySelector('[data-act="gps"]').addEventListener('click', function () {
            if (!navigator.geolocation) { readout.textContent = 'This browser can\'t share its location.'; return; }
            readout.textContent = 'Getting your location...';
            navigator.geolocation.getCurrentPosition(
                function (pos) { setPin([pos.coords.latitude, pos.coords.longitude], 17); },
                function () { readout.textContent = 'Location not available. Allow location access, or click the map instead.'; show(); },
                { enableHighAccuracy: true, timeout: 15000 });
        });
        // Forms that start hidden (tabs, <details>) need a resize once shown.
        setTimeout(function () { map.invalidateSize(); }, 200);
    }

    function initView(root) {
        var lat = parseFloat(root.dataset.lat), lng = parseFloat(root.dataset.lng);
        if (isNaN(lat) || isNaN(lng)) return;
        var map = L.map(root, { scrollWheelZoom: false, dragging: !L.Browser.mobile }).setView([lat, lng], 15);
        layers(map);
        var m = L.marker([lat, lng]).addTo(map);
        if (root.dataset.title) m.bindTooltip(root.dataset.title, { permanent: false, direction: 'top' });
    }

    function init() {
        var pickers = document.querySelectorAll('.map-pin-picker'), views = document.querySelectorAll('.map-pin-view');
        if (!pickers.length && !views.length) return;
        loadLeaflet(function () {
            pickers.forEach(initPicker);
            views.forEach(initView);
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
