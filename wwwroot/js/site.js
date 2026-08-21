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
    }

    if (path === '/inventario' || path === '/inventario/index') {
        const inventoryDraftScript = document.createElement('script');
        inventoryDraftScript.src = '/js/draft-inventory.js';
        inventoryDraftScript.defer = true;
        document.body.appendChild(inventoryDraftScript);

        // Si venimos de crear/guardar un borrador, enfocarlo automáticamente.
        const idBorrador = new URLSearchParams(window.location.search).get('borrador');
        const search = document.getElementById('inventorySearch');

        if (idBorrador && search) {
            search.value = idBorrador;
            search.dispatchEvent(new Event('input', { bubbles: true }));
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
