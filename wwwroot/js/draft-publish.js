(() => {
    const form = document.getElementById('draftEditForm');
    if (!form) return;

    const checks = Array.from(document.querySelectorAll('.checklist .check'));
    const listo = checks.length >= 6 && checks.every(x => x.classList.contains('done'));
    const aviso = document.querySelector('.publish-disabled');
    const acciones = form.querySelector('.save-actions');

    if (aviso) {
        if (listo) {
            aviso.textContent = '✓ Lista para publicar. Al publicar será visible en el marketplace y comenzará su historial comercial confiable.';
            aviso.style.background = '#eef9f1';
            aviso.style.color = '#24613a';
            aviso.style.border = '1px solid #cfe8d6';
        } else {
            aviso.textContent = 'Completa los elementos pendientes antes de publicar. Guardar cambios nunca hace pública la propiedad.';
        }
    }

    if (!listo || !acciones || document.getElementById('btnPublishDraft')) return;

    const boton = document.createElement('button');
    boton.type = 'submit';
    boton.id = 'btnPublishDraft';
    boton.className = 'btn-save primary';
    boton.textContent = 'Publicar propiedad';
    boton.formAction = '/Publicacion/Publicar';
    boton.style.background = '#24613a';

    boton.addEventListener('click', (event) => {
        const confirmado = window.confirm(
            '¿Publicar esta propiedad?\n\n' +
            'Será visible en el marketplace público y comenzará su historial comercial. Las notas privadas seguirán siendo internas.'
        );

        if (!confirmado) event.preventDefault();
    });

    acciones.appendChild(boton);
})();
