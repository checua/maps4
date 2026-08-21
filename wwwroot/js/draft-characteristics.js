(() => {
    const form = document.getElementById('draftEditForm');
    if (!form) return;

    const idField = form.querySelector('input[name="IdInmueble"]');
    const idInmueble = Number(idField?.value || 0);
    if (!idInmueble) return;

    const style = document.createElement('style');
    style.textContent = `
        .optional-characteristics{margin:20px 0 24px;padding:18px;border:1px solid #dbe7f3;border-left:4px solid #8fb3d9;border-radius:14px;background:#f8fbff}
        .optional-head{display:flex;align-items:flex-start;justify-content:space-between;gap:14px;flex-wrap:wrap;margin-bottom:16px}
        .optional-title-row{display:flex;align-items:center;gap:9px;flex-wrap:wrap}
        .optional-title-row h3{margin:0;font-size:19px}
        .optional-badge{display:inline-flex;align-items:center;padding:4px 9px;border-radius:999px;background:#eef5fb;border:1px solid #cfe0f0;color:#4b6b8f;font-size:11px;font-weight:800;text-transform:uppercase;letter-spacing:.03em}
        .optional-description{margin:5px 0 0;color:#5f6f82;font-size:13px;line-height:1.45;max-width:780px}
        .optional-benefit{display:inline-flex;align-items:center;gap:6px;color:#4b6b8f;background:#fff;border:1px solid #dbe7f3;border-radius:10px;padding:8px 10px;font-size:11px;font-weight:700;white-space:nowrap}
        .characteristic-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}
        .characteristic-field label{display:block;font-size:13px;font-weight:700;margin-bottom:6px}
        .characteristic-field input{width:100%;border:1px solid #d8dde5;border-radius:11px;padding:11px 12px;background:#fff;color:#17212b}
        .optional-more{margin-top:15px;border-top:1px solid #dbe7f3;padding-top:13px}
        .optional-more summary{list-style:none;display:flex;align-items:center;justify-content:space-between;gap:12px;cursor:pointer;border:1px solid #cfddea;background:#fff;border-radius:11px;padding:10px 12px;color:#344054;font-size:13px;font-weight:750;user-select:none}
        .optional-more summary::-webkit-details-marker{display:none}
        .optional-more summary::after{content:'⌄';font-size:18px;line-height:1;color:#667085;transition:transform .18s ease}
        .optional-more[open] summary::after{transform:rotate(180deg)}
        .optional-summary-left{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
        .optional-selected-count{display:none;padding:2px 7px;border-radius:999px;background:#eef9f1;border:1px solid #cfe8d6;color:#24613a;font-size:10px;font-weight:800}
        .optional-selected-count.visible{display:inline-flex}
        .optional-more-body{padding-top:15px}
        .amenity-zone{margin-top:17px;padding-top:15px;border-top:1px dashed #cfddea}
        .amenity-zone-title{font-size:13px;font-weight:800;color:#344054;margin-bottom:3px}
        .amenity-zone-help{font-size:12px;color:#667085;margin-bottom:12px}
        .amenity-groups{display:grid;gap:14px}
        .amenity-group-title{font-size:11px;font-weight:800;letter-spacing:.04em;color:#667085;text-transform:uppercase;margin-bottom:7px}
        .amenity-grid{display:flex;gap:8px;flex-wrap:wrap}
        .amenity-chip{position:relative}
        .amenity-chip input{position:absolute;opacity:0;pointer-events:none}
        .amenity-chip span{display:inline-flex;align-items:center;border:1px solid #d8dde5;border-radius:999px;padding:8px 11px;background:#fff;color:#344054;font-size:12px;cursor:pointer;user-select:none}
        .amenity-chip input:checked + span{background:#eef9f1;border-color:#a8d5b6;color:#24613a;font-weight:700}
        .amenity-note{margin-top:10px;color:#667085;font-size:11px}
        @media(max-width:850px){.characteristic-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.optional-benefit{white-space:normal}}
        @media(max-width:520px){.characteristic-grid{grid-template-columns:1fr}.optional-characteristics{padding:14px}}
    `;
    document.head.appendChild(style);

    load();

    async function load() {
        try {
            const response = await fetch(`/Borrador/Caracteristicas/${idInmueble}`, { credentials: 'same-origin' });
            if (!response.ok) return;
            const data = await response.json();
            render(data);
        } catch (_) {
            // La captura básica sigue funcionando aunque esta sección no pueda cargarse.
        }
    }

    function render(data) {
        if (document.getElementById('structuredCharacteristicsSection')) return;

        const firstSection = form.querySelector('.section');
        if (!firstSection) return;

        const loaded = document.createElement('input');
        loaded.type = 'hidden';
        loaded.name = 'CaracteristicasCargadas';
        loaded.value = 'true';
        form.appendChild(loaded);

        const amenities = Array.isArray(data.amenidades) ? data.amenidades : [];
        const selectedAmenities = amenities.filter(x => Boolean(x.seleccionada)).length;
        const hasAdvancedValues = hasValue(data.mediosBanos) || hasValue(data.niveles) || hasValue(data.antiguedadAnos) || selectedAmenities > 0;

        const section = document.createElement('section');
        section.className = 'optional-characteristics';
        section.id = 'structuredCharacteristicsSection';
        section.innerHTML = `
            <div class="optional-head">
                <div>
                    <div class="optional-title-row">
                        <h3>Características</h3>
                        <span class="optional-badge">Opcional</span>
                    </div>
                    <p class="optional-description">No es obligatorio completar esta sección para guardar o publicar. Agregar estos datos permite búsquedas mucho más precisas.</p>
                </div>
                <span class="optional-benefit">⌖ Más datos = mejores coincidencias</span>
            </div>

            <div class="characteristic-grid">
                ${numberField('Recamaras','Recámaras',data.recamaras,100)}
                ${numberField('BanosCompletos','Baños completos',data.banosCompletos,100)}
                ${numberField('Estacionamientos','Estacionamientos',data.estacionamientos,100)}
            </div>

            <details class="optional-more" id="optionalMoreCharacteristics" ${hasAdvancedValues ? 'open' : ''}>
                <summary>
                    <span class="optional-summary-left">
                        <span>Más características y amenidades</span>
                        <span class="optional-selected-count" id="optionalSelectedCount"></span>
                    </span>
                </summary>
                <div class="optional-more-body">
                    <div class="characteristic-grid">
                        ${numberField('MediosBanos','Medios baños',data.mediosBanos,100)}
                        ${numberField('Niveles','Niveles',data.niveles,100)}
                        ${numberField('AntiguedadAnos','Antigüedad (años)',data.antiguedadAnos,500)}
                    </div>
                    <div class="amenity-zone">
                        <div class="amenity-zone-title">Amenidades y detalles</div>
                        <div class="amenity-zone-help">Selecciona solo lo que aplique. Puedes regresar y completar esto después.</div>
                        <div class="amenity-groups" id="amenityGroups"></div>
                        <div class="amenity-note">Estas amenidades se guardan como datos estructurados para utilizarlas después en los filtros del mapa.</div>
                    </div>
                </div>
            </details>
        `;

        firstSection.insertAdjacentElement('afterend', section);
        renderAmenities(section.querySelector('#amenityGroups'), amenities);
        updateSelectedCount(section);
        section.addEventListener('change', () => updateSelectedCount(section));
    }

    function numberField(name, label, value, max) {
        const safeValue = value === null || value === undefined ? '' : String(value);
        return `<div class="characteristic-field"><label for="${name}">${label}</label><input id="${name}" name="${name}" type="number" min="0" max="${max}" step="1" value="${escapeHtml(safeValue)}" /></div>`;
    }

    function renderAmenities(container, amenities) {
        if (!container || !amenities.length) return;
        const groups = new Map();
        amenities.forEach(item => {
            const key = item.grupo || 'OTROS';
            if (!groups.has(key)) groups.set(key, []);
            groups.get(key).push(item);
        });

        groups.forEach((items, group) => {
            const wrapper = document.createElement('div');
            const title = document.createElement('div');
            title.className = 'amenity-group-title';
            title.textContent = friendlyGroup(group);
            wrapper.appendChild(title);

            const grid = document.createElement('div');
            grid.className = 'amenity-grid';
            items.sort((a,b) => (a.orden || 0) - (b.orden || 0)).forEach(item => {
                const label = document.createElement('label');
                label.className = 'amenity-chip';
                const input = document.createElement('input');
                input.type = 'checkbox';
                input.name = 'AmenidadesSeleccionadas';
                input.value = item.codigo;
                input.checked = Boolean(item.seleccionada);
                const span = document.createElement('span');
                span.textContent = item.nombre;
                label.append(input, span);
                grid.appendChild(label);
            });
            wrapper.appendChild(grid);
            container.appendChild(wrapper);
        });
    }

    function updateSelectedCount(section) {
        const badge = section.querySelector('#optionalSelectedCount');
        if (!badge) return;
        const advancedFilled = ['MediosBanos','Niveles','AntiguedadAnos']
            .filter(name => hasValue(form.querySelector(`[name="${name}"]`)?.value)).length;
        const selected = section.querySelectorAll('input[name="AmenidadesSeleccionadas"]:checked').length;
        const total = advancedFilled + selected;
        badge.textContent = total > 0 ? `${total} seleccionado${total === 1 ? '' : 's'}` : '';
        badge.classList.toggle('visible', total > 0);
    }

    function hasValue(value) {
        return value !== null && value !== undefined && String(value).trim() !== '';
    }

    function friendlyGroup(group) {
        const names = {
            EXTERIOR:'Exterior', SERVICIOS:'Servicios', SEGURIDAD:'Seguridad',
            ACCESIBILIDAD:'Accesibilidad', EQUIPAMIENTO:'Equipamiento', OTROS:'Otros'
        };
        return names[group] || group;
    }

    function escapeHtml(value) {
        return String(value).replace(/[&<>'"]/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[ch]));
    }
})();
