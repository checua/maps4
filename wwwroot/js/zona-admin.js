(() => {
    'use strict';

    let map;
    let zonaPolygon = null;
    let modoDibujo = false;
    let codigoTocado = false;
    let infoWindow = null;
    let mapLoadTimeout = null;
    let zonaAliases = [];
    let zonasOverview = [];
    let zonasOverviewVisibles = false;
    let zonasOverviewCargando = false;
    const inmuebleCircles = new Map();

    const defaultCenter = { lat: 24.0277, lng: -104.6532 };

    function cargarGoogleMaps() {
        if (window.google?.maps) {
            inicializarMapa();
            return;
        }

        setStatus('Cargando Google Maps…');

        const existente = document.querySelector('script[data-rsmaps-zona-google="1"]');
        if (existente) return;

        const script = document.createElement('script');
        const apiKey = 'AIzaSyAZ7HVHi9uywPRyEgtb9U-0Ul0C_v5zQXg';
        const callbackName = '__rsMapsZonaGoogleReady';

        window[callbackName] = () => {
            if (mapLoadTimeout) {
                clearTimeout(mapLoadTimeout);
                mapLoadTimeout = null;
            }
            try {
                inicializarMapa();
            } catch (error) {
                console.error('No fue posible inicializar el mapa de zonas.', error);
                setStatus('Google Maps cargó, pero no fue posible inicializar el mapa. Revisa la consola.');
            }
        };

        const authAnterior = window.gm_authFailure;
        window.gm_authFailure = () => {
            if (typeof authAnterior === 'function') authAnterior();
            setStatus('Google Maps rechazó la clave para esta página. Revisa las restricciones del API key.');
        };

        script.dataset.rsmapsZonaGoogle = '1';
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&callback=${callbackName}&v=weekly&loading=async`;
        script.async = true;
        script.defer = true;
        script.onerror = () => {
            if (mapLoadTimeout) clearTimeout(mapLoadTimeout);
            setStatus('No fue posible descargar Google Maps. Revisa conexión, bloqueadores o consola.');
        };
        document.head.appendChild(script);

        mapLoadTimeout = setTimeout(() => {
            if (!map) {
                setStatus('Google Maps está tardando demasiado. Abre F12 > Consola para ver el motivo.');
            }
        }, 10000);
    }

    function inicializarMapa() {
        if (map) return;
        if (!window.google?.maps) {
            setStatus('La biblioteca de Google Maps todavía no está disponible.');
            return;
        }

        const contenedor = document.getElementById('zona-map');
        if (!contenedor) {
            throw new Error('No existe el contenedor #zona-map.');
        }

        map = new google.maps.Map(contenedor, {
            center: defaultCenter,
            zoom: 12,
            mapTypeId: google.maps.MapTypeId.ROADMAP,
            streetViewControl: false,
            mapTypeControl: true,
            fullscreenControl: true
        });

        infoWindow = new google.maps.InfoWindow();
        cargarPinsInventario();

        map.addListener('click', (event) => {
            if (!modoDibujo || !event.latLng) return;
            agregarVertice(event.latLng);
        });

        setStatus('Mapa listo. Crea una zona o selecciona una existente.');
    }

    function cargarPinsInventario() {
        const pins = Array.isArray(window.rsMapsZonaPins) ? window.rsMapsZonaPins : [];
        inmuebleCircles.clear();

        pins.forEach(pin => {
            const lat = Number(pin.lat);
            const lng = Number(pin.lng);
            if (!Number.isFinite(lat) || !Number.isFinite(lng)) return;

            const coberturaPendiente = pin.coberturaPendiente === true;
            const fueraAreaActual = pin.fueraAreaActual === true;
            const color = coberturaPendiente ? '#f59e0b' : (fueraAreaActual ? '#94a3b8' : '#2563eb');

            const circle = new google.maps.Circle({
                map,
                center: { lat, lng },
                radius: coberturaPendiente ? 60 : 42,
                strokeColor: color,
                strokeOpacity: fueraAreaActual ? 0.45 : 0.9,
                strokeWeight: coberturaPendiente ? 2 : 1,
                fillColor: color,
                fillOpacity: fueraAreaActual ? 0.25 : 0.75,
                clickable: true,
                zIndex: coberturaPendiente ? 30 : (fueraAreaActual ? 10 : 20)
            });

            inmuebleCircles.set(Number(pin.idInmueble), { circle, pin });
            circle.addListener('click', () => abrirInfoInmueble(pin));
        });
    }

    function abrirInfoInmueble(pin) {
        if (!map || !infoWindow || !pin) return;

        const lat = Number(pin.lat);
        const lng = Number(pin.lng);
        if (!Number.isFinite(lat) || !Number.isFinite(lng)) return;

        const precio = pin.precio
            ? Number(pin.precio).toLocaleString('es-MX', { style: 'currency', currency: 'MXN', maximumFractionDigits: 0 })
            : 'Sin precio';
        const estado = pin.coberturaPendiente === true
            ? '<br><strong style="color:#b45309">⚠ Cobertura pendiente</strong>'
            : (pin.fueraAreaActual === true
                ? '<br><span style="color:#64748b">Fuera del área actual de zonas</span>'
                : '');
        const idInmueble = encodeURIComponent(pin.idInmueble);
        const acciones = `
            <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:10px;padding-top:8px;border-top:1px solid #e5e7eb">
                <a href="/?inmuebleId=${idInmueble}" target="_blank" rel="noopener"
                   style="font-weight:600;text-decoration:none">Ver propiedad</a>
                <a href="/Inventario" target="_blank" rel="noopener"
                   style="color:#475569;text-decoration:none">Inventario</a>
            </div>`;

        infoWindow.setPosition({ lat, lng });
        infoWindow.setContent(`
            <div style="min-width:200px">
                <strong>#${pin.idInmueble} ${escapeHtml(pin.tipo || 'Inmueble')}</strong><br>
                <span>${escapeHtml(pin.direccion || 'Sin dirección')}</span><br>
                <span>${escapeHtml(precio)}</span>${estado}
                ${acciones}
            </div>`);
        infoWindow.open({ map });
    }

    function enfocarInmueble(idInmueble) {
        if (!map) return;

        const item = inmuebleCircles.get(Number(idInmueble));
        if (!item) return;

        const lat = Number(item.pin.lat);
        const lng = Number(item.pin.lng);
        if (!Number.isFinite(lat) || !Number.isFinite(lng)) return;

        map.panTo({ lat, lng });
        map.setZoom(Math.max(map.getZoom() || 12, 16));
        abrirInfoInmueble(item.pin);
        setStatus(`Inmueble #${item.pin.idInmueble}: revisa el hueco de cobertura y decide si amplías una zona o creas una nueva.`);
    }

    function iniciarDibujo() {
        if (!map) {
            Swal.fire('Zonas', 'El mapa todavía no está disponible.', 'warning');
            return;
        }
        modoDibujo = true;
        map.setOptions({ draggableCursor: 'crosshair' });
        setStatus(zonasOverviewVisibles
            ? 'Modo dibujo activo con todas las zonas visibles: usa los polígonos tenues como referencia y haz clic para agregar vértices.'
            : 'Modo dibujo activo: haz clic para agregar vértices.');
    }

    function agregarVertice(latLng) {
        if (!zonaPolygon) {
            crearPoligono([{ lat: latLng.lat(), lng: latLng.lng() }]);
        } else {
            zonaPolygon.getPath().push(latLng);
        }
        actualizarStatusVertices();
    }

    function crearPoligono(vertices) {
        if (zonaPolygon) zonaPolygon.setMap(null);

        zonaPolygon = new google.maps.Polygon({
            map,
            paths: vertices,
            strokeColor: colorActual(),
            strokeOpacity: 0.95,
            strokeWeight: 2,
            fillColor: colorActual(),
            fillOpacity: 0.28,
            editable: true,
            draggable: false,
            clickable: true,
            zIndex: 10
        });

        vincularEventosPath();
    }

    function vincularEventosPath() {
        if (!zonaPolygon) return;
        const path = zonaPolygon.getPath();
        path.addListener('insert_at', actualizarStatusVertices);
        path.addListener('set_at', actualizarStatusVertices);
        path.addListener('remove_at', actualizarStatusVertices);
    }

    function aplicarColor() {
        if (!zonaPolygon) return;
        const color = colorActual();
        zonaPolygon.setOptions({ strokeColor: color, fillColor: color });
    }

    function deshacerVertice() {
        if (!zonaPolygon) return;
        const path = zonaPolygon.getPath();
        if (path.getLength() > 0) path.pop();
        if (path.getLength() === 0) {
            zonaPolygon.setMap(null);
            zonaPolygon = null;
        }
        actualizarStatusVertices();
    }

    function limpiarPoligono() {
        if (zonaPolygon) zonaPolygon.setMap(null);
        zonaPolygon = null;
        modoDibujo = false;
        if (map) map.setOptions({ draggableCursor: null });
        setStatus(zonasOverviewVisibles
            ? 'Polígono editable limpio. Las zonas existentes siguen visibles como referencia.'
            : 'Polígono limpio. Pulsa Dibujar para comenzar.');
    }

    function nuevaZona() {
        document.getElementById('zona-id').value = '';
        document.getElementById('zona-nombre').value = '';
        document.getElementById('zona-codigo').value = '';
        document.getElementById('zona-prioridad').value = '100';
        document.getElementById('zona-color').value = '#ef4444';
        document.getElementById('zona-descripcion').value = '';
        document.getElementById('zona-alias-input').value = '';
        zonaAliases = [];
        renderAliases();
        codigoTocado = false;
        limpiarPoligono();
        setStatus(zonasOverviewVisibles
            ? 'Nueva zona. Las zonas existentes quedan visibles para ayudarte a cubrir huecos sin solaparlas.'
            : 'Nueva zona. Escribe un nombre y dibuja el perímetro.');
    }

    async function editarZona(idZona) {
        modoDibujo = false;
        if (map) map.setOptions({ draggableCursor: null });
        setStatus('Cargando zona…');

        try {
            const response = await fetch(`/ZonaAdmin/Obtener?id=${encodeURIComponent(idZona)}`, { credentials: 'same-origin' });
            if (!response.ok) throw new Error('No fue posible cargar la zona.');

            const data = await response.json();
            const zona = data.zona;

            document.getElementById('zona-id').value = zona.idZona;
            document.getElementById('zona-nombre').value = zona.nombre || '';
            document.getElementById('zona-codigo').value = zona.codigo || '';
            document.getElementById('zona-prioridad').value = zona.prioridad ?? 100;
            document.getElementById('zona-color').value = zona.colorHex || '#ef4444';
            document.getElementById('zona-descripcion').value = zona.descripcion || '';
            document.getElementById('zona-alias-input').value = '';
            zonaAliases = Array.isArray(zona.aliases)
                ? zona.aliases.filter(x => typeof x === 'string' && x.trim()).map(x => x.trim())
                : [];
            renderAliases();
            codigoTocado = true;

            const vertices = Array.isArray(zona.vertices) ? zona.vertices.map(v => ({ lat: Number(v.lat), lng: Number(v.lng) })) : [];
            if (vertices.length >= 3) {
                crearPoligono(vertices);
                ajustarMapaAlPoligono();
                setStatus(`${zona.nombre}: ${vertices.length} vértices · ${zonaAliases.length} alias. Puedes mover los puntos directamente.`);
            } else {
                limpiarPoligono();
                setStatus('La zona no tiene un polígono editable. Pulsa Dibujar.');
            }
        } catch (error) {
            console.error(error);
            Swal.fire('Zona', error.message || 'No fue posible cargar la zona.', 'error');
            setStatus('No fue posible cargar la zona.');
        }
    }

    function ajustarMapaAlPoligono() {
        if (!zonaPolygon) return;
        const bounds = new google.maps.LatLngBounds();
        zonaPolygon.getPath().forEach(p => bounds.extend(p));
        if (!bounds.isEmpty()) map.fitBounds(bounds, 40);
    }

    function obtenerIdsZonas() {
        return Array.from(document.querySelectorAll('[data-id-zona]'))
            .map(card => Number(card.dataset.idZona))
            .filter(id => Number.isInteger(id) && id > 0);
    }

    function actualizarBotonTodasZonas() {
        const boton = document.getElementById('btn-zona-ver-todas');
        if (!boton) return;
        boton.textContent = zonasOverviewVisibles ? 'Ocultar todas' : 'Ver todas';
        boton.classList.toggle('btn-dark', zonasOverviewVisibles);
        boton.classList.toggle('btn-outline-dark', !zonasOverviewVisibles);
        boton.disabled = zonasOverviewCargando;
    }

    function ocultarTodasZonas() {
        zonasOverview.forEach(item => item.polygon?.setMap(null));
        zonasOverview = [];
        zonasOverviewVisibles = false;
        actualizarBotonTodasZonas();
        setStatus(zonaPolygon
            ? 'Vista general oculta. La zona seleccionada permanece editable.'
            : 'Vista general oculta. Selecciona una zona o pulsa Nueva.');
    }

    async function mostrarTodasZonas() {
        if (!map) {
            Swal.fire('Zonas', 'El mapa todavía no está disponible.', 'warning');
            return;
        }
        if (zonasOverviewCargando) return;

        const ids = obtenerIdsZonas();
        if (ids.length === 0) {
            Swal.fire('Zonas', 'Todavía no hay zonas guardadas para mostrar.', 'info');
            return;
        }

        zonasOverviewCargando = true;
        actualizarBotonTodasZonas();
        setStatus(`Cargando ${ids.length} zona(s) para la vista general…`);

        try {
            zonasOverview.forEach(item => item.polygon?.setMap(null));
            zonasOverview = [];

            const resultados = await Promise.all(ids.map(async idZona => {
                try {
                    const response = await fetch(`/ZonaAdmin/Obtener?id=${encodeURIComponent(idZona)}`, { credentials: 'same-origin' });
                    if (!response.ok) return null;
                    const data = await response.json();
                    return data?.zona || null;
                } catch (error) {
                    console.warn(`No fue posible cargar la zona ${idZona} para la vista general.`, error);
                    return null;
                }
            }));

            const bounds = new google.maps.LatLngBounds();
            let totalZonas = 0;
            let totalVertices = 0;

            resultados.filter(Boolean).forEach(zona => {
                const vertices = Array.isArray(zona.vertices)
                    ? zona.vertices
                        .map(v => ({ lat: Number(v.lat), lng: Number(v.lng) }))
                        .filter(v => Number.isFinite(v.lat) && Number.isFinite(v.lng))
                    : [];

                if (vertices.length < 3) return;

                const color = /^#[0-9A-Fa-f]{6}$/.test(zona.colorHex || '') ? zona.colorHex : '#64748b';
                const polygon = new google.maps.Polygon({
                    map,
                    paths: vertices,
                    strokeColor: color,
                    strokeOpacity: 0.82,
                    strokeWeight: 2,
                    fillColor: color,
                    fillOpacity: 0.12,
                    editable: false,
                    draggable: false,
                    clickable: false,
                    zIndex: 4
                });

                vertices.forEach(v => bounds.extend(v));
                zonasOverview.push({ idZona: Number(zona.idZona), nombre: zona.nombre || zona.codigo || 'Zona', polygon });
                totalZonas++;
                totalVertices += vertices.length;
            });

            if (totalZonas === 0) {
                throw new Error('Ninguna zona guardada tiene un polígono válido para mostrar.');
            }

            zonasOverviewVisibles = true;
            if (!bounds.isEmpty()) map.fitBounds(bounds, 35);
            setStatus(`Vista general: ${totalZonas} zona(s) · ${totalVertices} vértices. Los polígonos tenues muestran la cobertura actual; puedes pulsar Nueva y dibujar sobre los huecos.`);
        } catch (error) {
            console.error(error);
            zonasOverview.forEach(item => item.polygon?.setMap(null));
            zonasOverview = [];
            zonasOverviewVisibles = false;
            Swal.fire('Zonas', error.message || 'No fue posible mostrar todas las zonas.', 'error');
            setStatus('No fue posible cargar la vista general de zonas.');
        } finally {
            zonasOverviewCargando = false;
            actualizarBotonTodasZonas();
        }
    }

    async function toggleTodasZonas() {
        if (zonasOverviewVisibles) {
            ocultarTodasZonas();
            return;
        }
        await mostrarTodasZonas();
    }

    function agregarAliasDesdeInput() {
        const input = document.getElementById('zona-alias-input');
        if (!input) return;

        const valor = input.value.trim();
        if (!valor) return;
        if (valor.length < 2 || valor.length > 150) {
            Swal.fire('Alias', 'El alias debe tener entre 2 y 150 caracteres.', 'warning');
            return;
        }
        if (zonaAliases.length >= 50) {
            Swal.fire('Alias', 'La zona ya tiene el máximo de 50 alias.', 'warning');
            return;
        }

        const llave = normalizarAliasCliente(valor);
        const existe = zonaAliases.some(x => normalizarAliasCliente(x) === llave);
        if (!existe) {
            zonaAliases.push(valor);
            zonaAliases.sort((a, b) => a.localeCompare(b, 'es', { sensitivity: 'base' }));
            renderAliases();
        }
        input.value = '';
        input.focus();
    }

    function eliminarAlias(indice) {
        if (indice < 0 || indice >= zonaAliases.length) return;
        zonaAliases.splice(indice, 1);
        renderAliases();
    }

    function renderAliases() {
        const container = document.getElementById('zona-alias-list');
        if (!container) return;
        container.innerHTML = '';

        zonaAliases.forEach((alias, indice) => {
            const chip = document.createElement('div');
            chip.className = 'zona-alias-chip';

            const texto = document.createElement('span');
            texto.textContent = alias;
            texto.title = alias;

            const quitar = document.createElement('button');
            quitar.type = 'button';
            quitar.setAttribute('aria-label', `Quitar ${alias}`);
            quitar.title = 'Quitar alias';
            quitar.textContent = '×';
            quitar.addEventListener('click', () => eliminarAlias(indice));

            chip.appendChild(texto);
            chip.appendChild(quitar);
            container.appendChild(chip);
        });
    }

    function normalizarAliasCliente(value) {
        return String(value || '')
            .trim()
            .toLowerCase()
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .replace(/[^a-z0-9ñ]+/g, ' ')
            .replace(/\s+/g, ' ')
            .trim();
    }

    async function guardarZona() {
        const nombre = document.getElementById('zona-nombre').value.trim();
        const codigo = document.getElementById('zona-codigo').value.trim();
        const prioridad = Number(document.getElementById('zona-prioridad').value || 100);
        const colorHex = colorActual();
        const descripcion = document.getElementById('zona-descripcion').value.trim();
        const idRaw = document.getElementById('zona-id').value;
        const vertices = obtenerVertices();

        if (!nombre) {
            Swal.fire('Zona', 'Escribe el nombre de la zona.', 'warning');
            return;
        }
        if (vertices.length < 3) {
            Swal.fire('Zona', 'Dibuja al menos tres vértices.', 'warning');
            return;
        }

        modoDibujo = false;
        if (map) map.setOptions({ draggableCursor: null });
        setStatus('Guardando zona, alias y reclasificando inmuebles…');

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const payload = {
            idZona: idRaw ? Number(idRaw) : null,
            codigo,
            nombre,
            descripcion,
            prioridad,
            colorHex,
            aliases: zonaAliases.slice(),
            vertices
        };

        try {
            const response = await fetch('/ZonaAdmin/Guardar', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(payload)
            });

            let data = null;
            try { data = await response.json(); } catch { }

            if (!response.ok) {
                throw new Error(data?.message || `No fue posible guardar la zona (${response.status}).`);
            }

            await Swal.fire('Zona guardada', data.message || 'La zona se guardó correctamente.', 'success');
            window.location.reload();
        } catch (error) {
            console.error(error);
            Swal.fire('Zona', error.message || 'No fue posible guardar la zona.', 'error');
            setStatus('No fue posible guardar la zona.');
        }
    }

    function obtenerVertices() {
        if (!zonaPolygon) return [];
        const vertices = [];
        zonaPolygon.getPath().forEach(p => vertices.push({
            lat: Number(p.lat().toFixed(7)),
            lng: Number(p.lng().toFixed(7))
        }));
        return vertices;
    }

    function actualizarStatusVertices() {
        const total = zonaPolygon ? zonaPolygon.getPath().getLength() : 0;
        setStatus(modoDibujo
            ? `Dibujando: ${total} vértice(s). Sigue haciendo clic; luego mueve los puntos si necesitas pulir.`
            : `Polígono: ${total} vértice(s).`);
    }

    function colorActual() {
        return document.getElementById('zona-color')?.value || '#ef4444';
    }

    function generarCodigo(nombre) {
        return (nombre || '')
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .toUpperCase()
            .replace(/[^A-Z0-9]+/g, '_')
            .replace(/^_+|_+$/g, '')
            .substring(0, 60);
    }

    function setStatus(texto) {
        const el = document.getElementById('zona-status');
        if (el) el.textContent = texto;
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.getElementById('btn-zona-nueva')?.addEventListener('click', nuevaZona);
        document.getElementById('btn-zona-dibujar')?.addEventListener('click', iniciarDibujo);
        document.getElementById('btn-zona-deshacer')?.addEventListener('click', deshacerVertice);
        document.getElementById('btn-zona-limpiar')?.addEventListener('click', limpiarPoligono);
        document.getElementById('btn-zona-guardar')?.addEventListener('click', guardarZona);
        document.getElementById('btn-zona-ver-todas')?.addEventListener('click', toggleTodasZonas);
        document.getElementById('btn-zona-alias-agregar')?.addEventListener('click', agregarAliasDesdeInput);
        document.getElementById('zona-color')?.addEventListener('input', aplicarColor);

        document.getElementById('zona-alias-input')?.addEventListener('keydown', (event) => {
            if (event.key === 'Enter') {
                event.preventDefault();
                agregarAliasDesdeInput();
            }
        });

        document.getElementById('zona-nombre')?.addEventListener('input', (event) => {
            if (!codigoTocado) document.getElementById('zona-codigo').value = generarCodigo(event.target.value);
        });
        document.getElementById('zona-codigo')?.addEventListener('input', () => { codigoTocado = true; });

        document.querySelectorAll('[data-id-zona]').forEach(card => {
            card.addEventListener('click', () => editarZona(Number(card.dataset.idZona)));
        });

        document.querySelectorAll('.zona-coverage-item[data-id-inmueble]').forEach(item => {
            item.addEventListener('click', () => enfocarInmueble(Number(item.dataset.idInmueble)));
        });

        renderAliases();
        actualizarBotonTodasZonas();
        cargarGoogleMaps();
    });
})();
