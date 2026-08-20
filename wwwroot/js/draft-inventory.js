(() => {
    function getDraftId(card) {
        const idText = card.querySelector('.property-id')?.textContent || '';
        const match = idText.match(/\d+/);
        return match ? match[0] : null;
    }

    function enhanceDraftCards() {
        document.querySelectorAll('.property-card[data-state="BORRADOR"]').forEach(card => {
            if (card.dataset.draftEnhanced === 'true') return;

            const id = getDraftId(card);
            const footer = card.querySelector('.property-footer');
            if (!id || !footer) return;

            // El borrador no usa las acciones comerciales genéricas. Su acción
            // principal es completar información; publicar tendrá un flujo
            // explícito con validación de calidad.
            const legacyActions = footer.querySelector('.property-actions');
            if (legacyActions) legacyActions.remove();

            const readOnly = footer.querySelector('.readonly-label');
            const isOwner = card.dataset.owner === 'true';

            if (isOwner) {
                if (readOnly) readOnly.remove();

                const continueLink = document.createElement('a');
                continueLink.href = `/Borrador/Editar/${encodeURIComponent(id)}`;
                continueLink.textContent = 'Continuar captura →';
                continueLink.style.textDecoration = 'none';
                continueLink.style.fontSize = '12px';
                continueLink.style.fontWeight = '750';
                continueLink.style.color = '#fff';
                continueLink.style.background = '#17212b';
                continueLink.style.padding = '8px 11px';
                continueLink.style.borderRadius = '9px';
                continueLink.style.marginLeft = 'auto';
                footer.appendChild(continueLink);
            }

            card.dataset.draftEnhanced = 'true';
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', enhanceDraftCards);
    } else {
        enhanceDraftCards();
    }
})();
