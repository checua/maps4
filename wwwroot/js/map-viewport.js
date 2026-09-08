(() => {
    const path = window.location.pathname.toLowerCase();
    if (path !== '/' && path !== '/home' && path !== '/home/index') return;

    const propertyCache = new Map();
    const activeMarkers = new Map();
    const loadedRegions = [];
    const preloadFactor = 0.35;
    const maxLoadedRegions = 24;

    let viewportListenerAttached = false;
    let viewportAbortController = null;
    let requestSequence = 0;
    let lastRequestSignature = '';
    let lastViewportWasTruncated = false;
    let focusMarker = null;
    let focusPropertyId = null;
    let focusTimer = null;

    const legacyLoadInmueble = window.loadInmueble;

    // index.js legacy llama fetchMarkers() una sola vez al crear el mapa.
    // Sustituimos esa carga global por un listener de viewport.
    window.fetchMarkers = function () {
        attachViewportListener();
        refreshViewport(false);
    };

    // Cuando Inventario abre /?inmuebleId=..., el flujo legacy abre la ficha.
    // Añadimos el comportamiento que faltaba: centrar, acercar y señalar el punto.
    if (typeof legacyLoadInmueble === 'function') {
        window.loadInmueble = function (inmuebleId) {
            const result = legacyLoadInmueble.apply(this, arguments);
            focusInmuebleWhenReady(inmuebleId);
            return result;
        };
    }

    // Corrige el flujo del botón de ubicación del modal. El código legacy buscaba
    // el inmueble en markers aunque los marcadores públicos vivían en markersx.
    window.gotoLocation = function () {
        if (typeof currentInmueble === 'undefined' || !currentInmueble) {
            Swal.fire('Error', 'No hay un inmueble seleccionado.', 'error');
            return;
        }

        const lat = Number(currentInmueble.lat);
        const lng = Number(currentInmueble.lng);
        const inmuebleId = Number(currentInmueble.id);
        const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
        const url = isIOS
            ? `maps://maps.apple.com/?q=${lat},${lng}`
            : `https://maps.google.com/?q=${lat},${lng}`;

        Swal.fire({
            title: 'Selecciona una opción',
            showDenyButton: true,
            showCancelButton: true,
            confirmButtonText: 'Acercar',
            denyButtonText: 'Abrir en Mapas',
            cancelButtonText: 'Cancelar'
        }).then((result) => {
            if (result.isConfirmed) {
                focusMapPosition(inmuebleId, lat, lng, true);
            } else if (result.isDenied) {
                window.open(url, '_blank');
            }
            $('#modalInmueble').modal('hide');
        });
    };

    function attachViewportListener() {
        if (viewportListenerAttached || typeof map === 'undefined' || !map) return;
        viewportListenerAttached = true;

        // "idle" evita consultar mientras el usuario todavía está arrastrando o
        // haciendo zoom. getBounds() ya representa el viewport final.
        map.addListener('idle', () => refreshViewport(false));
    }

    async function refreshViewport(force) {
        if (typeof map === 'undefined' || !map || typeof google === 'undefined' || !google.maps) return;

        const bounds = map.getBounds?.();
        if (!bounds) return;

        const visible = normalizeBounds(bounds);
        const zoom = Math.round(Number(map.getZoom?.() ?? 12));

        const cachedRegion = loadedRegions.find(region => !region.truncated && containsBounds(region, visible));
        if (!force && cachedRegion) {
            renderCachedProperties(expandBounds(visible, 0.18));
            emitViewportUpdate(false, zoom, true);
            return;
        }

        const requested = expandBounds(visible, preloadFactor);
        const signature = boundsSignature(requested, zoom);
        if (!force && signature === lastRequestSignature) {
            renderCachedProperties(requested);
            return;
        }
        lastRequestSignature = signature;

        viewportAbortController?.abort();
        viewportAbortController = new AbortController();
        const sequence = ++requestSequence;

        const params = new URLSearchParams({
            north: requested.north.toFixed(7),
            south: requested.south.toFixed(7),
            east: requested.east.toFixed(7),
            west: requested.west.toFixed(7),
            zoom: String(zoom)
        });

        try {
            const response = await fetch(`/Home/listaInmueblesViewport?${params.toString()}`, {
                credentials: 'same-origin',
                signal: viewportAbortController.signal
            });

            if (!response.ok) throw new Error(`Viewport HTTP ${response.status}`);
            const payload = await response.json();
            if (sequence !== requestSequence) return;

            const items = Array.isArray(payload.items) ? payload.items : [];
            items.forEach(item => {
                const id = Number(item.idInmueble);
                if (Number.isFinite(id)) propertyCache.set(id, item);
            });

            lastViewportWasTruncated = Boolean(payload.truncated);
            rememberRegion(requested, lastViewportWasTruncated);
            renderCachedProperties(requested);
            emitViewportUpdate(lastViewportWasTruncated, zoom, false, payload.maxResultados);
        } catch (error) {
            if (error?.name === 'AbortError') return;
            console.error('Error al cargar inmuebles del viewport:', error);
            emitViewportError();
        }
    }

    function renderCachedProperties(renderBounds) {
        const wantedIds = new Set();

        propertyCache.forEach((item, id) => {
            const lat = Number(item.lat);
            const lng = Number(item.lng);
            if (!Number.isFinite(lat) || !Number.isFinite(lng)) return;
            if (!containsPoint(renderBounds, lat, lng)) return;

            wantedIds.add(id);
            if (!activeMarkers.has(id)) {
                activeMarkers.set(id, createPropertyMarker(item));
            } else {
                const marker = activeMarkers.get(id);
                marker.rsmapsData = item;
                marker.customInfo = item.precio;
                marker.setMap(map);
            }
        });

        for (const [id, marker] of activeMarkers.entries()) {
            if (wantedIds.has(id)) continue;
            marker.setMap(null);
            activeMarkers.delete(id);
        }

        syncLegacyMarkerArray();
    }

    function createPropertyMarker(item) {
        const id = Number(item.idInmueble);
        const latLng = new google.maps.LatLng(Number(item.lat), Number(item.lng));
        const marker = new google.maps.Marker({
            position: latLng,
            map,
            title: String(item.idTipo ?? ''),
            icon: {
                url: iconForType(Number(item.idTipo)),
                scaledSize: new google.maps.Size(32, 32),
                origin: new google.maps.Point(0, 0),
                anchor: new google.maps.Point(16, 16)
            }
        });

        marker.customInfo = item.precio;
        marker.rsmapsData = item;
        marker.rsmapsInmuebleId = id;

        attachMarkerInteractions(marker, item);

        if (focusPropertyId === id) {
            removeFocusMarker();
            bounceMarker(marker);
        }

        return marker;
    }

    function attachMarkerInteractions(marker, item) {
        let longPressTimer = null;
        let isLongPress = false;
        const longPressDuration = 1000;

        const handleLongPress = () => {
            if (typeof mousedUp !== 'undefined' && mousedUp) return;
            isLongPress = true;
            copyPropertyToMemory(item);
        };

        const onPressStart = (event) => {
            isLongPress = false;
            if (typeof mousedUp !== 'undefined') mousedUp = false;
            longPressTimer = window.setTimeout(handleLongPress, longPressDuration);
            if (event?.type === 'touchstart') event.preventDefault?.();
        };

        const onPressEnd = () => {
            clearTimeout(longPressTimer);
            if (typeof mousedUp !== 'undefined') mousedUp = true;
            window.setTimeout(() => { isLongPress = false; }, longPressDuration);
        };

        const onPressMove = () => {
            clearTimeout(longPressTimer);
            isLongPress = false;
            if (typeof mousedUp !== 'undefined') mousedUp = true;
        };

        marker.addListener('click', () => {
            if (isLongPress || (typeof mousedUp !== 'undefined' && !mousedUp)) return;
            openPropertyItem(item);
        });
        marker.addListener('mousedown', onPressStart);
        marker.addListener('mouseup', onPressEnd);
        marker.addListener('mousemove', onPressMove);
        marker.addListener('touchstart', onPressStart);
        marker.addListener('touchend', onPressEnd);
        marker.addListener('touchmove', onPressMove);
    }

    function openPropertyItem(item) {
        $('#btnClear').css('display', 'none');
        $('.btn-fileupload').css('display', 'none');

        currentInmueble = {
            lat: item.lat,
            lng: item.lng,
            id: item.idInmueble
        };

        const logged = String(document.getElementById('lnkAcceso')?.innerText || '').toUpperCase();
        const owner = String(item.refUsuario?.correo || '').toUpperCase();

        if (logged !== owner) {
            $('.boton-eliminar-inmueble').css('display', 'none');
            $('#contacto_a').hide();
            $('.boton-guardar-inmueble').css('display', 'none');
        } else {
            $('#contacto_a').show();
            $('.boton-guardar-inmueble').css('display', 'inline');
            $('.boton-eliminar-inmueble').show();
            $('.boton-guardar-inmueble').text('Actualizar');
            $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, true)');
            if (typeof isUpdate !== 'undefined') isUpdate = true;
        }

        selectedInmuebleId = item.idInmueble;
        if (typeof clikeado !== 'undefined') clikeado = 1;
        const nomTel = `${item.refUsuario?.nombres || ''} ${item.refUsuario?.aPaterno || ''}`.trim();
        GetCode1(
            item.idTipo,
            item.idInmueble,
            nomTel,
            item.telefono,
            item.terreno,
            item.construccion,
            item.precio,
            item.observaciones,
            item.contacto,
            item.imagenes);
    }

    function copyPropertyToMemory(item) {
        if (typeof accumulatedIds === 'undefined' || typeof accumulatedUrls === 'undefined') return;

        const id = Number(item.idInmueble);
        if (!accumulatedIds.includes(id)) {
            accumulatedIds.push(id);
            accumulatedUrls.push(`${window.location.origin}/Share/${id}`);
        }
        if (typeof inmuebleDescriptions !== 'undefined') {
            inmuebleDescriptions[id] = item.observaciones || 'Descripción no disponible';
        }

        const text = accumulatedIds.map(propertyId => {
            const description = typeof inmuebleDescriptions !== 'undefined'
                ? (inmuebleDescriptions[propertyId] || 'Descripción no disponible')
                : 'Descripción no disponible';
            return `${description}\n${window.location.origin}/Share/${propertyId}`;
        }).join('\n\n');

        const finish = () => Swal.fire({
            title: `Copiado a memoria: Inmueble #${id}`,
            text: `Inmuebles copiados: ${accumulatedIds.join(', ')}`,
            icon: 'success',
            confirmButtonText: 'Ok',
            showCancelButton: true,
            cancelButtonText: 'Vaciar memoria'
        }).then(result => {
            if (result.dismiss === Swal.DismissReason.cancel) {
                accumulatedIds.length = 0;
                accumulatedUrls.length = 0;
                if (typeof inmuebleDescriptions !== 'undefined') {
                    Object.keys(inmuebleDescriptions).forEach(key => delete inmuebleDescriptions[key]);
                }
            }
        });

        if (navigator.clipboard?.writeText) {
            navigator.clipboard.writeText(text).then(finish).catch(error => console.error('Error al copiar inmueble:', error));
        } else {
            const textarea = document.createElement('textarea');
            textarea.value = text;
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand('copy');
            textarea.remove();
            finish();
        }
    }

    function syncLegacyMarkerArray() {
        if (typeof markersx === 'undefined' || !Array.isArray(markersx)) return;
        markersx.length = 0;
        activeMarkers.forEach(marker => markersx.push(marker));
    }

    function focusInmuebleWhenReady(inmuebleId) {
        focusPropertyId = Number(inmuebleId);
        clearInterval(focusTimer);
        let attempts = 0;

        focusTimer = window.setInterval(() => {
            attempts++;
            const ready =
                typeof map !== 'undefined' && map &&
                typeof currentInmueble !== 'undefined' && currentInmueble &&
                Number(currentInmueble.id) === focusPropertyId;

            if (ready) {
                clearInterval(focusTimer);
                focusMapPosition(
                    focusPropertyId,
                    Number(currentInmueble.lat),
                    Number(currentInmueble.lng),
                    true);
            } else if (attempts >= 100) {
                clearInterval(focusTimer);
            }
        }, 100);
    }

    function focusMapPosition(inmuebleId, lat, lng, zoomIn) {
        if (!Number.isFinite(lat) || !Number.isFinite(lng) || typeof map === 'undefined' || !map) return;
        focusPropertyId = Number(inmuebleId);
        const position = new google.maps.LatLng(lat, lng);
        map.setCenter(position);
        if (zoomIn && Number(map.getZoom?.() || 0) < 17) map.setZoom(17);

        const existing = activeMarkers.get(focusPropertyId);
        if (existing) {
            removeFocusMarker();
            bounceMarker(existing);
            return;
        }

        removeFocusMarker();
        focusMarker = new google.maps.Marker({
            position,
            map,
            title: `Inmueble #${focusPropertyId}`,
            animation: google.maps.Animation.DROP,
            zIndex: 999999
        });
        bounceMarker(focusMarker);
    }

    function bounceMarker(marker) {
        marker.setAnimation?.(google.maps.Animation.BOUNCE);
        window.setTimeout(() => marker.setAnimation?.(null), 1400);
    }

    function removeFocusMarker() {
        if (!focusMarker) return;
        focusMarker.setMap(null);
        focusMarker = null;
    }

    function rememberRegion(bounds, truncated) {
        loadedRegions.unshift({ ...bounds, truncated, loadedAt: Date.now() });
        if (loadedRegions.length > maxLoadedRegions) loadedRegions.length = maxLoadedRegions;
    }

    function normalizeBounds(bounds) {
        const ne = bounds.getNorthEast();
        const sw = bounds.getSouthWest();
        return {
            north: clamp(ne.lat(), -90, 90),
            south: clamp(sw.lat(), -90, 90),
            east: normalizeLng(ne.lng()),
            west: normalizeLng(sw.lng())
        };
    }

    function expandBounds(bounds, factor) {
        const latSpan = Math.max(0.0001, bounds.north - bounds.south);
        let lngSpan = longitudeSpan(bounds.west, bounds.east);
        lngSpan = Math.max(0.0001, lngSpan);

        return {
            north: clamp(bounds.north + latSpan * factor, -90, 90),
            south: clamp(bounds.south - latSpan * factor, -90, 90),
            west: normalizeLng(bounds.west - lngSpan * factor),
            east: normalizeLng(bounds.east + lngSpan * factor)
        };
    }

    function containsBounds(container, inner) {
        const northSouth = container.north >= inner.north && container.south <= inner.south;
        if (!northSouth) return false;

        if (container.west <= container.east && inner.west <= inner.east) {
            return container.west <= inner.west && container.east >= inner.east;
        }

        // Para viewports que crucen el antimeridiano usamos las cuatro esquinas.
        return containsPoint(container, inner.north, inner.west)
            && containsPoint(container, inner.north, inner.east)
            && containsPoint(container, inner.south, inner.west)
            && containsPoint(container, inner.south, inner.east);
    }

    function containsPoint(bounds, lat, lng) {
        if (lat < bounds.south || lat > bounds.north) return false;
        const normalized = normalizeLng(lng);
        return bounds.west <= bounds.east
            ? normalized >= bounds.west && normalized <= bounds.east
            : normalized >= bounds.west || normalized <= bounds.east;
    }

    function longitudeSpan(west, east) {
        return west <= east ? east - west : (180 - west) + (east + 180);
    }

    function boundsSignature(bounds, zoom) {
        return [zoom, bounds.north, bounds.south, bounds.east, bounds.west]
            .map((value, index) => index === 0 ? String(value) : Number(value).toFixed(4))
            .join('|');
    }

    function normalizeLng(value) {
        let lng = Number(value);
        while (lng > 180) lng -= 360;
        while (lng < -180) lng += 360;
        return lng;
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, Number(value)));
    }

    function iconForType(type) {
        switch (type) {
            case 2: return '/images/icon/casa_che.png';
            case 3: return '/images/icon/casa.png';
            case 4: return '/images/icon/apartment1.png';
            case 5: return '/images/icon/apartment2.png';
            case 6: return '/images/icon/terreno1b.png';
            case 7: return '/images/icon/terreno2.png';
            case 8: return '/images/icon/local.png';
            case 9: return '/images/icon/local2.png';
            case 10: return '/images/icon/building.png';
            case 11: return '/images/icon/building2.png';
            case 12: return '/images/icon/montacargas2.png';
            case 13: return '/images/icon/montacargas.png';
            case 14: return '/images/icon/desk.png';
            case 15: return '/images/icon/desk2.png';
            case 16: return '/images/icon/tractor1.png';
            case 17: return '/images/icon/tractor2.png';
            default: return '/images/icon/casa_che.png';
        }
    }

    function emitViewportUpdate(truncated, zoom, fromCache, maxResultados) {
        window.dispatchEvent(new CustomEvent('rsmaps:viewport-markers-updated', {
            detail: {
                activeMarkers: activeMarkers.size,
                cachedProperties: propertyCache.size,
                truncated: Boolean(truncated),
                zoom,
                fromCache: Boolean(fromCache),
                maxResultados: maxResultados ?? null
            }
        }));
    }

    function emitViewportError() {
        window.dispatchEvent(new CustomEvent('rsmaps:viewport-markers-error'));
    }

    window.rsmapsViewport = {
        refresh: () => refreshViewport(true),
        getStats: () => ({
            activeMarkers: activeMarkers.size,
            cachedProperties: propertyCache.size,
            loadedRegions: loadedRegions.length,
            truncated: lastViewportWasTruncated
        })
    };
})();
