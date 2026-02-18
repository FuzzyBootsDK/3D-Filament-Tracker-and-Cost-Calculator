/* ============================================================
   3D PRINT COST CALCULATOR — CLEAN REWRITE + PRINTER PROFILES
   ============================================================ */

/* ------------------------------------------------------------
   Helpers
------------------------------------------------------------ */

function formatDKK(value) {
  return (value || 0).toLocaleString("da-DK", {
    style: "currency",
    currency: "DKK",
    minimumFractionDigits: 2,
  });
}

function getNumber(id) {
  return parseFloat(document.getElementById(id).value) || 0;
}

function getInt(id) {
  return parseInt(document.getElementById(id).value, 10) || 0;
}

/* ------------------------------------------------------------
   Printer Profiles (Bambu Lab + structure for custom)
------------------------------------------------------------ */

const printerProfiles = {
  "Bambu X1C": {
    printerPrice: 8999,
    lifetimeYears: 4,
    uptimePercent: 85,
    maintenanceYearly: 600,
    powerWatt: 150,
    electricityPrice: 0.2,
    materialFactor: 1.12,
    bufferFactor: 1.25,
    printTimeMultiplier: 0.75,
  },
  "Bambu P1S": {
    printerPrice: 6499,
    lifetimeYears: 4,
    uptimePercent: 80,
    maintenanceYearly: 500,
    powerWatt: 140,
    electricityPrice: 0.2,
    materialFactor: 1.10,
    bufferFactor: 1.22,
    printTimeMultiplier: 0.8,
  },
  "Bambu A1": {
    printerPrice: 3999,
    lifetimeYears: 3,
    uptimePercent: 75,
    maintenanceYearly: 400,
    powerWatt: 130,
    electricityPrice: 0.2,
    materialFactor: 1.08,
    bufferFactor: 1.2,
    printTimeMultiplier: 0.9,
  },
  "Bambu A1 mini": {
    printerPrice: 2999,
    lifetimeYears: 3,
    uptimePercent: 70,
    maintenanceYearly: 350,
    powerWatt: 110,
    electricityPrice: 0.2,
    materialFactor: 1.05,
    bufferFactor: 1.18,
    printTimeMultiplier: 0.95,
  },
  "Bambu H2S": {
    printerPrice: 14999,
    lifetimeYears: 5,
    uptimePercent: 90,
    maintenanceYearly: 900,
    powerWatt: 220,
    electricityPrice: 0.2,
    materialFactor: 1.15,
    bufferFactor: 1.3,
    printTimeMultiplier: 0.7,
  },
  "Bambu H2D": {
    printerPrice: 16999,
    lifetimeYears: 5,
    uptimePercent: 92,
    maintenanceYearly: 1000,
    powerWatt: 230,
    electricityPrice: 0.2,
    materialFactor: 1.15,
    bufferFactor: 1.32,
    printTimeMultiplier: 0.68,
  },
  "Bambu H2C": {
    printerPrice: 18999,
    lifetimeYears: 5,
    uptimePercent: 93,
    maintenanceYearly: 1100,
    powerWatt: 240,
    electricityPrice: 0.2,
    materialFactor: 1.16,
    bufferFactor: 1.35,
    printTimeMultiplier: 0.65,
  },
};

// active profile (for time multiplier etc.)
let activePrinterProfile = null;

/* ------------------------------------------------------------
   Materials
------------------------------------------------------------ */

function createMaterialRow(name = "PLA", pricePerKg = 149, weightGrams = 0) {
  const row = document.createElement("div");
  row.className = "material-row";

  const nameInput = document.createElement("input");
  nameInput.type = "text";
  nameInput.value = name;
  nameInput.dataset.role = "name";

  const priceInput = document.createElement("input");
  priceInput.type = "number";
  priceInput.step = "1";
  priceInput.value = pricePerKg;
  priceInput.dataset.role = "price";

  const weightInput = document.createElement("input");
  weightInput.type = "number";
  weightInput.step = "1";
  weightInput.value = weightGrams;
  weightInput.dataset.role = "weight";

  const removeBtn = document.createElement("button");
  removeBtn.type = "button";
  removeBtn.textContent = "✕";
  removeBtn.className = "btn-secondary";
  removeBtn.style.padding = "0.2rem 0.4rem";
  removeBtn.addEventListener("click", () => {
    row.remove();
    updateUI();
  });

  [nameInput, priceInput, weightInput].forEach((el) =>
    el.addEventListener("input", updateUI)
  );

  row.appendChild(nameInput);
  row.appendChild(priceInput);
  row.appendChild(weightInput);
  row.appendChild(removeBtn);

  return row;
}

function getMaterials() {
  return [...document.querySelectorAll(".material-row")].map((row) => ({
    name: row.querySelector("input[data-role='name']").value || "Materiale",
    pricePerKg: parseFloat(row.querySelector("input[data-role='price']").value) || 0,
    weightGrams: parseFloat(row.querySelector("input[data-role='weight']").value) || 0,
  }));
}

/* ------------------------------------------------------------
   Core Cost Model (Batch‑accurate + depreciation + el)
------------------------------------------------------------ */

function calculateMachineRatePerHour() {
  const printerPrice = getNumber("printerPrice");
  const lifetimeYears = getNumber("printerLifetimeYears") || 3;
  const uptimePercent = getNumber("uptimePercent") || 50;
  const maintenanceYearly = getNumber("maintenanceYearly");
  const powerWatt = getNumber("powerWatt");
  const electricityPrice = getNumber("electricityPrice");

  const uptime = Math.max(0.1, uptimePercent / 100);
  const yearlyHours = 365 * 24 * uptime;
  const lifetimeHours = lifetimeYears * yearlyHours;

  const depreciationPerHour =
    lifetimeHours > 0 ? printerPrice / lifetimeHours : 0;
  const maintenancePerHour =
    yearlyHours > 0 ? maintenanceYearly / yearlyHours : 0;
  const electricityPerHour = (powerWatt / 1000) * electricityPrice;

  return depreciationPerHour + maintenancePerHour + electricityPerHour;
}

function calculateCosts() {
  const materials = getMaterials();

  // Print time per part
  const ph = getNumber("printHours");
  const pm = getNumber("printMinutes");
  let printHoursPerPart = ph + pm / 60;

  if (activePrinterProfile?.printTimeMultiplier) {
    printHoursPerPart *= activePrinterProfile.printTimeMultiplier;
  }

  // Handling time per job
  const lh = getNumber("laborHours");
  const lm = getNumber("laborMinutes");
  const handlingHours = lh + lm / 60;

  // Batch size
  const batchSize = Math.max(1, getInt("batchSize"));

  // Total print time
  const totalPrintHours = printHoursPerPart * batchSize;

  // Material cost
  const materialFactor = getNumber("materialFactor") || 1;
  let totalMaterialCost = 0;

  materials.forEach((m) => {
    const kg = (m.weightGrams / 1000) * materialFactor;
    totalMaterialCost += kg * m.pricePerKg * batchSize;
  });

  // Labor cost
  const hourlyRate = getNumber("hourlyRate");
  const laborCostTotal = handlingHours * hourlyRate;

  // Machine cost (depreciation + maintenance + el)
  const machineRate = calculateMachineRatePerHour();
  const machineCostTotal = totalPrintHours * machineRate;

  // Hardware + packaging (per unit)
  const hardwareTotal = getNumber("hardwareCost") * batchSize;
  const packagingTotal = getNumber("packagingCost") * batchSize;

  // Buffer
  const bufferFactor = getNumber("bufferFactor") || 1;

  // Total cost
  const totalCost =
    (totalMaterialCost +
      laborCostTotal +
      machineCostTotal +
      hardwareTotal +
      packagingTotal) *
    bufferFactor;

  // Per‑unit cost
  const baseCostPerUnit = totalCost / batchSize;

  return {
    batchSize,
    baseCostPerUnit,
    materialPerUnit: totalMaterialCost / batchSize,
    laborPerUnit: laborCostTotal / batchSize,
    machinePerUnit: machineCostTotal / batchSize,
    hardwarePerUnit: hardwareTotal / batchSize,
    packagingPerUnit: packagingTotal / batchSize,
  };
}

/* ------------------------------------------------------------
   Pricing Presets
------------------------------------------------------------ */

const pricingPresets = [
  { id: "competitive", name: "Konkurrencedygtig", margin: 25 },
  { id: "standard", name: "Standard", margin: 40 },
  { id: "premium", name: "Premium", margin: 60 },
  { id: "luxury", name: "Luksus", margin: 80 },
  { id: "custom", name: "Tilpasset", margin: null },
];

let selectedPresetId = "standard";

function buildPricingRows() {
  const list = document.getElementById("pricingList");
  list.innerHTML = "";

  pricingPresets.forEach((preset) => {
    const row = document.createElement("div");
    row.className = "pricing-row";
    row.dataset.id = preset.id;

    row.innerHTML = `
      <div class="pricing-label">
        <div class="pricing-name">${preset.name}</div>
        <div class="pricing-meta">${
          preset.id === "custom"
            ? "Sæt din egen vinstmargin manuelt"
            : `${preset.margin}% vinstmargin`
        }</div>
      </div>
      <div class="pricing-value" id="price-${preset.id}">
        <span>0,00 kr.</span>
        <span>inkl. moms</span>
      </div>
    `;

    row.addEventListener("click", () => {
      selectedPresetId = preset.id;
      updateUI();
    });

    list.appendChild(row);
  });
}

/* ------------------------------------------------------------
   Batch Optimization Table
------------------------------------------------------------ */

function updateBatchTable(costs) {
  const tbody = document.getElementById("batchTableBody");
  tbody.innerHTML = "";

  const tiers = [1, 5, 10, 25, 50, 100];
  const rows = [];

  tiers.forEach((qty) => {
    const unitCost = costs.baseCostPerUnit * (costs.batchSize / qty);
    const price = unitCost * 1.4; // 40% margin

    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${qty}</td>
      <td>${formatDKK(unitCost)}</td>
      <td>${formatDKK(price)}</td>
    `;
    tbody.appendChild(tr);

    rows.push({
      qty,
      unitCost: formatDKK(unitCost),
      price: formatDKK(price),
    });
  });

  return rows;
}

/* ------------------------------------------------------------
   PDF Export State
------------------------------------------------------------ */

function collectExportState() {
  const costs = calculateCosts();
  const vatRate = getNumber("vatRate") / 100;
  const customMargin = getNumber("customMargin");

  let selectedPrice = "0,00 kr.";
  let selectedMargin = "0%";

  pricingPresets.forEach((preset) => {
    const margin =
      preset.id === "custom" ? customMargin : preset.margin || 0;

    const priceExVat = costs.baseCostPerUnit * (1 + margin / 100);
    const priceIncVat = priceExVat * (1 + vatRate);

    if (preset.id === selectedPresetId) {
      selectedPrice = formatDKK(priceIncVat);
      selectedMargin = `${margin}%`;
    }
  });

  const batchRows = updateBatchTable(costs);

  return {
    partName: document.getElementById("partName").value || "",
    batchSize: costs.batchSize,
    unitCost: formatDKK(costs.baseCostPerUnit),
    selectedPrice,
    selectedMargin,
    costBreakdown: {
      material: formatDKK(costs.materialPerUnit),
      labor: formatDKK(costs.laborPerUnit),
      machine: formatDKK(costs.machinePerUnit),
      hardware: formatDKK(costs.hardwarePerUnit),
      packaging: formatDKK(costs.packagingPerUnit),
      total: formatDKK(costs.baseCostPerUnit),
    },
    batchTable: batchRows,
  };
}

/* ------------------------------------------------------------
   UI Update
------------------------------------------------------------ */

function updateUI() {
  const costs = calculateCosts();
  const vatRate = getNumber("vatRate") / 100;
  const customMargin = getNumber("customMargin");

  document.getElementById("batchLabel").textContent =
    costs.batchSize + " stk";
  document.getElementById("unitCostLabel").textContent =
    formatDKK(costs.baseCostPerUnit);

  document.getElementById("costMaterial").textContent = formatDKK(
    costs.materialPerUnit
  );
  document.getElementById("costLabor").textContent = formatDKK(
    costs.laborPerUnit
  );
  document.getElementById("costMachine").textContent = formatDKK(
    costs.machinePerUnit
  );
  document.getElementById("costHardware").textContent = formatDKK(
    costs.hardwarePerUnit
  );
  document.getElementById("costPackaging").textContent = formatDKK(
    costs.packagingPerUnit
  );
  document.getElementById("costTotal").textContent = formatDKK(
    costs.baseCostPerUnit
  );

  document.getElementById("customMarginLabel").textContent =
    customMargin + "%";

  updateBatchTable(costs);

  pricingPresets.forEach((preset) => {
    const row = document.querySelector(
      `.pricing-row[data-id="${preset.id}"]`
    );
    row.classList.toggle("selected", preset.id === selectedPresetId);

    const margin =
      preset.id === "custom" ? customMargin : preset.margin || 0;

    const priceExVat = costs.baseCostPerUnit * (1 + margin / 100);
    const priceIncVat = priceExVat * (1 + vatRate);

    const valueEl = document.getElementById(`price-${preset.id}`);
    valueEl.innerHTML = `${formatDKK(
      priceExVat
    )}<span>${formatDKK(priceIncVat)} inkl. moms</span>`;

    if (preset.id === selectedPresetId) {
      document.getElementById("selectedPrice").textContent =
        formatDKK(priceIncVat);
      document.getElementById("selectedMargin").textContent =
        margin + "%";
    }
  });
}

/* ------------------------------------------------------------
   Init
------------------------------------------------------------ */

function initCalculator() {
  // Default material
  document
    .getElementById("materialsBody")
    .appendChild(createMaterialRow());

  // Add material button
  document
    .getElementById("addMaterialBtn")
    .addEventListener("click", () => {
      document
        .getElementById("materialsBody")
        .appendChild(createMaterialRow());
      updateUI();
    });

  // Pricing rows
  buildPricingRows();

  // Printer profile selector
  document
    .getElementById("printerProfile")
    .addEventListener("change", () => {
      const name = document.getElementById("printerProfile").value;
      const profile = printerProfiles[name];
      activePrinterProfile = profile || null;
      if (!profile) {
        updateUI();
        return;
      }

      document.getElementById("printerPrice").value =
        profile.printerPrice;
      document.getElementById("printerLifetimeYears").value =
        profile.lifetimeYears;
      document.getElementById("uptimePercent").value =
        profile.uptimePercent;
      document.getElementById("maintenanceYearly").value =
        profile.maintenanceYearly;
      document.getElementById("powerWatt").value = profile.powerWatt;
      document.getElementById("electricityPrice").value =
        profile.electricityPrice;
      document.getElementById("materialFactor").value =
        profile.materialFactor;
      document.getElementById("bufferFactor").value =
        profile.bufferFactor;

      updateUI();
    });

  // Inputs
  document.querySelectorAll("input, select").forEach((el) => {
    el.addEventListener("input", updateUI);
  });

  // PDF export
  window.collectExportState = collectExportState;

  updateUI();
}

// Only initialize if we're on calculator page
if (document.getElementById("materialsBody")) {
  if (document.readyState === 'loading') {
    document.addEventListener("DOMContentLoaded", initCalculator);
  } else {
    initCalculator();
  }
}
