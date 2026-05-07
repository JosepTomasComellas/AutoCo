// ── Drag & Drop: gestió síncrona del dragover sense round-trip a Blazor ──────
// En Blazor Server, preventDefault() en ondragover arriba tard (xarxa).
// Afegim un listener global que crida preventDefault de forma síncrona
// sempre que el cursor estigui sobre un element .dnd-zone o fill seu.
document.addEventListener('dragover', function (e) {
    if (e.target.closest && e.target.closest('.dnd-zone')) {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
    }
});

// Drag ghost: mostra només el chip arrossegat, no tot el grup.
// Usem un clon fora del contenidor scroll perquè alguns browsers capturen
// tot el contenidor desbordable si l'element és dins d'overflow-y:auto.
(function () {
    var _ghost = null;

    document.addEventListener('dragstart', function (e) {
        var chip = e.target.closest && e.target.closest('.dnd-chip');
        if (!chip) return;

        if (_ghost) { _ghost.remove(); _ghost = null; }

        _ghost = chip.cloneNode(true);
        // Eliminar el botó X del ghost: l'amplada extra desplaça el cursor del centre
        var closeBtn = _ghost.querySelector('button');
        if (closeBtn) closeBtn.remove();
        _ghost.style.cssText = 'position:fixed;top:-1000px;left:0;margin:0;'
            + 'width:auto;opacity:1;pointer-events:none;z-index:9999;';
        document.body.appendChild(_ghost);

        // Hotspot al centre del ghost resultant (sense el botó)
        e.dataTransfer.setDragImage(_ghost, _ghost.offsetWidth / 2, _ghost.offsetHeight / 2);
    }, true);

    document.addEventListener('dragend', function () {
        if (_ghost) { _ghost.remove(); _ghost = null; }
    }, true);
}());

// ── Canvi de cultura (i18n) ───────────────────────────────────────────────────
// Escriu la cookie .AspNetCore.Culture i recarrega la pàgina per aplicar-la.
window.setCulture = function (culture) {
    const expiry = new Date();
    expiry.setFullYear(expiry.getFullYear() + 1);
    document.cookie = `.AspNetCore.Culture=c=${culture}|uic=${culture}; expires=${expiry.toUTCString()}; path=/; SameSite=Lax`;
    location.reload();
};

window.downloadBase64File = function (base64, fileName, mimeType) {
    const bytes  = atob(base64);
    const buffer = new Uint8Array(bytes.length);
    for (let i = 0; i < bytes.length; i++) buffer[i] = bytes.charCodeAt(i);
    const blob = new Blob([buffer], { type: mimeType });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
