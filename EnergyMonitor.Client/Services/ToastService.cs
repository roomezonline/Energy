namespace EnergyMonitor.Client.Services;

public enum ToastType { Success, Error, Warning, Info }

public class ToastEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Message { get; set; } = "";
    public ToastType Type { get; set; } = ToastType.Info;
}

public class ToastService
{
    public event Action? OnChanged;
    public List<ToastEntry> Toasts { get; } = new();
    private readonly List<System.Timers.Timer> _timers = new();

    public void Show(string message, ToastType type = ToastType.Info, int durationMs = 4000)
    {
        var entry = new ToastEntry { Message = message, Type = type };
        Toasts.Add(entry);
        if (Toasts.Count > 5) Toasts.RemoveAt(0);
        OnChanged?.Invoke();

        var timer = new System.Timers.Timer(durationMs) { AutoReset = false };
        timer.Elapsed += (_, _) =>
        {
            Remove(entry.Id);
            timer.Dispose();
            lock (_timers) _timers.Remove(timer);
        };
        lock (_timers) _timers.Add(timer);
        timer.Start();
    }

    public void ShowSuccess(string message, int durationMs = 3000) => Show(message, ToastType.Success, durationMs);
    public void ShowError(string message, int durationMs = 5000) => Show(message, ToastType.Error, durationMs);
    public void ShowWarning(string message, int durationMs = 4000) => Show(message, ToastType.Warning, durationMs);
    public void ShowInfo(string message, int durationMs = 3000) => Show(message, ToastType.Info, durationMs);

    public void Remove(Guid id)
    {
        Toasts.RemoveAll(t => t.Id == id);
        OnChanged?.Invoke();
    }
}
