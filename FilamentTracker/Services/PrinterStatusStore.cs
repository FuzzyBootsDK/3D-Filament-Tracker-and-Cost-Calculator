using FilamentTracker.Models;

namespace FilamentTracker.Services;

/// <summary>
/// Central in-memory store for live printer statuses.
/// Subscribes once to BambuLabService and fans updates out to UI components.
/// </summary>
public sealed class PrinterStatusStore : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(200);
    private readonly BambuLabService _bambuLabService;
    private readonly object _lock = new();
    private Dictionary<int, PrintStatus> _statuses;
    private readonly Dictionary<int, PrintStatus> _pending = new();
    private CancellationTokenSource? _debounceCts;

    public event Action<int, PrintStatus>? OnStatusChanged;

    public PrinterStatusStore(BambuLabService bambuLabService)
    {
        _bambuLabService = bambuLabService;
        _statuses = _bambuLabService.GetAllPrinterStatuses();
        _bambuLabService.OnStatusUpdated += HandleStatusUpdated;
    }

    public Dictionary<int, PrintStatus> GetAllStatuses()
    {
        lock (_lock)
        {
            return new Dictionary<int, PrintStatus>(_statuses);
        }
    }

    public bool IsPrinterConnected(int printerId) => _bambuLabService.IsPrinterConnected(printerId);

    private void HandleStatusUpdated(int printerId, PrintStatus status)
    {
        lock (_lock)
        {
            _statuses[printerId] = status;
            _pending[printerId] = status;

            // One scheduled dispatch window at a time; new updates coalesce into _pending.
            if (_debounceCts == null)
            {
                _debounceCts = new CancellationTokenSource();
                _ = DispatchDebouncedAsync(_debounceCts.Token);
            }
        }
    }

    private async Task DispatchDebouncedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceInterval, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Dictionary<int, PrintStatus> snapshot;
        lock (_lock)
        {
            snapshot = new Dictionary<int, PrintStatus>(_pending);
            _pending.Clear();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        try
        {
            foreach (var (printerId, status) in snapshot)
                OnStatusChanged?.Invoke(printerId, status);
        }
        catch
        {
            // Never let a subscriber exception break status fan-out.
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
            _pending.Clear();
        }

        _bambuLabService.OnStatusUpdated -= HandleStatusUpdated;
    }
}

