(() => {
    const form = document.getElementById('draftEditForm');
    if (!form) return;

    const idField = form.querySelector('input[name="IdInmueble"]');
    const idInmueble = Number(idField?.value || 0);
    if (!idInmueble) return;

    const style = document.createElement('style');
    style.textContent = `
        .optional-characteristics{background:#f8fbff;border:1px solid #dbe7f3!important;border-left:4px solid #8fb3d9!important;border-radius:16px;padding:18px!important;margin:2px 0 22px!important;box-shadow:0 1px 2px rgba(16,24,40,.02)}
        .optional-characteristics-head{display:flex;align-items:flex-start;justify-content:space-between;gap:14px;flex-wrap:wrap;margin-bottom:18px}
        .optional-characteristics-title{display:flex;align-items:center;gap:9px;flex-wrap:wrap;margin-bottom:5px}
        .optional-characteristics-title h3{margin:0!important}
        .optional-badge{display:inline-flex;align-items:center;padding:4px 9px;border-radius:999px;border:1px solid #cbdced;background:#eef5fb;color:#4b6b8f;font-size:11px;font-weight:800;letter-spacing:.02em}
        .optional-characteristics-copy{margin:0;color:#5f6f82;font-size:13px;line-height:1.45;max-width:760px}
        .optional-benefit{display:flex;align-items:center;gap:7px;border:1px solid #d9e5ef;background:#fff;border-radius:10px;padding:8px 10px;color:#50677f;font-size:12px;white-space:nowrap}
        .optional-benefit strong{color:#344b62}
        .characteristic-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}
        .characteristic-field label{display:block;font-size:13px;font-weight:700;margin-bottom:6px}
        .characteristic-field input{width:100%;border:1px solid #d8dde5;border-radius:11px;padding:11px 12px;background:#fff;color:#17212b}
        .amenity-divider{height:1px;background:#e5edf5;margin:18px 0 14px}
        .amenity-heading{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-bottom:4px}
        .amenity-heading strong{font-size:13px;color:#344054}
        .amenity-heading span{font-size:12px;color:#667085}
        .amenity-groups{display:grid;gap:14px;margin-top:14px}
        .amenity-group-title{font-size:12px;font-weight:800;letter-spacing:.04em;color:#667085;text-transform:uppercase;margin-bottom:7px}
        .amenity-grid{display:flex;gap:8px;flex-wrap:wrap}
        .amenity-chip{position:relative}
        .amenity-chip input{position:absolute;opacity:0;pointer-events:none}
        .amenity-chip span{display:inline-flex;align-items:center;border:1px solid #d8dde5;border-radius:999px;padding:8px 11px;background:#fff;color:#344054;font-size:12px;cursor:pointer;user-select:none;transition:background .15s,border-color .15s,color .15s}
        .amenity-chip span:hover{border-color:#b8c9db;background:#fbfdff}
        .amenity-chip input:checked + span{background:#eef9f1;border-color:#a8d5b6;color:#24613a;font-weight:700}
        .amenity-note{margin-top:12px;color:#667085;font-size:12px}
        @media(max-width:850px){
            .characteristic-grid{grid-template-columns:repeat(2,minmax(0,1fr))}
            .optional-benefit{white-space:normal}
        }
        @media(max-width:520px){
            .optional-characteristics{padding:15px!important}
            .characteristic-grid{grid-template-columns:1fr}
            .optional-characteristics-head{display:block}
            .optional-benefit{margin-top:10px}
        }
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
            // La captura basica sigue funcionando aunque esta seccion no pueda cargarse.
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

        const section = document.createElement('section');
        section.className = 'section optional-characteristics';
        section.id = 'structuredCharacteristicsSection';
        section.innerHTML = `
            <div class="optional-characteristics-head">
                <div>
                    <div class="optional-characteristics-title">
                        <h3>Características</h3>
                        <span class="optional-badge">Opcional</span>
                    </div>
                    <p class="optional-characteristics-copy">No es obligatorio completar esta sección para guardar o publicar. Agregar estos datos mejora la precisión de las búsquedas y filtros del mapa.</p>
                </div>
                <div class="optional-benefit"><span>⌖</span><span><strong>Más datos</strong> = mejores coincidencias</span></div>
            </div>

            <div class="characteristic-grid">
                ${numberField('Recamaras','Recámaras',data.recamaras,100)}
                ${numberField('BanosCompletos','Baños completos',data.banosCompletos,100)}
                ${numberField('MediosBanos','Medios baños',data.mediosBanos,100)}
                ${numberField('Estacionamientos','Estacionamientos',data.estacionamientos,100)}
                ${numberField('Niveles','Niveles',data.niveles,100)}
                ${numberField('AntiguedadAnos','Antigüedad (años)',data.antiguedadAnos,500)}
            </div>

            <div class="amenity-divider"></div>
            <div class="amenity-heading">
                <strong>Amenidades y detalles</strong>
                <span>Selecciona únicamente lo que aplique; también puedes completarlo después.</span>
            </div>
            <div class="amenity-groups" id="amenityGroups"></div>
            <div class="amenity-note">Las amenidades se guardan como datos estructurados para poder filtrarlas después en el mapa.</div>
        `;

        firstSection.insertAdjacentElement('afterend', section);
        renderAmenities(section.querySelector('#amenityGroups'), Array.isArray(data.amenidades) ? data.amenidades : []);
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
