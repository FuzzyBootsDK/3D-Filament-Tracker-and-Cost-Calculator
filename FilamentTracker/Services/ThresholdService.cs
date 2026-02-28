namespace FilamentTracker.Services;

public class ThresholdService
{
    public decimal LowThreshold { get; private set; } = 500;

    public decimal CriticalThreshold { get; private set; } = 250;

    public event Action? OnThresholdsChanged;

    public void SetThresholds(decimal low, decimal critical)
    {
        LowThreshold = low;
        CriticalThreshold = critical;
        OnThresholdsChanged?.Invoke();
    }

    public string GetStatus(decimal weightRemaining)
    {
        if (weightRemaining < CriticalThreshold) return "critical";
        if (weightRemaining < LowThreshold) return "low";
        return "ok";
    }
}