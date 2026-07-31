window.chartInstances = {};

var phaseColors = [
    { border: '#f97316', fill: 'rgba(249,115,22,0.08)', crosshair: 'rgba(249,115,22,0.12)' },
    { border: '#3b82f6', fill: 'rgba(59,130,246,0.08)', crosshair: 'rgba(59,130,246,0.12)' },
    { border: '#ef4444', fill: 'rgba(239,68,68,0.08)', crosshair: 'rgba(239,68,68,0.12)' }
];

function createOrUpdateChart(id, labels, datasets, unit) {
    var ctx = document.getElementById(id);
    if (!ctx) return;

    if (window.chartInstances[id]) {
        var chart = window.chartInstances[id];
        if (!document.body.contains(chart.canvas)) {
            chart.destroy();
            delete window.chartInstances[id];
        } else {
            chart.data.labels = labels;
            chart.data.datasets.forEach(function(ds, i) {
                if (datasets[i]) {
                    ds.data = datasets[i].data;
                    ds.label = datasets[i].label;
                }
            });
            chart.update('none');
            return;
        }
    }

    var chartDatasets = [];
    for (var i = 0; i < datasets.length; i++) {
        var c = phaseColors[i % phaseColors.length];
        chartDatasets.push({
            label: datasets[i].label,
            data: datasets[i].data,
            borderColor: c.border,
            backgroundColor: c.fill,
            borderWidth: 3,
            pointRadius: 0,
            pointHitRadius: 10,
            pointHoverRadius: 5,
            pointHoverBackgroundColor: c.border,
            pointHoverBorderColor: '#fff',
            pointHoverBorderWidth: 3,
            tension: 0.3,
            fill: true,
            cubicInterpolationMode: 'monotone'
        });
    }

    window.chartInstances[id] = new Chart(ctx, {
        type: 'line',
        data: { labels: labels, datasets: chartDatasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 800, easing: 'easeOutQuart' },
            transitions: {
                active: { animation: { duration: 200 } }
            },
            interaction: {
                intersect: false,
                mode: 'index',
                axis: 'x'
            },
            plugins: {
                legend: {
                    position: 'top',
                    rtl: true,
                    align: 'end',
                    labels: {
                        color: '#64748b',
                        font: { size: 11, family: 'Vazirmatn', weight: '600' },
                        boxWidth: 14, boxHeight: 3,
                        padding: 16,
                        usePointStyle: true,
                        pointStyle: 'line'
                    }
                },
                tooltip: {
                    backgroundColor: '#fff',
                    titleColor: '#1e293b',
                    bodyColor: '#475569',
                    borderColor: '#e2e8f0',
                    borderWidth: 1,
                    cornerRadius: 8,
                    padding: 12,
                    bodyFont: { size: 12, family: 'Vazirmatn' },
                    titleFont: { size: 11, family: 'Vazirmatn', weight: '700' },
                    boxWidth: 10, boxHeight: 4,
                    boxPadding: 4,
                    displayColors: true,
                    callbacks: {
                        label: function(ctx) {
                            return ctx.dataset.label + ': ' + ctx.parsed.y.toFixed(1) + (unit ? ' ' + unit : '');
                        }
                    }
                }
            },
            scales: {
                x: {
                    display: true,
                    grid: { color: 'rgba(0,0,0,0.04)', drawBorder: false },
                    border: { display: false },
                    ticks: {
                        color: '#94a3b8',
                        font: { size: 10, family: 'Vazirmatn' },
                        maxTicksLimit: 8,
                        maxRotation: 0
                    }
                },
                y: {
                    display: true,
                    grid: { color: 'rgba(0,0,0,0.04)', drawBorder: false },
                    border: { display: false },
                    ticks: {
                        color: '#94a3b8',
                        font: { size: 10, family: 'Vazirmatn' },
                        padding: 8,
                        maxTicksLimit: 6,
                        callback: function(val) { var formatted = parseFloat(val.toFixed(4)); return formatted + (unit ? ' ' + unit : ''); }
                    }
                }
            },
            hover: {
                mode: 'index',
                intersect: false
            }
        }
    });
}
