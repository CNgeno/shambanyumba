/*
 * Property map pin picker, on property forms:
 *   <div class="map-pin-picker" data-lat-input="Latitude" data-lng-input="Longitude" data-address-inputs="Location,County"
 *        data-google-key="(optional Maps JavaScript API key)" data-resolve-url="/Properties/ResolveMapLink"></div>
 *
 * With a Google key: Google's own map (click / drag to pin, Google search, satellite).
 * Without one: an OpenStreetMap map to click on, plus a live Google Maps preview of the pin, and the Google route:
 * find the place in Google Maps, drop a pin, Share > Copy link, paste here (short maps.app.goo.gl links work too).
 * Either way: "My location" uses the phone's GPS, and coordinates or Google links can be pasted in the search box.
 *
 * Pages that only show a pin render a Google Maps iframe on the server (MapPin.EmbedUrl): no script needed.
 * Kept identical in RealEstateERP.Web and RealEstateERP.Booking.Web (wwwroot/js/map-pin.js).
 */
(function () {
    'use strict';
    var LEAFLET = 'https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/';
    var KENYA = { lat: 0.2, lng: 37.9 }, KENYA_ZOOM = 6, PIN_ZOOM = 17;

    function fix(n) { return Math.round(n * 1e6) / 1e6; }
    function text6(n) { return fix(n).toFixed(6); }

    /** Plain "lat, lng" only; links and degrees go to the server, which knows every Google format. */
    function parsePlain(t) {
        var m = (t || '').trim().match(/^(-?\d{1,2}(?:\.\d+)?)\s*[, ]\s*(-?\d{1,3}(?:\.\d+)?)$/);
        if (!m) return null;
        var lat = parseFloat(m[1]), lng = parseFloat(m[2]);
        return Math.abs(lat) <= 90 && Math.abs(lng) <= 180 ? { lat: lat, lng: lng } : null;
    }
    function looksLikeLink(t) { return /^https?:\/\//i.test(t) || /°/.test(t); }

    function loadScript(src, done, fail) {
        var s = document.createElement('script');
        s.src = src; s.async = true; s.onload = done; s.onerror = fail;
        document.head.appendChild(s);
    }

    // ---------- Map engines: { setMarker(p, zoom), clearMarker(), center(p, zoom), resize() } ----------

    function googleEngine(canvas, start, onPick, ready, fail) {
        var key = canvas.parentNode.dataset.googleKey;
        window.__mapPinGoogleReady = window.__mapPinGoogleReady || [];
        var go = function () {
            var map = new google.maps.Map(canvas, {
                center: start || KENYA, zoom: start ? PIN_ZOOM : KENYA_ZOOM,
                mapTypeControl: true, streetViewControl: false, fullscreenControl: true, gestureHandling: 'cooperative'
            });
            var marker = null, geocoder = new google.maps.Geocoder();
            function setMarker(p, zoom) {
                if (!marker) {
                    marker = new google.maps.Marker({ position: p, map: map, draggable: true });
                    marker.addListener('dragend', function () { var q = marker.getPosition(); onPick({ lat: q.lat(), lng: q.lng() }); });
                } else marker.setPosition(p);
                if (zoom) { map.setCenter(p); if (map.getZoom() < zoom) map.setZoom(zoom); }
            }
            map.addListener('click', function (e) { var p = { lat: e.latLng.lat(), lng: e.latLng.lng() }; setMarker(p); onPick(p); });
            ready({
                setMarker: setMarker,
                clearMarker: function () { if (marker) { marker.setMap(null); marker = null; } },
                center: function (p, zoom) { map.setCenter(p); map.setZoom(zoom); },
                resize: function () { },
                search: function (q, done) {
                    geocoder.geocode({ address: q, componentRestrictions: { country: 'KE' } }, function (results, status) {
                        if (status !== 'OK' || !results.length) return done(status === 'ZERO_RESULTS' ? [] : null);
                        done(results.slice(0, 5).map(function (r) {
                            var vp = r.geometry.viewport;
                            return { name: r.formatted_address, lat: r.geometry.location.lat(), lng: r.geometry.location.lng(), bounds: vp };
                        }));
                    });
                },
                show: function (r) { if (r.bounds) map.fitBounds(r.bounds); else this.center(r, 15); }
            });
        };
        if (window.google && google.maps && google.maps.Map) return go();
        window.__mapPinGoogleReady.push(go);
        if (window.__mapPinGoogleLoading) return;
        window.__mapPinGoogleLoading = true;
        window.__mapPinGoogle = function () { window.__mapPinGoogleReady.forEach(function (f) { f(); }); };
        window.gm_authFailure = fail;   // bad key or API not enabled
        loadScript('https://maps.googleapis.com/maps/api/js?key=' + encodeURIComponent(key) + '&callback=__mapPinGoogle&loading=async', null, fail);
    }

    function leafletEngine(canvas, start, onPick, ready, fail) {
        var go = function () {
            var map = L.map(canvas, { scrollWheelZoom: false }).setView(start || KENYA, start ? PIN_ZOOM : KENYA_ZOOM);
            var street = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            }).addTo(map);
            var satellite = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
                maxZoom: 19, attribution: 'Imagery &copy; Esri'
            });
            L.control.layers({ 'Map': street, 'Satellite': satellite }, null, { position: 'topright' }).addTo(map);
            var marker = null;
            function setMarker(p, zoom) {
                if (!marker) {
                    marker = L.marker(p, { draggable: true, autoPan: true }).addTo(map);
                    marker.on('dragend', function () { var q = marker.getLatLng(); onPick({ lat: q.lat, lng: q.lng }); });
                } else marker.setLatLng(p);
                if (zoom) map.setView(p, Math.max(map.getZoom(), zoom));
            }
            map.on('click', function (e) { var p = { lat: e.latlng.lat, lng: e.latlng.lng }; setMarker(p); onPick(p); });
            setTimeout(function () { map.invalidateSize(); }, 200);
            ready({
                setMarker: setMarker,
                clearMarker: function () { if (marker) { map.removeLayer(marker); marker = null; } },
                center: function (p, zoom) { map.setView(p, zoom); },
                search: function (q, done) {
                    fetch('https://nominatim.openstreetmap.org/search?format=json&limit=5&countrycodes=ke&q=' + encodeURIComponent(q), { headers: { 'Accept-Language': 'en' } })
                        .then(function (r) { return r.json(); })
                        .then(function (list) { done(list.map(function (r) { return { name: r.display_name, lat: +r.lat, lng: +r.lon }; })); })
                        .catch(function () { done(null); });
                },
                show: function (r) { map.setView([r.lat, r.lng], 16); }
            });
        };
        if (window.L) return go();
        var css = document.createElement('link');
        css.rel = 'stylesheet'; css.href = LEAFLET + 'leaflet.css';
        document.head.appendChild(css);
        loadScript(LEAFLET + 'leaflet.js', go, fail);
    }

    // ---------- The picker ----------

    function initPicker(root) {
        var latInput = document.getElementById(root.dataset.latInput);
        var lngInput = document.getElementById(root.dataset.lngInput);
        if (!latInput || !lngInput) return;
        var addressInputs = (root.dataset.addressInputs || '').split(',').map(function (id) { return document.getElementById(id.trim()); }).filter(Boolean);
        var useGoogle = !!root.dataset.googleKey;

        root.innerHTML =
            '<div class="map-pin-tools">' +
            '  <input type="search" class="map-pin-search" placeholder="Search a place, or paste a Google Maps link or coordinates" aria-label="Search the map" />' +
            '  <button type="button" class="map-pin-btn" data-act="find">Find</button>' +
            '  <button type="button" class="map-pin-btn" data-act="gps" title="Use this phone\'s location (stand at the property)">&#x1F4CD; My location</button>' +
            (useGoogle ? '' : '  <a class="map-pin-btn" data-act="google" target="_blank" rel="noopener" href="https://www.google.com/maps">Find it in Google Maps &#x2197;</a>') +
            '</div>' +
            (useGoogle ? '' : '<p class="map-pin-help">Most accurate: open <strong>Google Maps</strong>, press and hold on the property to drop a pin, tap <strong>Share</strong> &rsaquo; <strong>Copy link</strong>, then paste the link above. Or click the map below.</p>') +
            '<div class="map-pin-results" hidden></div>' +
            '<div class="map-pin-canvas"></div>' +
            '<div class="map-pin-status"><span class="map-pin-readout"></span> <button type="button" class="map-pin-link" data-act="clear">Remove pin</button></div>' +
            (useGoogle ? '' : '<div class="map-pin-preview" hidden><div class="map-pin-preview-label">How it shows on Google Maps</div><iframe loading="lazy" referrerpolicy="no-referrer-when-downgrade" title="Pin on Google Maps"></iframe></div>');

        var search = root.querySelector('.map-pin-search');
        var readout = root.querySelector('.map-pin-readout');
        var clearBtn = root.querySelector('[data-act="clear"]');
        var results = root.querySelector('.map-pin-results');
        var googleLink = root.querySelector('[data-act="google"]');
        var preview = root.querySelector('.map-pin-preview');
        var previewTimer = null;

        var start = latInput.value && lngInput.value ? { lat: parseFloat(latInput.value), lng: parseFloat(lngInput.value) } : null;
        var engine = null, pin = start;

        function address() { return addressInputs.map(function (i) { return i.value; }).filter(Boolean).join(', '); }
        function show(message) {
            if (message) readout.textContent = message;
            else readout.textContent = pin ? 'Pinned at ' + text6(pin.lat) + ', ' + text6(pin.lng) + '. Drag the pin to adjust.' : 'No pin yet. Click the map where the property is.';
            clearBtn.hidden = !pin;
            if (googleLink) {
                googleLink.href = pin ? 'https://www.google.com/maps/search/?api=1&query=' + text6(pin.lat) + ',' + text6(pin.lng)
                    : 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(address() || 'Kenya');
            }
            if (preview) {
                clearTimeout(previewTimer);
                if (!pin) { preview.hidden = true; return; }
                previewTimer = setTimeout(function () {
                    preview.hidden = false;
                    preview.querySelector('iframe').src = 'https://maps.google.com/maps?q=' + text6(pin.lat) + ',' + text6(pin.lng) + '&z=17&output=embed';
                }, 400);
            }
        }
        function picked(p, zoom) {
            pin = { lat: fix(p.lat), lng: fix(p.lng) };
            latInput.value = text6(pin.lat); lngInput.value = text6(pin.lng);
            if (engine && zoom) engine.setMarker(pin, zoom);
            show();
        }
        function clear() {
            pin = null; latInput.value = ''; lngInput.value = '';
            if (engine) engine.clearMarker();
            show();
        }

        function resolveLink(t) {
            var url = root.dataset.resolveUrl;
            if (!url) { show('Paste the coordinates (e.g. -1.286400, 36.817200) instead.'); return; }
            show('Reading the link...');
            fetch(url + (url.indexOf('?') < 0 ? '?' : '&') + 'url=' + encodeURIComponent(t), { credentials: 'same-origin' })
                .then(function (r) { return r.json(); })
                .then(function (j) {
                    if (typeof j.lat === 'number') { picked(j, PIN_ZOOM); search.value = ''; }
                    else show('No location in that link. In Google Maps, drop a pin on the property first, then Share › Copy link.');
                })
                .catch(function () { show('Couldn\'t read that link right now. Paste the coordinates instead.'); });
        }
        function find() {
            results.hidden = true;
            var t = search.value.trim();
            if (!t) { t = address(); search.value = t; }
            if (!t) return;
            var plain = parsePlain(t);
            if (plain) return picked(plain, PIN_ZOOM);
            if (looksLikeLink(t)) return resolveLink(t);
            if (!engine) return;
            show('Searching...');
            engine.search(t, function (list) {
                if (list === null) return show('Search isn\'t available right now. Zoom in and click the map instead.');
                if (!list.length) return show('Nothing found for "' + t + '". Try a nearby town, or use Google Maps and paste the link.');
                if (list.length === 1) { engine.show(list[0]); return show('Now click the exact spot to drop the pin.'); }
                results.innerHTML = '';
                list.forEach(function (r) {
                    var b = document.createElement('button');
                    b.type = 'button'; b.className = 'map-pin-result'; b.textContent = r.name;
                    b.addEventListener('click', function () { results.hidden = true; engine.show(r); show('Now click the exact spot to drop the pin.'); });
                    results.appendChild(b);
                });
                results.hidden = false;
                show('Pick the right place, then click the exact spot.');
            });
        }

        root.querySelector('[data-act="find"]').addEventListener('click', find);
        search.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); find(); } });
        search.addEventListener('paste', function () { setTimeout(function () { var t = search.value.trim(); if (parsePlain(t) || looksLikeLink(t)) find(); }, 0); });
        clearBtn.addEventListener('click', clear);
        addressInputs.forEach(function (i) { i.addEventListener('change', function () { show(); }); });
        root.querySelector('[data-act="gps"]').addEventListener('click', function () {
            if (!navigator.geolocation) return show('This browser can\'t share its location.');
            show('Getting your location...');
            navigator.geolocation.getCurrentPosition(
                function (pos) { picked({ lat: pos.coords.latitude, lng: pos.coords.longitude }, PIN_ZOOM); },
                function () { show('Location not available. Allow location access, or click the map instead.'); },
                { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
        });

        var canvas = root.querySelector('.map-pin-canvas');
        function ready(e) { engine = e; if (pin) engine.setMarker(pin); show(); }
        function onPick(p) { picked(p); }
        show();
        if (useGoogle) {
            googleEngine(canvas, start, onPick, ready, function () {
                // Key rejected or the API isn't enabled: fall back to the free map so the form still works.
                canvas.innerHTML = '';
                root.dataset.googleKey = '';
                initPicker(root);
            });
        } else {
            leafletEngine(canvas, start, onPick, ready, function () {
                canvas.innerHTML = '<p class="map-pin-error">The map could not load. You can still paste a Google Maps link or coordinates above.</p>';
            });
        }
    }

    function init() { document.querySelectorAll('.map-pin-picker').forEach(initPicker); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
