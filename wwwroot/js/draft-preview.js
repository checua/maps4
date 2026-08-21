(() => {
    const form = document.getElementById('draftEditForm');
    const summary = document.querySelector('.summary');
    if (!form || !summary) return;

    const originalState = Array.from(summary.querySelectorAll('.state-row .pill')).map(x => x.textContent?.trim()).filter(Boolean);

    const style = document.createElement('style');
    style.textContent = `
        .summary > .state-row,.summary > h2,.summary > .muted,.summary > .summary-price,.summary > .location-box{display:none}
        .draft-preview-label{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:10px}
        .draft-preview-label strong{font-size:13px;color:#344054}
        .draft-preview-label span{font-size:10px;color:#667085;text-transform:uppercase;letter-spacing:.04em;font-weight:800}
        .draft-preview-card{overflow:hidden;border:1px solid #e1e5ea;border-radius:15px;background:#fff;box-shadow:0 1px 3px rgba(16,24,40,.04)}
        .draft-preview-image{position:relative;aspect-ratio:4/3;background:#eef1f4;overflow:hidden}
        .draft-preview-image>img{width:100%;height:100%;object-fit:cover;display:block}
        .draft-preview-placeholder{width:100%;height:100%;display:flex;align-items:center;justify-content:center;color:#8290a3;font-size:34px}
        .draft-preview-status{position:absolute;left:9px;top:9px;display:flex;gap:5px;flex-wrap:wrap}
        .draft-preview-status span{background:rgba(255,255,255,.95);border:1px solid rgba(216,221,229,.9);border-radius:999px;padding:4px 7px;font-size:9px;font-weight:850;color:#17212b}
        .draft-preview-photo-count{position:absolute;right:9px;bottom:9px;background:rgba(23,33,43,.88);color:#fff;border-radius:999px;padding:4px 8px;font-size:10px;font-weight:750}
        .draft-preview-thumbs{display:flex;gap:5px;padding:6px;background:#f8f9fb;border-top:1px solid #eef0f3}
        .draft-preview-thumbs img{width:42px;height:32px;object-fit:cover;border-radius:6px;border:1px solid #d8dde5}
        .draft-preview-thumbs img.cover{outline:2px solid #344054;outline-offset:1px}
        .draft-preview-body{padding:13px}
        .draft-preview-price{font-size:23px;font-weight:800;color:#17212b;line-height:1.1;margin-bottom:7px}
        .draft-preview-address{font-size:13px;color:#667085;line-height:1.35;margin-bottom:10px}
        .draft-preview-type{font-size:12px;font-weight:800;color:#344054;margin-bottom:10px}
        .draft-preview-stats{display:flex;gap:6px;flex-wrap:wrap;padding-top:9px;border-top:1px solid #eef0f3}
        .draft-preview-stat{font-size:10px;color:#475467;background:#f7f8fa;border-radius:999px;padding:5px 7px}
        .draft-preview-description{margin-top:10px;color:#667085;font-size:11px;line-height:1.45;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden}
        .draft-preview-amenities{display:flex;gap:5px;flex-wrap:wrap;margin-top:9px}
        .draft-preview-amenities span{font-size:9px;color:#24613a;background:#eef9f1;border:1px solid #cfe8d6;border-radius:999px;padding:4px 6px}
        .draft-preview-more{font-size:9px!important;color:#667085!important;background:#f2f4f7!important;border-color:#e1e5ea!important}
        .summary .progress-head{margin-top:18px}
    `;
    document.head.appendChild(style);

    const label = document.createElement('div');
    label.className = 'draft-preview-label';
    label.innerHTML = '<strong>Vista previa</strong><span>Resumen público</span>';

    const card = document.createElement('div');
    card.className = 'draft-preview-card';
    card.id = 'draftLivePreview';

    summary.insertBefore(card, summary.firstChild);
    summary.insertBefore(label, card);

    render();

    form.addEventListener('input', render);
    form.addEventListener('change', render);

    const observer = new MutationObserver(() => render());
    observer.observe(form, { childList: true, subtree: true });

    function render() {
        const photos = getPhotos();
        const cover = photos.find(x => x.isCover) || photos[0] || null;
        const typeSelect = form.querySelector('[name="IdTipo"]');
        const typeName = typeSelect?.selectedOptions?.[0]?.textContent?.trim() || 'Propiedad';
        const price = numberValue('Precio');
        const address = stringValue('Direccion') || 'Ubicación registrada en mapa';
        const description = stringValue('Observaciones');
        const amenities = selectedAmenities();
        const stats = buildStats();

        const imageHtml = cover
            ? `<img src="${escapeAttr(cover.src)}" alt="Portada del inmueble" />`
            : '<div class="draft-preview-placeholder">⌂</div>';

        const states = originalState.length ? originalState : ['BORRADOR','CUENTA'];
        const statusHtml = states.map(x => `<span>${escapeHtml(x)}</span>`).join('');
        const thumbs = photos.slice(0, 4).map(x => `<img src="${escapeAttr(x.src)}" alt="Miniatura" class="${x.isCover ? 'cover' : ''}" />`).join('');
        const extraAmenities = Math.max(0, amenities.length - 4);
        const amenityHtml = amenities.slice(0,4).map(x => `<span>${escapeHtml(x)}</span>`).join('') +
            (extraAmenities > 0 ? `<span class="draft-preview-more">+${extraAmenities}</span>` : '');

        card.innerHTML = `
            <div class="draft-preview-image">
                ${imageHtml}
                <div class="draft-preview-status">${statusHtml}</div>
                ${photos.length ? `<div class="draft-preview-photo-count">📷 ${photos.length}</div>` : ''}
            </div>
            ${photos.length > 1 ? `<div class="draft-preview-thumbs">${thumbs}</div>` : ''}
            <div class="draft-preview-body">
                <div class="draft-preview-price">${price > 0 ? formatCurrency(price) : 'Precio pendiente'}</div>
                <div class="draft-preview-address">${escapeHtml(address)}</div>
                <div class="draft-preview-type">${escapeHtml(typeName)}</div>
                ${stats.length ? `<div class="draft-preview-stats">${stats.map(x => `<span class="draft-preview-stat">${escapeHtml(x)}</span>`).join('')}</div>` : ''}
                ${amenityHtml ? `<div class="draft-preview-amenities">${amenityHtml}</div>` : ''}
                ${description ? `<div class="draft-preview-description">${escapeHtml(description)}</div>` : ''}
            </div>`;
    }

    function getPhotos() {
        return Array.from(document.querySelectorAll('.photo-card')).map(card => {
            const img = card.querySelector('.photo-image img');
            return img ? { src: img.getAttribute('src') || '', isCover: Boolean(card.querySelector('.photo-cover')) } : null;
        }).filter(Boolean);
    }

    function buildStats() {
        const result = [];
        const terrain = numberValue('Terreno');
        const construction = numberValue('Construccion');
        const bedrooms = numberValue('Recamaras');
        const baths = numberValue('BanosCompletos');
        const parking = numberValue('Estacionamientos');
        if (terrain > 0) result.push(`Terreno ${formatNumber(terrain)} m²`);
        if (construction > 0) result.push(`Construcción ${formatNumber(construction)} m²`);
        if (bedrooms >= 0 && hasInputValue('Recamaras')) result.push(`${bedrooms} rec.`);
        if (baths >= 0 && hasInputValue('BanosCompletos')) result.push(`${baths} baños`);
        if (parking >= 0 && hasInputValue('Estacionamientos')) result.push(`${parking} estac.`);
        return result;
    }

    function selectedAmenities() {
        return Array.from(form.querySelectorAll('input[name="AmenidadesSeleccionadas"]:checked'))
            .map(input => input.nextElementSibling?.textContent?.trim() || input.value)
            .filter(Boolean);
    }

    function numberValue(name) {
        const raw = form.querySelector(`[name="${name}"]`)?.value;
        if (raw === undefined || raw === null || String(raw).trim() === '') return 0;
        const value = Number(raw);
        return Number.isFinite(value) ? value : 0;
    }

    function hasInputValue(name) {
        const raw = form.querySelector(`[name="${name}"]`)?.value;
        return raw !== undefined && raw !== null && String(raw).trim() !== '';
    }

    function stringValue(name) {
        return form.querySelector(`[name="${name}"]`)?.value?.trim() || '';
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('es-MX', { style:'currency', currency:'MXN', maximumFractionDigits:0 }).format(value);
    }

    function formatNumber(value) {
        return new Intl.NumberFormat('es-MX', { maximumFractionDigits:2 }).format(value);
    }

    function escapeHtml(value) {
        return String(value).replace(/[&<>'"]/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[ch]));
    }

    function escapeAttr(value) {
        return escapeHtml(value);
    }
})();
