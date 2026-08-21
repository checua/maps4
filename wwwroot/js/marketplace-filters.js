(() => {
    const overMap = document.getElementById('over_map');
    if (!overMap || document.getElementById('rsmapsAdvancedFilters')) return;

    const state = {
        items: [],
        amenities: [],
        attached: false
    };

    injectStyles();
    renderShell();
    wireBaseFilters();
    initialize();

    async function initialize() {
        try {
            const [itemsResponse, amenitiesResponse] = await Promise.all([
                fetch('/Home/listaInmuebles', { credentials: 'same-origin' }),
                fetch('/Home/listaAmenidadesFiltros', { credentials: 'same-origin' })
            ]);

            if (!itemsResponse.ok) throw new Error('No fue posible cargar los inmuebles para filtros.');
            state.items = await itemsResponse.json();
            state.amenities = amenitiesResponse.ok ? await amenitiesResponse.json() : [];
            renderAmenities();
            await waitForMarkers();
            attachDataToMarkers();
            applyFilters();
        } catch (error) {
            console.warn('Filtros precisos no disponibles:', error);
            setStatus('Filtros avanzados no disponibles por el momento.');
        }
    }

    function renderShell() {
        const wrapper = document.createElement('div');
        wrapper.id = 'rsmapsAdvancedFilters';
        wrapper.className = 'rsmaps-filter-wrap';
        wrapper.innerHTML = `
            <button type="button" class="rsmaps-filter-toggle" id="rsmapsFilterToggle" aria-expanded="false">
                <span>Más filtros</span><span class="rsmaps-filter-badge" id="rsmapsFilterBadge"></span><span>⌄</span>
            </button>
            <div class="rsmaps-filter-panel" id="rsmapsFilterPanel" hidden>
                <div class="rsmaps-filter-title">Afinar búsqueda</div>
                <div class="rsmaps-filter-help">Solo se aplican los filtros que selecciones.</div>
                <div class="rsmaps-filter-grid">
                    ${selectField('rsmapsBedrooms','Recámaras mín.',['0:Cualquiera','1:1+','2:2+','3:3+','4:4+','5:5+'])}
                    ${selectField('rsmapsBathrooms','Baños mín.',['0:Cualquiera','1:1+','2:2+','3:3+','4:4+'])}
                    ${selectField('rsmapsParking','Estacionamientos',['0:Cualquiera','1:1+','2:2+','3:3+','4:4+'])}
                </div>
                <div class="rsmaps-amenity-title">Amenidades</div>
                <div class="rsmaps-amenities" id="rsmapsAmenities"><span class="rsmaps-filter-muted">Cargando…</span></div>
                <div class="rsmaps-filter-footer">
                    <button type="button" class="rsmaps-filter-clear" id="rsmapsFilterClear">Limpiar avanzados</button>
                    <strong id="rsmapsFilterStatus">—</strong>
                </div>
            </div>`;
        overMap.appendChild(wrapper);

        const toggle = document.getElementById('rsmapsFilterToggle');
        const panel = document.getElementById('rsmapsFilterPanel');
        toggle?.addEventListener('click', () => {
            const opening = panel?.hasAttribute('hidden');
            panel?.toggleAttribute('hidden', !opening);
            toggle.setAttribute('aria-expanded', opening ? 'true' : 'false');
        });

        ['rsmapsBedrooms','rsmapsBathrooms','rsmapsParking'].forEach(id => {
            document.getElementById(id)?.addEventListener('change', applyFilters);
        });

        document.getElementById('rsmapsFilterClear')?.addEventListener('click', () => {
            ['rsmapsBedrooms','rsmapsBathrooms','rsmapsParking'].forEach(id => {
                const element = document.getElementById(id);
                if (element) element.value = '0';
            });
            document.querySelectorAll('#rsmapsAmenities input[type="checkbox"]').forEach(x => x.checked = false);
            applyFilters();
        });
    }

    function selectField(id, label, options) {
        const html = options.map(raw => {
            const [value, text] = raw.split(':');
            return `<option value="${value}">${text}</option>`;
        }).join('');
        return `<label class="rsmaps-filter-field"><span>${label}</span><select id="${id}">${html}</select></label>`;
    }

    function renderAmenities() {
        const container = document.getElementById('rsmapsAmenities');
        if (!container) return;
        container.innerHTML = '';

        if (!state.amenities.length) {
            container.innerHTML = '<span class="rsmaps-filter-muted">Sin amenidades disponibles.</span>';
            return;
        }

        state.amenities.forEach(item => {
            const label = document.createElement('label');
            label.className = 'rsmaps-filter-chip';
            label.innerHTML = `<input type="checkbox" value="${escapeHtml(item.codigo)}"><span>${escapeHtml(item.nombre)}</span>`;
            label.querySelector('input')?.addEventListener('change', applyFilters);
            container.appendChild(label);
        });
    }

    function wireBaseFilters() {
        ['cboTipoPropiedad','ddlViewBy','ddlViewBy2'].forEach(id => {
            document.getElementById(id)?.addEventListener('change', () => {
                setTimeout(applyFilters, 0);
            });
        });
    }

    async function waitForMarkers() {
        for (let attempt = 0; attempt < 80; attempt++) {
            if (Array.isArray(window.markersx) && window.markersx.length > 0) return;
            await delay(100);
        }
    }

    function attachDataToMarkers() {
        if (!Array.isArray(window.markersx) || !window.markersx.length) return;

        const buckets = new Map();
        state.items.forEach(item => {
            const key = dataKey(item.lat, item.lng, item.idTipo, item.precio);
            if (!buckets.has(key)) buckets.set(key, []);
            buckets.get(key).push(item);
        });

        window.markersx.forEach(marker => {
            const position = marker.getPosition?.();
            if (!position) return;
            const key = dataKey(position.lat(), position.lng(), Number(marker.title || 0), Number(marker.customInfo || 0));
            const bucket = buckets.get(key);
            marker.rsmapsData = bucket?.length ? bucket.shift() : null;
        });
        state.attached = true;
    }

    function applyFilters() {
        if (!state.attached) {
            attachDataToMarkers();
            if (!state.attached) return;
        }

        const type = Number(document.getElementById('cboTipoPropiedad')?.value || 1);
        const minPrice = numberValue(document.getElementById('ddlViewBy')?.value, 0);
        const maxPrice = numberValue(document.getElementById('ddlViewBy2')?.value, Number.MAX_SAFE_INTEGER);
        const bedrooms = numberValue(document.getElementById('rsmapsBedrooms')?.value, 0);
        const bathrooms = numberValue(document.getElementById('rsmapsBathrooms')?.value, 0);
        const parking = numberValue(document.getElementById('rsmapsParking')?.value, 0);
        const amenities = Array.from(document.querySelectorAll('#rsmapsAmenities input:checked')).map(x => x.value.toUpperCase());

        let visible = 0;
        (window.markersx || []).forEach(marker => {
            const item = marker.rsmapsData;
            if (!item) {
                marker.setVisible(false);
                return;
            }

            const itemAmenities = new Set(String(item.amenidadesCsv || '')
                .split(',').map(x => x.trim().toUpperCase()).filter(Boolean));

            const matches =
                (type <= 1 || Number(item.idTipo) === type) &&
                Number(item.precio || 0) >= minPrice && Number(item.precio || 0) <= maxPrice &&
                (bedrooms <= 0 || Number(item.recamaras ?? -1) >= bedrooms) &&
                (bathrooms <= 0 || Number(item.banosCompletos ?? -1) >= bathrooms) &&
                (parking <= 0 || Number(item.estacionamientos ?? -1) >= parking) &&
                amenities.every(code => itemAmenities.has(code));

            marker.setVisible(matches);
            if (matches) visible++;
        });

        const advancedCount = [bedrooms,bathrooms,parking].filter(x => x > 0).length + amenities.length;
        const badge = document.getElementById('rsmapsFilterBadge');
        if (badge) {
            badge.textContent = advancedCount ? String(advancedCount) : '';
            badge.classList.toggle('visible', advancedCount > 0);
        }
        setStatus(`${visible} coincidencia${visible === 1 ? '' : 's'}`);
    }

    function setStatus(text) {
        const status = document.getElementById('rsmapsFilterStatus');
        if (status) status.textContent = text;
    }

    function dataKey(lat, lng, type, price) {
        return `${Number(lat || 0).toFixed(6)}|${Number(lng || 0).toFixed(6)}|${Number(type || 0)}|${Math.round(Number(price || 0) * 100)}`;
    }

    function numberValue(value, fallback) {
        const number = Number(String(value ?? '').replace(/,/g, ''));
        return Number.isFinite(number) ? number : fallback;
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>'"]/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[ch]));
    }

    function delay(ms) { return new Promise(resolve => setTimeout(resolve, ms)); }

    window.applyMarketplaceFilters = applyFilters;

    function injectStyles() {
        const style = document.createElement('style');
        style.textContent = `
            .rsmaps-filter-wrap{float:left;clear:both;margin-top:8px;width:230px;font-family:Arial,sans-serif}
            .rsmaps-filter-toggle{width:100%;display:flex;align-items:center;justify-content:space-between;gap:8px;background:rgba(255,255,255,.96);border:1px solid #d7dde5;border-radius:8px;padding:8px 10px;font-size:12px;font-weight:700;box-shadow:0 2px 8px rgba(0,0,0,.08)}
            .rsmaps-filter-badge{display:none;margin-left:auto;background:#17212b;color:#fff;border-radius:999px;min-width:19px;height:19px;align-items:center;justify-content:center;font-size:10px}.rsmaps-filter-badge.visible{display:inline-flex}
            .rsmaps-filter-panel{margin-top:5px;background:rgba(255,255,255,.98);border:1px solid #d7dde5;border-radius:10px;padding:11px;box-shadow:0 5px 18px rgba(0,0,0,.14);max-height:60vh;overflow:auto}
            .rsmaps-filter-title{font-size:13px;font-weight:800;color:#17212b}.rsmaps-filter-help{font-size:10px;color:#667085;margin:2px 0 9px}
            .rsmaps-filter-grid{display:grid;grid-template-columns:1fr;gap:7px}.rsmaps-filter-field span{display:block;font-size:10px;font-weight:700;color:#475467;margin-bottom:3px}.rsmaps-filter-field select{width:100%;border:1px solid #d7dde5;border-radius:7px;padding:6px;background:#fff;font-size:11px}
            .rsmaps-amenity-title{font-size:10px;font-weight:800;color:#475467;text-transform:uppercase;margin:10px 0 5px}.rsmaps-amenities{display:flex;gap:5px;flex-wrap:wrap}
            .rsmaps-filter-chip{position:relative;margin:0}.rsmaps-filter-chip input{position:absolute;opacity:0}.rsmaps-filter-chip span{display:inline-flex;border:1px solid #d7dde5;border-radius:999px;padding:5px 7px;background:#fff;font-size:9px;cursor:pointer}.rsmaps-filter-chip input:checked+span{background:#eaf6ee;border-color:#9ecdad;color:#205b34;font-weight:700}
            .rsmaps-filter-footer{display:flex;align-items:center;justify-content:space-between;gap:8px;border-top:1px solid #edf0f3;margin-top:10px;padding-top:8px;font-size:10px}.rsmaps-filter-clear{border:0;background:transparent;color:#667085;padding:0;text-decoration:underline;font-size:10px}.rsmaps-filter-muted{font-size:10px;color:#98a2b3}
            @media(max-width:600px){.rsmaps-filter-wrap{width:190px}.rsmaps-filter-panel{max-height:48vh}}
        `;
        document.head.appendChild(style);
    }
})();
