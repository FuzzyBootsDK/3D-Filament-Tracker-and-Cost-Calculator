using System.Collections.Generic;

namespace FilamentTracker.Models
{
    // Represents an AMS device reported by the printer
    public class PrinterAmsDevice
    {
        public int Id { get; set; }
        public int? ActiveSlot { get; set; }
        public List<PrinterAmsSlot> Slots { get; set; } = new List<PrinterAmsSlot>();
    }

    // Represents a single AMS slot
    public class PrinterAmsSlot
    {
        public int Slot { get; set; }
        public string? Material { get; set; }
        public string? Brand { get; set; }
        public string? Type { get; set; }
        public string? Color { get; set; }
        public int? Humidity { get; set; }
        public int? Temp { get; set; }
        // External id provided by the printer (e.g., Bambu Lab spool id)
        public string? ExternalId { get; set; }
    }
}
