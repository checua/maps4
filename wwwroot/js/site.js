// Utilidades globales ligeras de RSMaps.

document.addEventListener('DOMContentLoaded', () => {
    const path = window.location.pathname.toLowerCase();

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
