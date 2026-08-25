(() => {
    'use strict';

    let map;
    let zonaPolygon = null;
    let modoDibujo = false;
    let codigoTocado = false;
    let infoWindow = null;

    const defaultCenter = { lat: 24.0277, lng: -104.6532 };

    function cargarGoogleMaps() {
        if (window.google?.maps) {
            inicializarMapa();
            return;
        }

        const script = document.createElement('script');
        // Misma clave cliente que usa actualmente el mapa principal de RSMaps.
        const apiKey = 'AIzaSyAZ7HVHi9uywPRyEgtb9U-0Ul0C_v5zQXg';
        script.src = `https://maps.googleapis.com/maps/api/js?key=${apiKey}&v=weekly&loading=async`;
        script.async = true;
        script.defer = true;
        script.onload = inicializarMapa;
        script.onerror = () => setStatus('No fue posible cargar Google Maps.');
        document.head.appendChild(script);
    }

    function inicializarMapa() {
        map = new google.maps.Map(document.getElementById('zona-map'), {
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

        pins.forEach(pin => {
            const lat = Number(pin.lat);
            const lng = Number(pin.lng);
            if (!Number.isFinite(lat) || !Number.isFinite(lng)) return;

            const circle = new google.maps.Circle({
                map,
                center: { lat, lng },
                radius: 42,
                strokeColor: '#2563eb',
                strokeOpacity: 0.9,
                strokeWeight: 1,
                fillColor: '#2563eb',
                fillOpacity: 0.75,
                clickable: true,
                zIndex: 20
            });

            circle.addListener('click', () => {
                const precio = pin.precio ? Number(pin.precio).toLocaleString('es-MX', { style: 'currency', currency: 'MXN', maximumFractionDigits: 0 }) : 'Sin precio';
                infoWindow.setPosition({ lat, lng });
                infoWindow.setContent(`
                    <div style="min-width:180px">
                        <strong>#${pin.idInmueble} ${escapeHtml(pin.tipo || 'Inmueble')}</strong><br>
                        <span>${escapeHtml(pin.direccion || 'Sin dirección')}</span><br>
                        <span>${escapeHtml(precio)}</span>
                    </div>`);
                infoWindow.open({ map });
            });
        });
    }

    function iniciarDibujo() {
        if (!map) return;
        modoDibujo = true;
        map.setOptions({ draggableCursor: 'crosshair' });
        setStatus('Modo dibujo activo: haz clic para agregar vértices.');
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
        setStatus('Polígono limpio. Pulsa Dibujar para comenzar.');
    }

    function nuevaZona() {
        document.getElementById('zona-id').value = '';
        document.getElementById('zona-nombre').value = '';
        document.getElementById('zona-codigo').value = '';
        document.getElementById('zona-prioridad').value = '100';
        document.getElementById('zona-color').value = '#ef4444';
        document.getElementById('zona-descripcion').value = '';
        codigoTocado = false;
        limpiarPoligono();
        setStatus('Nueva zona. Escribe un nombre y dibuja el perímetro.');
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
            codigoTocado = true;

            const vertices = Array.isArray(zona.vertices) ? zona.vertices.map(v => ({ lat: Number(v.lat), lng: Number(v.lng) })) : [];
            if (vertices.length >= 3) {
                crearPoligono(vertices);
                ajustarMapaAlPoligono();
                setStatus(`${zona.nombre}: ${vertices.length} vértices. Puedes moverlos directamente.`);
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
        setStatus('Guardando zona y reclasificando inmuebles…');

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const payload = {
            idZona: idRaw ? Number(idRaw) : null,
            codigo,
            nombre,
            descripcion,
            prioridad,
            colorHex,
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
        document.getElementById('zona-color')?.addEventListener('input', aplicarColor);

        document.getElementById('zona-nombre')?.addEventListener('input', (event) => {
            if (!codigoTocado) document.getElementById('zona-codigo').value = generarCodigo(event.target.value);
        });
        document.getElementById('zona-codigo')?.addEventListener('input', () => { codigoTocado = true; });

        document.querySelectorAll('[data-id-zona]').forEach(card => {
            card.addEventListener('click', () => editarZona(Number(card.dataset.idZona)));
        });

        cargarGoogleMaps();
    });
})();
