(() => {
    const form = document.getElementById('draftEditForm');
    if (!form) return;

    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const idInmueble = Number(form.querySelector('input[name="IdInmueble"]')?.value || 0);
    if (!idInmueble || !token) return;

    document.addEventListener('click', async (event) => {
        const button = event.target.closest('.photo-buttons button[formaction*="MoverFoto"]');
        if (!button) return;

        event.preventDefault();
        event.stopPropagation();

        if (button.disabled) return;

        const action = new URL(button.formAction, window.location.origin);
        const idImagen = Number(action.searchParams.get('idImagen') || 0);
        const direccion = Number(action.searchParams.get('direccion') || 0);
        if (!idImagen || ![-1, 1].includes(direccion)) {
            showPhotoError('No fue posible identificar la foto que quieres mover.');
            return;
        }

        button.disabled = true;
        button.dataset.originalText = button.textContent || '';
        button.textContent = '…';

        try {
            const body = new FormData();
            body.append('__RequestVerificationToken', token);
            body.append('idInmueble', String(idInmueble));
            body.append('idImagen', String(idImagen));
            body.append('direccion', String(direccion));

            const response = await fetch('/BorradorFoto/Mover', {
                method: 'POST',
                credentials: 'same-origin',
                body
            });

            let data = null;
            try { data = await response.json(); } catch (_) { }

            if (!response.ok || !data?.success)
                throw new Error(data?.message || 'No fue posible reordenar la foto.');

            window.location.reload();
        } catch (error) {
            button.disabled = false;
            button.textContent = button.dataset.originalText || (direccion < 0 ? '←' : '→');
            showPhotoError(error.message || 'No fue posible reordenar la foto.');
        }
    }, true);

    function showPhotoError(message) {
        let box = document.getElementById('photoActionInlineError');
        if (!box) {
            box = document.createElement('div');
            box.id = 'photoActionInlineError';
            box.style.margin = '0 0 14px';
            box.style.padding = '10px 12px';
            box.style.border = '1px solid #f5c2c0';
            box.style.borderRadius = '10px';
            box.style.background = '#fff3f2';
            box.style.color = '#b42318';
            box.style.fontSize = '13px';

            const photoSection = Array.from(document.querySelectorAll('.section'))
                .find(section => section.querySelector('.photo-grid, .photo-empty'));
            photoSection?.prepend(box);
        }
        if (box) box.textContent = `⚠ ${message}`;
    }
})();
