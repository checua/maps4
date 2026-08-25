(() => {
    'use strict';

    const cards = Array.from(document.querySelectorAll('.property-card'));
    if (cards.length === 0) return;

    const style = document.createElement('style');
    style.textContent = `
        .property-zones {
            margin-top: 8px;
            display: inline-flex;
            align-items: center;
            max-width: 100%;
            padding: 5px 9px;
            border-radius: 999px;
            background: #eef4ff;
            color: #344b7a;
            border: 1px solid #d8e3fb;
            font-size: 12px;
            line-height: 1.25;
        }
        .property-zones span {
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }
    `;
    document.head.appendChild(style);

    function obtenerId(card) {
        const texto = card.querySelector('.property-id')?.textContent || '';
        const match = texto.match(/\bID\s+(\d+)\b/i);
        return match ? Number(match[1]) : null;
    }

    const cardsPorId = new Map();
    cards.forEach(card => {
        const id = obtenerId(card);
        if (id) cardsPorId.set(id, card);
    });

    async function cargar() {
        try {
            const response = await fetch('/InventarioZonas', { credentials: 'same-origin' });
            if (!response.ok) return;

            const data = await response.json();
            const zonas = Array.isArray(data?.zonas) ? data.zonas : [];

            zonas.forEach(item => {
                const card = cardsPorId.get(Number(item.idInmueble));
                if (!card) return;

                const zonasCsv = String(item.zonasCsv || item.zonaPrincipalNombre || '').trim();
                if (!zonasCsv) return;

                const body = card.querySelector('.property-body');
                const tipo = card.querySelector('.property-type');
                if (!body || !tipo || body.querySelector('.property-zones')) return;

                const chip = document.createElement('div');
                chip.className = 'property-zones';
                chip.title = item.zonaPrincipalNombre
                    ? `Zona principal: ${item.zonaPrincipalNombre}`
                    : 'Zonas detectadas automáticamente';

                const texto = document.createElement('span');
                texto.textContent = `📍 ${zonasCsv}`;
                chip.appendChild(texto);
                tipo.insertAdjacentElement('afterend', chip);

                const busquedaActual = card.dataset.search || '';
                const extras = [
                    zonasCsv,
                    item.zonaPrincipalNombre || '',
                    item.zonaPrincipalCodigo || ''
                ].join(' ').toLowerCase();
                card.dataset.search = `${busquedaActual} ${extras}`.trim();
            });

            // Si el usuario ya escribió una búsqueda, recalcularla ahora que las
            // zonas forman parte del índice textual de cada tarjeta.
            document.getElementById('inventorySearch')
                ?.dispatchEvent(new Event('input', { bubbles: true }));
        } catch (error) {
            console.warn('No fue posible cargar las zonas del inventario.', error);
        }
    }

    cargar();
})();
