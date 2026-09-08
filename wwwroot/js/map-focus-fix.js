(() => {
    const path = window.location.pathname.toLowerCase();
    if (path !== '/' && path !== '/home' && path !== '/home/index') return;

    const query = new URLSearchParams(window.location.search);
    const requestedInmuebleId = Number(query.get('inmuebleId') || 0);
    let explicitFocusMarker = null;
    let focusTimer = null;

    // El mapa debe iniciar sin filtros de precio implícitos. Históricamente
    // arrancaba en 2,500..1,000,000 y por eso propiedades publicadas como
    // #147/#187 podían existir en el viewport pero quedar invisibles.
    normalizeDefaultPriceFilters();

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
    // marcador explícito con el icono correcto que no depende de los filtros.
    const previousLoadInmueble = window.loadInmueble;
    if (typeof previousLoadInmueble === 'function') {
        window.loadInmueble = function (inmuebleId) {
            const result = previousLoadInmueble.apply(this, arguments);
            if (Number(inmuebleId) > 0) scheduleExplicitFocus(Number(inmuebleId));
            return result;
        };
    }

    // Sustituimos el comportamiento de "Acercar" para garantizar que siempre
    // exista un marcador visible, incluso si el marker normal está filtrado.
    window.gotoLocation = function () {
        if (typeof currentInmueble === 'undefined' || !currentInmueble) {
            Swal.fire('Error', 'No hay un inmueble seleccionado.', 'error');
            return;
        }

        const lat = Number(currentInmueble.lat);
        const lng = Number(currentInmueble.lng);
        const inmuebleId = Number(currentInmueble.id);
        const idTipo = resolveCurrentType();
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
                    resolveCurrentType(),
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

        if (explicitFocusMarker) explicitFocusMarker.setMap(null);

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

        explicitFocusMarker.setAnimation(google.maps.Animation.BOUNCE);
        window.setTimeout(() => explicitFocusMarker?.setAnimation(null), 1400);
    }

    function resolveCurrentType() {
        const fromForm = Number(document.getElementById('tipo')?.value || 0);
        if (Number.isFinite(fromForm) && fromForm > 0) return fromForm;

        if (typeof currentInmueble !== 'undefined' && currentInmueble) {
            const fromCurrent = Number(currentInmueble.idTipo || 0);
            if (Number.isFinite(fromCurrent) && fromCurrent > 0) return fromCurrent;
        }
        return 2;
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
