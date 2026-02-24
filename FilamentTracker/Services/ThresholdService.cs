using FilamentTracker.Models;

namespace FilamentTracker.Services;

public class ThresholdService
{
    private decimal _lowThreshold = 500;
    private decimal _criticalThreshold = 250;
    
    public event Action? OnThresholdsChanged;
    
    public decimal LowThreshold => _lowThreshold;
    public decimal CriticalThreshold => _criticalThreshold;
    
    public void SetThresholds(decimal low, decimal critical)
    {
        _lowThreshold = low;
        _criticalThreshold = critical;
        OnThresholdsChanged?.Invoke();
    }
    
    public string GetStatus(decimal weightRemaining)
    {
        if (weightRemaining < _criticalThreshold) return "critical";
        if (weightRemaining < _lowThreshold) return "low";
        return "ok";
    }
}
