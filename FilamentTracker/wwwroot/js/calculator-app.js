/* ============================================================
   3D PRINT COST CALCULATOR — WITH INVENTORY INTEGRATION
   ============================================================ */

/* ------------------------------------------------------------
   Helpers
------------------------------------------------------------ */

let currentCurrency = "DKK";
let inventoryFilaments = [];

function formatCurrency(value) {
  return (value || 0).toLocaleString(undefined, {
    style: "currency",
    currency: currentCurrency,
    minimumFractionDigits: 2,
  });
}

function getNumber(id) {
  const el = document.getElementById(id);
  return el ? parseFloat(el.value) || 0 : 0;
}

function getInt(id) {
  const el = document.getElementById(id);
  return el ? parseInt(el.value, 10) || 0 : 0;
}

// Called from Blazor to initialize
window.initCalculatorWithInventory = function(data) {
  console.log("Initializing calculator with inventory data:", data);
  currentCurrency = data.currency || "DKK";
  inventoryFilaments = data.inventory || [];
  console.log("Inventory filaments loaded:", inventoryFilaments.length);
  
  setTimeout(() => {
    const materialsBody = document.getElementById("materialsBody");
    console.log("Materials body found:", materialsBody !== null);
    if (materialsBody) {
      initCalculator();
      console.log("Calculator initialized");
    }
  }, 100);
};


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

function createMaterialRow(selectedId = null, pricePerKg = 149, weightGrams = 0) {
  const row = document.createElement("div");
  row.className = "material-row";

  // Create dropdown with inventory
  const select = document.createElement("select");
  select.className = "calculator-material-select";
  
  // Add manual entry option
  const manualOption = document.createElement("option");
  manualOption.value = "";
  manualOption.textContent = "Manual Entry";
  select.appendChild(manualOption);
  
  // Add inventory filaments
  inventoryFilaments.forEach(filament => {
    const option = document.createElement("option");
    option.value = filament.id;
    option.textContent = filament.name;
    if (selectedId === filament.id) {
      option.selected = true;
    }
    select.appendChild(option);
  });

  const priceInput = document.createElement("input");
  priceInput.type = "number";
  priceInput.step = "0.01";
  priceInput.className = "calculator-material-input";
  priceInput.value = pricePerKg;
  priceInput.placeholder = "Price/kg";
  priceInput.dataset.role = "price";

  const weightInput = document.createElement("input");
  weightInput.type = "number";
  weightInput.step = "1";
  weightInput.className = "calculator-material-input";
  weightInput.value = weightGrams;
  weightInput.placeholder = "Weight (g)";
  weightInput.dataset.role = "weight";

  const removeBtn = document.createElement("button");
  removeBtn.type = "button";
  removeBtn.textContent = "✕";
  removeBtn.className = "calculator-btn-secondary";
  removeBtn.style.padding = "0.2rem 0.4rem";
  removeBtn.addEventListener("click", () => {
    row.remove();
    updateUI();
  });

  // Handle filament selection
  select.addEventListener("change", () => {
    const selectedFilamentId = select.value;
    if (selectedFilamentId) {
      // Find selected filament and populate price
      const filament = inventoryFilaments.find(f => f.id == selectedFilamentId);
      if (filament) {
        priceInput.value = filament.pricePerKg;
        priceInput.disabled = true;
        priceInput.style.opacity = "0.7";
        priceInput.style.cursor = "not-allowed";
      }
    } else {
      // Manual entry mode
      priceInput.disabled = false;
      priceInput.style.opacity = "1";
      priceInput.style.cursor = "text";
    }
    updateUI();
  });

  // Add input listeners
  [priceInput, weightInput].forEach(el => 
    el.addEventListener("input", updateUI)
  );

  row.appendChild(select);
  row.appendChild(priceInput);
  row.appendChild(weightInput);
  row.appendChild(removeBtn);

  return row;
}

function getMaterials() {
  return [...document.querySelectorAll(".material-row")].map((row) => {
    const select = row.querySelector("select");
    const priceInput = row.querySelector("input[data-role='price']");
    const weightInput = row.querySelector("input[data-role='weight']");
    
    const selectedFilamentId = select ? select.value : null;
    let name = "Manual Entry";
    
    if (selectedFilamentId) {
      const filament = inventoryFilaments.find(f => f.id == selectedFilamentId);
      if (filament) {
        name = filament.name;
      }
    }
    
    return {
      name: name,
      pricePerKg: parseFloat(priceInput?.value) || 0,
      weightGrams: parseFloat(weightInput?.value) || 0,
    };
  });
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
  { id: "competitive", name: "Competitive", margin: 25 },
  { id: "standard", name: "Standard", margin: 40 },
  { id: "premium", name: "Premium", margin: 60 },
  { id: "luxury", name: "Luxury", margin: 80 },
  { id: "custom", name: "Custom", margin: null },
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
            ? "Set your own profit margin manually"
            : `${preset.margin}% profit margin`
        }</div>
      </div>
      <div class="pricing-value" id="price-${preset.id}">
        <span>0.00</span>
        <span>incl. VAT</span>
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
    // Recalculate total cost for this batch size
    const totalMaterialCost = costs.materialPerUnit * qty;
    const totalLaborCost = costs.laborPerUnit * qty;
    const totalMachineCost = costs.machinePerUnit * qty;
    const totalHardwareCost = costs.hardwarePerUnit * qty;
    const totalPackagingCost = costs.packagingPerUnit * qty;
    const bufferFactor = costs.bufferFactor || 1;
    const totalCost = (totalMaterialCost + totalLaborCost + totalMachineCost + totalHardwareCost + totalPackagingCost) * bufferFactor;
    const unitCost = totalCost / qty;
    const price = unitCost * 1.4; // 40% margin

    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${qty}</td>
      <td>${formatCurrency(unitCost)}</td>
      <td>${formatCurrency(price)}</td>
    `;
    tbody.appendChild(tr);

    rows.push({
      qty,
      unitCost: formatCurrency(unitCost),
      price: formatCurrency(price),
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

  let selectedPrice = "0.00";
  let selectedMargin = "0%";

  pricingPresets.forEach((preset) => {
    const margin =
      preset.id === "custom" ? customMargin : preset.margin || 0;

    const priceExVat = costs.baseCostPerUnit * (1 + margin / 100);
    const priceIncVat = priceExVat * (1 + vatRate);

    if (preset.id === selectedPresetId) {
      selectedPrice = formatCurrency(priceIncVat);
      selectedMargin = `${margin}%`;
    }
  });

  const batchRows = updateBatchTable(costs);

  return {
    partName: document.getElementById("partName").value || "",
    batchSize: costs.batchSize,
    unitCost: formatCurrency(costs.baseCostPerUnit),
    selectedPrice,
    selectedMargin,
    costBreakdown: {
      material: formatCurrency(costs.materialPerUnit),
      labor: formatCurrency(costs.laborPerUnit),
      machine: formatCurrency(costs.machinePerUnit),
      hardware: formatCurrency(costs.hardwarePerUnit),
      packaging: formatCurrency(costs.packagingPerUnit),
      total: formatCurrency(costs.baseCostPerUnit),
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
    costs.batchSize + " pcs";
  document.getElementById("unitCostLabel").textContent =
    formatCurrency(costs.baseCostPerUnit);

  document.getElementById("costMaterial").textContent = formatCurrency(
    costs.materialPerUnit
  );
  document.getElementById("costLabor").textContent = formatCurrency(
    costs.laborPerUnit
  );
  document.getElementById("costMachine").textContent = formatCurrency(
    costs.machinePerUnit
  );
  document.getElementById("costHardware").textContent = formatCurrency(
    costs.hardwarePerUnit
  );
  document.getElementById("costPackaging").textContent = formatCurrency(
    costs.packagingPerUnit
  );
  document.getElementById("costTotal").textContent = formatCurrency(
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
    valueEl.innerHTML = `${formatCurrency(
      priceExVat
    )}<span>${formatCurrency(priceIncVat)} incl. VAT</span>`;

    if (preset.id === selectedPresetId) {
      document.getElementById("selectedPrice").textContent =
        formatCurrency(priceIncVat);
      document.getElementById("selectedMargin").textContent =
        margin + "%";
    }
  });
}

/* ------------------------------------------------------------
   Init
------------------------------------------------------------ */

function initCalculator() {
  // Add default material row
  const materialsBody = document.getElementById("materialsBody");
  if (materialsBody) {
    materialsBody.appendChild(createMaterialRow());
  }

  // Wire up add material button
  const addBtn = document.getElementById("addMaterialBtn");
  if (addBtn) {
    addBtn.addEventListener("click", () => {
      document.getElementById("materialsBody").appendChild(createMaterialRow());
      updateUI();
    });
  }

  // Pricing rows
  buildPricingRows();

  // Printer profile selector
  const profileSelect = document.getElementById("printerProfile");
  if (profileSelect) {
    profileSelect.addEventListener("change", () => {
      const name = profileSelect.value;
      const profile = printerProfiles[name];
      activePrinterProfile = profile || null;
      if (!profile) {
        updateUI();
        return;
      }

      document.getElementById("printerPrice").value = profile.printerPrice;
      document.getElementById("printerLifetimeYears").value = profile.lifetimeYears;
      document.getElementById("uptimePercent").value = profile.uptimePercent;
      document.getElementById("maintenanceYearly").value = profile.maintenanceYearly;
      document.getElementById("powerWatt").value = profile.powerWatt;
      document.getElementById("electricityPrice").value = profile.electricityPrice;
      document.getElementById("materialFactor").value = profile.materialFactor;
      document.getElementById("bufferFactor").value = profile.bufferFactor;

      updateUI();
    });
  }

  // Input listeners for all inputs and selects
  document.querySelectorAll("input, select").forEach((el) => {
    if (!el.dataset.hasListener) {
      el.addEventListener("input", updateUI);
      el.dataset.hasListener = "true";
    }
  });

  // PDF export
  window.collectExportState = collectExportState;

  updateUI();
}
