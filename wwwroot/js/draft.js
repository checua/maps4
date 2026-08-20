(() => {
    let selectingDraftLocation = false;

    function addDraftMenuEntry() {
        const menu = document.getElementById('menu-flotante');
        if (!menu || document.getElementById('btnNuevoBorrador')) return;

        const button = document.createElement('a');
        button.id = 'btnNuevoBorrador';
        button.href = 'javascript:void(0);';
        button.className = 'btn btn-success';
        button.textContent = 'Nueva propiedad';
        button.addEventListener('click', startDraftCreation);

        menu.prepend(button);
    }

    async function isAuthenticated() {
        try {
            const response = await fetch('/Home/GetUserClaims', {
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) return false;
            const claims = await response.json();
            return Array.isArray(claims) && claims.length > 0;
        } catch (_) {
            return false;
        }
    }

    async function waitForMap(timeoutMs = 10000) {
        const started = Date.now();

        while ((!window.map || !window.google?.maps) && Date.now() - started < timeoutMs) {
            await new Promise(resolve => setTimeout(resolve, 100));
        }

        return window.map && window.google?.maps ? window.map : null;
    }

    async function startDraftCreation() {
        if (selectingDraftLocation) return;

        if (!(await isAuthenticated())) {
            await Swal.fire({
                icon: 'info',
                title: 'Inicia sesión',
                text: 'Necesitas iniciar sesión para crear una propiedad en tu inventario.',
                confirmButtonText: 'Entendido'
            });
            return;
        }

        const currentMap = await waitForMap();
        if (!currentMap) {
            await Swal.fire('Mapa no disponible', 'Espera a que el mapa termine de cargar e intenta nuevamente.', 'warning');
            return;
        }

        const menu = document.getElementById('menu-flotante');
        if (menu) menu.style.display = 'none';

        const instruction = await Swal.fire({
            icon: 'info',
            title: 'Nueva propiedad',
            html: 'Primero ubicaremos el inmueble.<br><strong>Toca una vez en el mapa</strong> donde se encuentra.',
            confirmButtonText: 'Seleccionar ubicación',
            showCancelButton: true,
            cancelButtonText: 'Cancelar'
        });

        if (!instruction.isConfirmed) return;

        selectingDraftLocation = true;
        document.body.style.cursor = 'crosshair';

        const listener = google.maps.event.addListenerOnce(currentMap, 'click', async event => {
            selectingDraftLocation = false;
            document.body.style.cursor = '';

            const lat = Number(event.latLng.lat().toFixed(6));
            const lng = Number(event.latLng.lng().toFixed(6));

            await chooseTypeAndCreate(lat, lng);
        });

        window.setTimeout(() => {
            if (!selectingDraftLocation) return;
            selectingDraftLocation = false;
            document.body.style.cursor = '';
            google.maps.event.removeListener(listener);
        }, 120000);
    }

    async function chooseTypeAndCreate(lat, lng) {
        let tipos;

        try {
            const response = await fetch('/Home/listaTipoPropiedades', {
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) throw new Error('No fue posible cargar los tipos de propiedad.');
            tipos = await response.json();
        } catch (error) {
            await Swal.fire('No se pudo continuar', error.message, 'error');
            return;
        }

        const options = {};
        (tipos || [])
            .filter(tipo => Number(tipo.idTipoPropiedad) > 1 && tipo.nombre)
            .forEach(tipo => {
                options[String(tipo.idTipoPropiedad)] = tipo.nombre;
            });

        if (Object.keys(options).length === 0) {
            await Swal.fire('Sin tipos disponibles', 'No hay tipos de propiedad disponibles para crear el borrador.', 'warning');
            return;
        }

        const selection = await Swal.fire({
            title: '¿Qué propiedad es?',
            html: `<div style="margin-bottom:8px;color:#667085;font-size:13px;">Ubicación: ${lat.toFixed(6)}, ${lng.toFixed(6)}</div>`,
            input: 'select',
            inputOptions: options,
            inputPlaceholder: 'Selecciona el tipo',
            showCancelButton: true,
            confirmButtonText: 'Crear borrador',
            cancelButtonText: 'Cancelar',
            inputValidator: value => value ? undefined : 'Selecciona un tipo de propiedad.'
        });

        if (!selection.isConfirmed) return;

        await createDraft(lat, lng, Number(selection.value));
    }

    async function createDraft(lat, lng, idTipo) {
        Swal.fire({
            title: 'Creando borrador…',
            allowOutsideClick: false,
            allowEscapeKey: false,
            didOpen: () => Swal.showLoading()
        });

        try {
            const response = await fetch('/Borrador/Crear', {
                method: 'POST',
                credentials: 'same-origin',
                redirect: 'follow',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ lat, lng, idTipo })
            });

            if (response.redirected && response.url.toLowerCase().includes('/inicio/iniciarsesion')) {
                throw new Error('Tu sesión terminó. Inicia sesión nuevamente.');
            }

            let data = null;
            try {
                data = await response.json();
            } catch (_) {
                // Mantener mensaje genérico cuando la respuesta no sea JSON.
            }

            if (!response.ok || !data?.success) {
                throw new Error(data?.message || 'No fue posible crear el borrador.');
            }

            const result = await Swal.fire({
                icon: 'success',
                title: `Borrador #${data.idInmueble} creado`,
                html: 'La ubicación y el tipo ya quedaron guardados.<br><strong>¿Quieres completar la propiedad ahora o continuar después?</strong><br><br>El borrador sigue privado y no aparece en el marketplace.',
                confirmButtonText: 'Completar ahora',
                showDenyButton: true,
                denyButtonText: 'Ir a Mi inventario',
                showCancelButton: true,
                cancelButtonText: 'Seguir en el mapa'
            });

            if (result.isConfirmed) {
                window.location.href = `/Borrador/Editar/${encodeURIComponent(data.idInmueble)}`;
            } else if (result.isDenied) {
                window.location.href = `/Inventario?borrador=${encodeURIComponent(data.idInmueble)}`;
            }
        } catch (error) {
            await Swal.fire('No se pudo crear', error.message, 'error');
        }
    }

    window.startDraftCreation = startDraftCreation;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', addDraftMenuEntry);
    } else {
        addDraftMenuEntry();
    }
})();
