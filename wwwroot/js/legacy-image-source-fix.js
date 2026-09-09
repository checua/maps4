(() => {
    const localHosts = new Set(['localhost', '127.0.0.1', '::1']);
    if (!localHosts.has(window.location.hostname.toLowerCase())) return;

    const publicAssetBaseUrl = 'https://rsmap.azurewebsites.net';

    function rewriteImage(img) {
        if (!(img instanceof HTMLImageElement)) return;
        if (img.dataset.rsmapsLegacyRemote === 'true') return;

        const rawSource = img.getAttribute('src') || '';
        if (!rawSource) return;

        let sourceUrl;
        try {
            sourceUrl = new URL(rawSource, window.location.origin);
        } catch {
            return;
        }

        if (!/^\/cargas\//i.test(sourceUrl.pathname)) return;
        if (sourceUrl.origin !== window.location.origin) return;

        img.dataset.rsmapsLegacyRemote = 'true';
        img.src = `${publicAssetBaseUrl}${sourceUrl.pathname}${sourceUrl.search}${sourceUrl.hash}`;
    }

    function scan(root) {
        if (!root) return;

        if (root instanceof HTMLImageElement) {
            rewriteImage(root);
            return;
        }

        root.querySelectorAll?.('img').forEach(rewriteImage);
    }

    // Reescribe cualquier foto legacy que ya exista al cargar este helper.
    scan(document);

    // GetCode1 agrega miniaturas con innerHTML de forma dinamica. MutationObserver
    // las redirige a produccion sin depender de que primero falle una solicitud local.
    const observer = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            mutation.addedNodes.forEach(node => {
                if (node.nodeType === Node.ELEMENT_NODE) scan(node);
            });
        }
    });

    observer.observe(document.documentElement, {
        childList: true,
        subtree: true
    });

    // Compatibilidad extra: si codigo legacy cambia el src de un IMG existente.
    document.addEventListener('error', event => {
        rewriteImage(event.target);
    }, true);
})();
