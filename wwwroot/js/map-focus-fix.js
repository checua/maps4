(() => {
    const path = window.location.pathname.toLowerCase();
    if (path !== '/' && path !== '/home' && path !== '/home/index') return;

    const query = new URLSearchParams(window.location.search);
    const requestedInmuebleId = Number(query.get('inmuebleId') || 0);
    let explicitFocusMarker = null;
    let focusTimer = null;
    let handoffTimer = null;
    let focusedProperty = null;

    // Mientras las fotos legacy terminan de migrarse a Azure Blob Storage,
    // localhost puede reutilizar las fotos que ya existen en la Web App.
    // Esto evita copiar /Cargas al repositorio y mantiene Git dedicado a código.
    installLegacyImageFallback();

    // El mapa debe iniciar sin filtros de precio implícitos. Históricamente
    // arrancaba en 2,500..1,000,000 y por eso propiedades publicadas como
    // #147/#187 podían existir en el viewport pero quedar invisibles.
    normalizeDefaultPriceFilters();

    // Registrar el destino desde el principio para que cualquier lógica legacy
    // sepa que la navegación explícita al inmueble tiene prioridad.
    if (requestedInmuebleId > 0 && typeof selectedInmuebleId !== 'undefined') {
        selectedInmuebleId = requestedInmuebleId;
    }

    // Cargar una copia mínima del inmueble enfocado. El flujo legacy guarda
    // lat/lng/id en currentInmueble, pero no siempre conserva idTipo; sin idTipo
    // un marker temporal puede caer en el pin genérico o en un icono incorrecto.
    if (requestedInmuebleId > 0) {
        loadFocusedProperty(requestedInmuebleId);
    }

    // Si venimos de Inventario con ?inmuebleId=..., conservar la geolocalización
    // del usuario como punto informativo, pero evitar que su callback cambie el
    // centro después de que ya enfocamos el inmueble solicitado.
    if (requestedInmuebleId > 0 && navigator.geolocation?.getCurrentPosition) {
        const originalGetCurrentPosition = navigator.geolocation.getCurrentPosition.bind(navigator.geolocation);
        navigator.geolocation.getCurrentPosition = function (success, error, options) {
            return originalGetCurrentPosition(function (position) {
                const currentMap = (typeof map !== 'undefined' && map) ? map : null;
                if (!currentMap) {
                    success?.(position);
                    return;
                }

                const originalSetCenter = currentMap.setCenter;
                currentMap.setCenter = function () { };
                try {
                    success?.(position);
                } finally {
                    currentMap.setCenter = originalSetCenter;
                }
            }, error, options);
        };
    }

    // map-viewport.js ya envuelve loadInmueble. Esta segunda envoltura añade un
    // marcador explícito con el icono correcto que no depende de los filtros ni
    // de que la propiedad sea parte del marketplace público.
    const previousLoadInmueble = window.loadInmueble;
    if (typeof previousLoadInmueble === 'function') {
        window.loadInmueble = function (inmuebleId) {
            const numericId = Number(inmuebleId);
            if (numericId > 0 && typeof selectedInmuebleId !== 'undefined') {
                selectedInmuebleId = numericId;
            }

            const result = previousLoadInmueble.apply(this, arguments);

            if (numericId > 0) {
                loadFocusedProperty(numericId);
                scheduleExplicitFocus(numericId);
            }
            return result;
        };
    }

    // Sustituimos el comportamiento de "Acercar" para garantizar que siempre
    // exista un marcador visible, incluso si el marker normal todavía no está
    // cargado o si el inmueble no pertenece al marketplace público.
    window.gotoLocation = function () {
        if (typeof currentInmueble === 'undefined' || !currentInmueble) {
            Swal.fire('Error', 'No hay un inmueble seleccionado.', 'error');
            return;
        }

        const lat = Number(currentInmueble.lat);
        const lng = Number(currentInmueble.lng);
        const inmuebleId = Number(currentInmueble.id);
        const idTipo = resolveCurrentType(inmuebleId);
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
        }).then(result => {
            if (result.isConfirmed) {
                showExplicitFocus(inmuebleId, lat, lng, idTipo, true);
            } else if (result.isDenied) {
                window.open(url, '_blank');
            }
            $('#modalInmueble').modal('hide');
        });
    };

    if (requestedInmuebleId > 0) {
        scheduleExplicitFocus(requestedInmuebleId);
    }

    function installLegacyImageFallback() {
        const localHosts = new Set(['localhost', '127.0.0.1', '::1']);
        if (!localHosts.has(window.location.hostname.toLowerCase())) return;

        const publicAssetBaseUrl = 'https://rsmap.azurewebsites.net';

        // Los errores de IMG no hacen bubble; el listener en captura permite
        // cubrir también miniaturas agregadas dinámicamente por index.js.
        document.addEventListener('error', event => {
            const image = event.target;
            if (!(image instanceof HTMLImageElement)) return;
            if (image.dataset.rsmapsAzureFallbackTried === 'true') return;

            const rawSource = image.getAttribute('src') || '';
            if (!rawSource) return;

            let sourceUrl;
            try {
                sourceUrl = new URL(rawSource, window.location.origin);
            } catch {
                return;
            }

            if (!/^\/cargas\//i.test(sourceUrl.pathname)) return;

            image.dataset.rsmapsAzureFallbackTried = 'true';
            image.src = `${publicAssetBaseUrl}${sourceUrl.pathname}${sourceUrl.search}${sourceUrl.hash}`;
        }, true);
    }

    function normalizeDefaultPriceFilters() {
        const minSelect = document.getElementById('ddlViewBy');
        if (minSelect) {
            let zero = minSelect.querySelector('option[value="0"]');
            if (!zero) {
                zero = document.createElement('option');
                zero.value = '0';
                zero.textContent = 'Sin mínimo';
                minSelect.insertBefore(zero, minSelect.firstChild);
            }
            minSelect.value = '0';
        }

        const maxSelect = document.getElementById('ddlViewBy2');
        if (maxSelect) {
            const unlimited = maxSelect.querySelector('option[value="1000000000"]');
            if (unlimited) unlimited.textContent = 'Sin máximo';
            maxSelect.value = '1000000000';
        }
    }

    async function loadFocusedProperty(inmuebleId) {
        try {
            const response = await fetch(`/Inmueble/GetInmuebleById?id=${encodeURIComponent(inmuebleId)}`, {
                credentials: 'same-origin'
            });
            if (!response.ok) return;

            const payload = await response.json();
            const item = Array.isArray(payload) ? payload[0] : payload;
            if (!item || Number(item.idInmueble) !== Number(inmuebleId)) return;

            focusedProperty = item;

            if (typeof currentInmueble !== 'undefined' &&
                currentInmueble &&
                Number(currentInmueble.id) === Number(inmuebleId)) {
                currentInmueble.idTipo = item.idTipo;
            }
        } catch (error) {
            console.warn('No fue posible completar los datos del inmueble enfocado:', error);
        }
    }

    function scheduleExplicitFocus(inmuebleId) {
        clearInterval(focusTimer);
        let attempts = 0;

        focusTimer = window.setInterval(() => {
            attempts++;
            const ready =
                typeof map !== 'undefined' && map &&
                typeof google !== 'undefined' && google.maps &&
                typeof currentInmueble !== 'undefined' && currentInmueble &&
                Number(currentInmueble.id) === Number(inmuebleId);

            if (ready) {
                clearInterval(focusTimer);
                showExplicitFocus(
                    Number(inmuebleId),
                    Number(currentInmueble.lat),
                    Number(currentInmueble.lng),
                    resolveCurrentType(inmuebleId),
                    true);
            } else if (attempts >= 120) {
                clearInterval(focusTimer);
            }
        }, 100);
    }

    function showExplicitFocus(inmuebleId, lat, lng, idTipo, zoomIn) {
        if (!Number.isFinite(lat) || !Number.isFinite(lng)) return;
        if (typeof map === 'undefined' || !map || typeof google === 'undefined' || !google.maps) return;

        const position = new google.maps.LatLng(lat, lng);
        map.setCenter(position);
        if (zoomIn) map.setZoom(17);

        // Si el viewport ya creó el marker real, usarlo y no superponer otro.
        const viewportMarker = findViewportMarker(inmuebleId, lat, lng);
        if (viewportMarker) {
            removeExplicitFocusMarker();
            bounceMarker(viewportMarker);
            return;
        }

        removeExplicitFocusMarker();

        explicitFocusMarker = new google.maps.Marker({
            position,
            map,
            title: `Inmueble #${inmuebleId}`,
            icon: {
                url: iconForType(idTipo),
                scaledSize: new google.maps.Size(36, 36),
                origin: new google.maps.Point(0, 0),
                anchor: new google.maps.Point(18, 18)
            },
            zIndex: 1000000
        });

        bounceMarker(explicitFocusMarker);

        // Si el inmueble es público, el cambio de centro dispara la consulta del
        // viewport. Cuando aparezca su marker real, retirar el marker temporal.
        scheduleViewportMarkerHandoff(inmuebleId, lat, lng);
    }

    function scheduleViewportMarkerHandoff(inmuebleId, lat, lng) {
        clearInterval(handoffTimer);
        let attempts = 0;

        handoffTimer = window.setInterval(() => {
            attempts++;
            const viewportMarker = findViewportMarker(inmuebleId, lat, lng);
            if (viewportMarker) {
                clearInterval(handoffTimer);
                removeExplicitFocusMarker();
                bounceMarker(viewportMarker);
            } else if (attempts >= 80) {
                clearInterval(handoffTimer);
            }
        }, 100);
    }

    function findViewportMarker(inmuebleId, lat, lng) {
        if (typeof markersx === 'undefined' || !Array.isArray(markersx)) return null;

        const numericId = Number(inmuebleId);
        const tolerance = 0.0000005;

        return markersx.find(marker => {
            if (!marker || typeof marker.getPosition !== 'function') return false;

            const markerId = Number(
                marker.rsmapsInmuebleId ??
                marker.inmuebleId ??
                marker.rsmapsData?.idInmueble ??
                0);

            if (markerId > 0 && markerId === numericId) return true;

            const position = marker.getPosition();
            if (!position) return false;

            return Math.abs(Number(position.lat()) - Number(lat)) <= tolerance &&
                Math.abs(Number(position.lng()) - Number(lng)) <= tolerance;
        }) || null;
    }

    function resolveCurrentType(inmuebleId) {
        if (focusedProperty &&
            Number(focusedProperty.idInmueble) === Number(inmuebleId)) {
            const fromFocused = Number(focusedProperty.idTipo || 0);
            if (Number.isFinite(fromFocused) && fromFocused > 0) return fromFocused;
        }

        const fromForm = Number(
            document.getElementById('tipo')?.value ||
            document.getElementById('cboTipoPropiedad2')?.value ||
            0);
        if (Number.isFinite(fromForm) && fromForm > 0) return fromForm;

        if (typeof currentInmueble !== 'undefined' && currentInmueble) {
            const fromCurrent = Number(currentInmueble.idTipo || 0);
            if (Number.isFinite(fromCurrent) && fromCurrent > 0) return fromCurrent;
        }

        const viewportMarker = findViewportMarker(
            inmuebleId,
            Number(currentInmueble?.lat),
            Number(currentInmueble?.lng));
        const fromViewport = Number(
            viewportMarker?.rsmapsData?.idTipo ||
            viewportMarker?.getTitle?.() ||
            0);
        if (Number.isFinite(fromViewport) && fromViewport > 0) return fromViewport;

        return 2;
    }

    function removeExplicitFocusMarker() {
        if (!explicitFocusMarker) return;
        explicitFocusMarker.setMap(null);
        explicitFocusMarker = null;
    }

    function bounceMarker(marker) {
        marker?.setAnimation?.(google.maps.Animation.BOUNCE);
        window.setTimeout(() => marker?.setAnimation?.(null), 1400);
    }

    function iconForType(type) {
        switch (Number(type)) {
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
})();
