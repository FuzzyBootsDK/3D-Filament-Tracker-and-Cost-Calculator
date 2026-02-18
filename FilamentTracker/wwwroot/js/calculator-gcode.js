function parseGcode(text) {
  const result = {
    seconds: null,
    grams: null,
    meters: null,
  };

  const timeMatch = text.match(/;TIME:(\d+)/);
  if (timeMatch) result.seconds = parseInt(timeMatch[1], 10);

  const gramsMatch = text.match(/Filament used \[g\]:\s*([0-9.]+)/i);
  if (gramsMatch) result.grams = parseFloat(gramsMatch[1]);

  const metersMatch = text.match(/Filament used:\s*([0-9.]+)\s*m/i);
  if (metersMatch) result.meters = parseFloat(metersMatch[1]);

  return result;
}

function attachGcodeHandlers() {
  const fileInput = document.getElementById("gcodeFile");
  const btn = document.getElementById("gcodeImportBtn");
  const statusEl = document.getElementById("gcodeStatus");

  if (!fileInput || !btn || !statusEl) return;

  btn.addEventListener("click", () => {
    const file = fileInput.files?.[0];
    if (!file) {
      statusEl.textContent = "Vælg en G‑code fil først.";
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      const text = e.target.result;
      const parsed = parseGcode(text);

      // TIME → hours + minutes
      if (parsed.seconds != null) {
        const totalMinutes = parsed.seconds / 60;
        const hours = Math.floor(totalMinutes / 60);
        const minutes = Math.round(totalMinutes % 60);

        document.getElementById("printHours").value = hours;
        document.getElementById("printMinutes").value = minutes;
      }

      // MATERIAL → first material row
      if (parsed.grams != null) {
        const firstWeight = document.querySelector(
          ".material-row input[data-role='weight']"
        );
        if (firstWeight) firstWeight.value = parsed.grams.toFixed(1);
      }

      statusEl.textContent = "G‑code indlæst – printtid og vægt opdateret.";
      if (typeof updateUI === "function") updateUI();
    };

    reader.readAsText(file);
  });
}

// Only initialize if we're on calculator page
if (document.getElementById("gcodeFile")) {
  if (document.readyState === 'loading') {
    document.addEventListener("DOMContentLoaded", attachGcodeHandlers);
  } else {
    attachGcodeHandlers();
  }
}
