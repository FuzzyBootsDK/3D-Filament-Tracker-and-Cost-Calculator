// Lightweight "PDF" export using print dialog.
// Opens a new window with a printable offer layout.
// User can then "Gem som PDF" in browser.

function buildOfferHtml(state) {
  const {
    partName,
    unitCost,
    selectedPrice,
    selectedMargin,
    batchSize,
    costBreakdown,
    batchTable,
  } = state;

  const rows = batchTable
    .map(
      (row) => `
      <tr>
        <td>${row.qty}</td>
        <td>${row.unitCost}</td>
        <td>${row.price}</td>
      </tr>`
    )
    .join("");

  return `
  <html lang="da">
  <head>
    <meta charset="UTF-8" />
    <title>Tilbud – ${partName || "3D‑print"}</title>
    <style>
      body { font-family: system-ui, -apple-system, sans-serif; padding: 24px; color: #111827; }
      h1 { font-size: 20px; margin-bottom: 4px; }
      h2 { font-size: 16px; margin-top: 18px; margin-bottom: 4px; }
      table { width: 100%; border-collapse: collapse; font-size: 13px; }
      th, td { padding: 4px 6px; border-bottom: 1px solid #e5e7eb; text-align: left; }
      .muted { color: #6b7280; font-size: 12px; }
      .summary { margin-top: 8px; padding: 8px; border-radius: 8px; background: #f9fafb; }
      .summary-row { display: flex; justify-content: space-between; font-size: 13px; }
    </style>
  </head>
  <body>
    <h1>Tilbud på 3D‑print</h1>
    <p class="muted">Genereret fra din omkostningsberegner</p>

    <h2>Projekt</h2>
    <p><strong>Delnavn:</strong> ${partName || "Ikke angivet"}</p>
    <p><strong>Batch størrelse:</strong> ${batchSize} stk</p>

    <h2>Prissætning</h2>
    <div class="summary">
      <div class="summary-row">
        <span>Enhedsomkostning (eks. moms)</span>
        <span>${unitCost}</span>
      </div>
      <div class="summary-row">
        <span>Valgt pris (inkl. moms)</span>
        <span>${selectedPrice}</span>
      </div>
      <div class="summary-row">
        <span>Valgt margin</span>
        <span>${selectedMargin}</span>
      </div>
    </div>

    <h2>Omkostningsfordeling (pr. enhed)</h2>
    <table>
      <tbody>
        <tr><td>Materiale</td><td>${costBreakdown.material}</td></tr>
        <tr><td>Arbejdsløn</td><td>${costBreakdown.labor}</td></tr>
        <tr><td>Maskine</td><td>${costBreakdown.machine}</td></tr>
        <tr><td>Hardware</td><td>${costBreakdown.hardware}</td></tr>
        <tr><td>Emballage</td><td>${costBreakdown.packaging}</td></tr>
        <tr><td><strong>Total</strong></td><td><strong>${costBreakdown.total}</strong></td></tr>
      </tbody>
    </table>

    <h2>Batch‑optimering</h2>
    <table>
      <thead>
        <tr>
          <th>Antal</th>
          <th>Enhedsomkostning</th>
          <th>Pris (40% margin)</th>
        </tr>
      </thead>
      <tbody>
        ${rows}
      </tbody>
    </table>
  </body>
  </html>
  `;
}

function exportOfferToPdf(state) {
  const html = buildOfferHtml(state);
  const win = window.open("", "_blank");
  if (!win) return;
  win.document.open();
  win.document.write(html);
  win.document.close();
  win.focus();
  win.print();
}

function attachPdfExportHandler() {
  const btn = document.getElementById("exportPdfBtn");
  if (!btn) return;
  btn.addEventListener("click", () => {
    if (typeof collectExportState === "function") {
      const state = collectExportState();
      exportOfferToPdf(state);
    }
  });
}

// Only initialize if we're on calculator page
if (document.getElementById("exportPdfBtn")) {
  if (document.readyState === 'loading') {
    document.addEventListener("DOMContentLoaded", attachPdfExportHandler);
  } else {
    attachPdfExportHandler();
  }
}
