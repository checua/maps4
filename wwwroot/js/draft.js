(() => {
    let selectingDraftLocation = false;
    let locationListener = null;
    let locationTimeout = null;

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

    function removeLocationBanner() {
        document.getElementById('draftLocationBanner')?.remove();
    }

    function cancelLocationSelection() {
        selectingDraftLocation = false;
        document.body.style.cursor = '';
        removeLocationBanner();

        if (locationListener) {
            google.maps.event.removeListener(locationListener);
            locationListener = null;
        }

        if (locationTimeout) {
            clearTimeout(locationTimeout);
            locationTimeout = null;
        }
    }

    function showLocationBanner() {
        removeLocationBanner();

        const banner = document.createElement('div');
        banner.id = 'draftLocationBanner';
        banner.style.cssText = [
            'position:fixed',
            'left:50%',
            'bottom:24px',
            'transform:translateX(-50%)',
            'z-index:10050',
            'display:flex',
            'align-items:center',
            'gap:12px',
            'max-width:calc(100vw - 28px)',
            'padding:11px 14px',
            'border-radius:14px',
            'background:#17212b',
            'color:#fff',
            'box-shadow:0 12px 30px rgba(0,0,0,.24)',
            'font-size:14px'
        ].join(';');

        const text = document.createElement('span');
        text.innerHTML = '<strong>Nueva propiedad:</strong> toca una vez en el mapa donde se encuentra.';

        const cancel = document.createElement('button');
        cancel.type = 'button';
        cancel.textContent = 'Cancelar';
        cancel.style.cssText = 'border:0;border-radius:9px;padding:7px 10px;background:#fff;color:#17212b;font-weight:650;cursor:pointer;';
        cancel.addEventListener('click', cancelLocationSelection);

        banner.append(text, cancel);
        document.body.appendChild(banner);
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

        selectingDraftLocation = true;
        document.body.style.cursor = 'crosshair';
        showLocationBanner();

        locationListener = google.maps.event.addListenerOnce(currentMap, 'click', async event => {
            selectingDraftLocation = false;
            document.body.style.cursor = '';
            removeLocationBanner();
            locationListener = null;

            if (locationTimeout) {
                clearTimeout(locationTimeout);
                locationTimeout = null;
            }

            const lat = Number(event.latLng.lat().toFixed(6));
            const lng = Number(event.latLng.lng().toFixed(6));

            await chooseTypeAndCreate(lat, lng);
        });

        locationTimeout = window.setTimeout(() => {
            if (selectingDraftLocation) cancelLocationSelection();
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

        const tiposValidos = (tipos || [])
            .filter(tipo => Number(tipo.idTipoPropiedad) > 1 && tipo.nombre);

        if (tiposValidos.length === 0) {
            await Swal.fire('Sin tipos disponibles', 'No hay tipos de propiedad disponibles para crear el borrador.', 'warning');
            return;
        }

        const optionsHtml = tiposValidos
            .map(tipo => `<option value="${Number(tipo.idTipoPropiedad)}">${escapeHtml(tipo.nombre)}</option>`)
            .join('');

        const getSelectedType = () => {
            const select = document.getElementById('draftTypeSelect');
            const value = Number(select?.value || 0);
            if (value <= 0) {
                Swal.showValidationMessage('Selecciona un tipo de propiedad.');
                return false;
            }
            return value;
        };

        const selection = await Swal.fire({
            title: '¿Qué propiedad es?',
            html: `
                <div style="text-align:left;margin-top:4px;">
                    <div style="margin-bottom:8px;color:#667085;font-size:13px;">Ubicación ✓ ${lat.toFixed(6)}, ${lng.toFixed(6)}</div>
                    <select id="draftTypeSelect" class="swal2-select" style="display:block;width:100%;margin:8px 0 4px;">
                        <option value="">Selecciona el tipo</option>
                        ${optionsHtml}
                    </select>
                    <div style="margin-top:12px;color:#667085;font-size:12px;line-height:1.4;">
                        <strong>Completar ahora</strong> abre la captura de datos.<br>
                        <strong>Guardar rápido</strong> lo deja privado para continuar después.
                    </div>
                </div>`,
            showCancelButton: true,
            showDenyButton: true,
            confirmButtonText: 'Completar ahora',
            denyButtonText: 'Guardar rápido',
            cancelButtonText: 'Cancelar',
            focusConfirm: false,
            preConfirm: getSelectedType,
            preDeny: getSelectedType
        });

        if (!selection.isConfirmed && !selection.isDenied) return;

        const idTipo = Number(document.getElementById('draftTypeSelect')?.value || selection.value || 0);
        if (idTipo <= 0) return;

        await createDraft(lat, lng, idTipo, selection.isConfirmed ? 'COMPLETAR' : 'RAPIDO');
    }

    async function createDraft(lat, lng, idTipo, nextAction) {
        Swal.fire({
            title: 'Guardando ubicación…',
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

            if (nextAction === 'COMPLETAR') {
                window.location.href = `/Borrador/Editar/${encodeURIComponent(data.idInmueble)}`;
                return;
            }

            await Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'success',
                title: `Borrador #${data.idInmueble} guardado`,
                text: 'Quedó privado. Puedes completarlo después desde Mi inventario.',
                showConfirmButton: false,
                timer: 3200,
                timerProgressBar: true
            });
        } catch (error) {
            await Swal.fire('No se pudo crear', error.message, 'error');
        }
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    window.startDraftCreation = startDraftCreation;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', addDraftMenuEntry);
    } else {
        addDraftMenuEntry();
    }
})();
