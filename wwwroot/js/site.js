// Utilidades globales ligeras de RSMaps.

document.addEventListener('DOMContentLoaded', () => {
    const path = window.location.pathname.toLowerCase();
    if (path !== '/inventario' && path !== '/inventario/index') return;

    document.querySelectorAll('.property-card').forEach(card => {
        const estado = (card.dataset.state || '').toUpperCase();
        if (!['PUBLICADO', 'PAUSADO', 'RETIRADO'].includes(estado)) return;

        const panel = card.querySelector('.action-panel');
        if (!panel || panel.querySelector('.close-operation-section')) return;

        const idInput = panel.querySelector('input[name="idInmueble"]');
        const idInmueble = idInput?.value;
        if (!idInmueble) return;

        const section = document.createElement('div');
        section.className = 'action-section close-operation-section';

        const title = document.createElement('div');
        title.className = 'action-section-title';
        title.textContent = 'Resultado de comercialización';

        const link = document.createElement('a');
        link.className = 'action-btn';
        link.style.textDecoration = 'none';
        link.href = `/Inventario/CerrarOperacion?idInmueble=${encodeURIComponent(idInmueble)}`;
        link.textContent = 'Cerrar operación';

        section.appendChild(title);
        section.appendChild(link);

        const note = panel.querySelector('.action-note');
        if (note) {
            panel.insertBefore(section, note);
        } else {
            panel.appendChild(section);
        }
    });
});
