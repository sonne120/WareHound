using System.Collections.Concurrent;
using WareHound.Avalonia.Services.BackgroundJobs.AsyncCollection;

namespace WareHound.Avalonia.Services.BackgroundJobs;

public interface IBackgroundJobs<T>
{
    ConcurrentQueue<T> BackgroundTasks { get; set; }
    AsyncConcurrencyQueue<T> BackgroundTaskGrpc { get; set; }

    void CleanBackgroundTask()
    {
        BackgroundTasks.Clear();
        BackgroundTaskGrpc.Clear();
    }
}

public class BackgroundJobs<T> : IBackgroundJobs<T>
{
    public ConcurrentQueue<T> BackgroundTasks { get; set; } = new();
    public AsyncConcurrencyQueue<T> BackgroundTaskGrpc { get; set; } = new();
}
