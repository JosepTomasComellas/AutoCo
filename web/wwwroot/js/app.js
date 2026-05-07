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

        var rect = chip.getBoundingClientRect();
        _ghost = chip.cloneNode(true);
        _ghost.style.cssText = 'position:fixed;top:-1000px;left:0;margin:0;'
            + 'width:' + rect.width + 'px;opacity:1;pointer-events:none;z-index:9999;';
        document.body.appendChild(_ghost);

        // Hotspot = posició exacta on l'usuari ha agafat el chip
        var hotX = e.clientX - rect.left;
        var hotY = e.clientY - rect.top;
        e.dataTransfer.setDragImage(_ghost, hotX, hotY);
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
