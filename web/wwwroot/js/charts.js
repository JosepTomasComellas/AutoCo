// AutoCo — Chart.js interop helpers
window.autocoCharts = (() => {
    const _instances = {};

    function destroy(canvasId) {
        if (_instances[canvasId]) {
            _instances[canvasId].destroy();
            delete _instances[canvasId];
        }
    }

    function renderBar(canvasId, labels, series) {
        destroy(canvasId);

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const datasets = series.map((s, i) => ({
            label: s.label,
            data: s.data,
            backgroundColor: s.color + 'cc',
            borderColor: s.color,
            borderWidth: 1.5,
            borderRadius: 4,
        }));

        const isDark = document.body.classList.contains('app-dark') ||
                       document.querySelector('.app-dark') !== null;

        const gridColor = isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)';
        const textColor = isDark ? '#94a3b8' : '#475569';

        _instances[canvasId] = new Chart(canvas, {
            type: 'bar',
            data: { labels, datasets },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                plugins: {
                    legend: {
                        labels: { color: textColor, boxWidth: 14, padding: 16 }
                    },
                    tooltip: {
                        callbacks: {
                            label: ctx => {
                                const v = ctx.raw;
                                return v === null || v === undefined
                                    ? `${ctx.dataset.label}: —`
                                    : `${ctx.dataset.label}: ${Number(v).toFixed(1)}`;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        min: 0, max: 10,
                        ticks: { stepSize: 2, color: textColor },
                        grid:  { color: gridColor }
                    },
                    x: {
                        ticks: { color: textColor },
                        grid:  { color: gridColor }
                    }
                }
            }
        });
    }

    return { renderBar, destroy };
})();
