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
