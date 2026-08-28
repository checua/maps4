// Utilidades globales ligeras de RSMaps.

document.addEventListener('DOMContentLoaded', () => {
    const path = window.location.pathname.toLowerCase();

    // Flujo moderno de captura desde el mapa. Se carga solo en Home para no
    // mezclar el nuevo módulo con el index.js legacy más de lo necesario.
    if (path === '/' || path === '/home' || path === '/home/index') {
        const script = document.createElement('script');
        script.src = '/js/draft.js';
        script.defer = true;
        document.body.appendChild(script);

        const marketplaceFilters = document.createElement('script');
        marketplaceFilters.src = '/js/marketplace-filters.js';
        marketplaceFilters.defer = true;
        document.body.appendChild(marketplaceFilters);

        // Navegación privada visible desde el mismo menú flotante del mapa.
        // Las rutas protegidas conservan la autorización del servidor.
        const menu = document.getElementById('menu-flotante');
        if (menu && !menu.querySelector('[data-rsmaps-nav="inventario"]')) {
            const inventario = document.createElement('a');
            inventario.href = '/Inventario';
            inventario.className = 'btn btn-info';
            inventario.dataset.rsmapsNav = 'inventario';
            inventario.textContent = 'Mi inventario';

            const zonas = document.createElement('a');
            zonas.href = '/ZonaAdmin';
            zonas.className = 'btn btn-info';
            zonas.dataset.rsmapsNav = 'zonas';
            zonas.textContent = 'Administrar zonas';

            menu.appendChild(inventario);
            menu.appendChild(zonas);
        }
    }

    if (path === '/inventario' || path === '/inventario/index') {
        const inventoryDraftScript = document.createElement('script');
        inventoryDraftScript.src = '/js/draft-inventory.js';
        inventoryDraftScript.defer = true;
        document.body.appendChild(inventoryDraftScript);

        const inventoryZonesScript = document.createElement('script');
        inventoryZonesScript.src = '/js/inventory-zones.js';
        inventoryZonesScript.defer = true;
        document.body.appendChild(inventoryZonesScript);

        // Desde Inventario debe ser evidente cómo volver al administrador de zonas.
        const inventoryActions = document.querySelector('.inventory-actions');
        if (inventoryActions && !inventoryActions.querySelector('[data-rsmaps-nav="zonas"]')) {
            const zonas = document.createElement('a');
            zonas.href = '/ZonaAdmin';
            zonas.className = 'back-map';
            zonas.dataset.rsmapsNav = 'zonas';
            zonas.textContent = 'Administrar zonas';
            inventoryActions.appendChild(zonas);
        }

        // Un borrador propio debe poder retomarse sin recordar su ID ni escribir rutas.
        document.querySelectorAll('.property-card[data-state="BORRADOR"][data-owner="true"]').forEach(card => {
            const idText = card.querySelector('.property-id')?.textContent || '';
            const match = idText.match(/\d+/);
            if (!match) return;

            const idInmueble = match[0];
            const primaryLink = card.querySelector('.property-footer > a');
            if (!primaryLink) return;

            primaryLink.href = `/Borrador/Editar/${encodeURIComponent(idInmueble)}`;
            primaryLink.textContent = 'Continuar borrador →';
            primaryLink.dataset.rsmapsDraftContinue = 'true';
        });

        // Aceptar también búsquedas escritas naturalmente como #176.
        const search = document.getElementById('inventorySearch');
        search?.addEventListener('input', () => {
            const value = search.value.trim();
            if (/^#\d+$/.test(value)) {
                search.value = value.substring(1);
                search.dispatchEvent(new Event('input', { bubbles: true }));
            }
        });

        // Si venimos de crear/guardar un borrador, enfocarlo automáticamente.
        const idBorrador = new URLSearchParams(window.location.search).get('borrador');

        if (idBorrador && search) {
            search.value = idBorrador;
            search.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }

    if (path === '/zonaadmin' || path === '/zonaadmin/index') {
        // ZonaAdmin ya tenía acceso al inventario; hacemos la navegación de los
        // tres módulos explícita y consistente sin alterar su lógica espacial.
        const header = document.querySelector('.zona-panel .d-flex.justify-content-between.align-items-start');
        const inventario = header?.querySelector('a[href="/Inventario"]');
        if (inventario) {
            inventario.textContent = 'Mi inventario';
            inventario.dataset.rsmapsNav = 'inventario';
        }

        if (header && !header.querySelector('[data-rsmaps-nav="mapa"]')) {
            const acciones = document.createElement('div');
            acciones.style.display = 'flex';
            acciones.style.gap = '6px';
            acciones.style.flexWrap = 'wrap';

            const mapa = document.createElement('a');
            mapa.href = '/';
            mapa.className = 'btn btn-sm btn-outline-secondary';
            mapa.dataset.rsmapsNav = 'mapa';
            mapa.textContent = 'Mapa';

            if (inventario) {
                inventario.parentNode?.insertBefore(acciones, inventario);
                acciones.appendChild(mapa);
                acciones.appendChild(inventario);
            } else {
                acciones.appendChild(mapa);
                header.appendChild(acciones);
            }
        }
    }

    if (path.startsWith('/borrador/editar')) {
        const characteristicsScript = document.createElement('script');
        characteristicsScript.src = '/js/draft-characteristics.js';
        characteristicsScript.defer = true;
        document.body.appendChild(characteristicsScript);

        const previewScript = document.createElement('script');
        previewScript.src = '/js/draft-preview.js';
        previewScript.defer = true;
        document.body.appendChild(previewScript);

        const photoActionsScript = document.createElement('script');
        photoActionsScript.src = '/js/draft-photo-actions.js';
        photoActionsScript.defer = true;
        document.body.appendChild(photoActionsScript);

        const publishScript = document.createElement('script');
        publishScript.src = '/js/draft-publish.js';
        publishScript.defer = true;
        document.body.appendChild(publishScript);
    }

    // UX de cierre: si conocemos el precio publicado y el campo todavía
    // viene en cero, usarlo como punto de partida editable para el asesor.
    if (path === '/inventario/cerraroperacion') {
        const precioCierre = document.getElementById('PrecioCierre');
        const precioPublicado = document.querySelector('.property-summary .price');

        if (precioCierre && precioPublicado && Number(precioCierre.value || 0) <= 0) {
            const texto = precioPublicado.textContent || '';
            const digitos = texto.replace(/[^0-9]/g, '');
            if (digitos && Number(digitos) > 0) {
                precioCierre.value = digitos;
            }
        }
    }
});
