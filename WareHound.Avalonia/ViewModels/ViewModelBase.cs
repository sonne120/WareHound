using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using WareHound.Avalonia.Services;

namespace WareHound.Avalonia.ViewModels;

public class ViewModelBase : ReactiveObject, IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _disposed;

    protected CompositeDisposable Disposables { get; } = new();
    protected ILoggerService? LoggerService { get; set; }

    protected bool IsDisposed => _disposed;

    protected CancellationToken CancellationToken =>
        (_cts ??= new CancellationTokenSource()).Token;

    [Reactive] public bool IsBusy { get; set; }
    [Reactive] public string ErrorMessage { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    protected ViewModelBase()
    {
        this.WhenAnyValue(x => x.ErrorMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasError)))
            .DisposeWith(Disposables);
    }

    protected ViewModelBase(ILoggerService logger) : this()
    {
        LoggerService = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    protected void CancelOperations()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
    
    protected void Log(string message)
    {
        LoggerService?.Log(message);
    }

    protected void LogDebug(string message)
    {
        LoggerService?.LogDebug(message);
    }

    protected void LogWarning(string message)
    {
        LoggerService?.LogWarning(message);
    }

    protected void LogError(string message, Exception? ex = null)
    {
        LoggerService?.LogError(message, ex);
    }

    protected void Subscribe<TMessage>(Action<TMessage> handler)
    {
        MessageBus.Current.Listen<TMessage>()
            .Subscribe(handler)
            .DisposeWith(Disposables);
    }


    protected void SubscribeOnUIThread<TMessage>(Action<TMessage> handler)
    {
        MessageBus.Current.Listen<TMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(handler)
            .DisposeWith(Disposables);
    }


    protected void Subscribe<TMessage>(Action<TMessage> handler, string? contract)
    {
        MessageBus.Current.Listen<TMessage>(contract)
            .Subscribe(handler)
            .DisposeWith(Disposables);
    }
    
    protected void SubscribeOnUIThread<TMessage>(Action<TMessage> handler, string? contract)
    {
        MessageBus.Current.Listen<TMessage>(contract)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(handler)
            .DisposeWith(Disposables);
    }
    
    protected static void Publish<TMessage>(TMessage message)
    {
        MessageBus.Current.SendMessage(message);
    }
    
    protected static void Publish<TMessage>(TMessage message, string? contract)
    {
        MessageBus.Current.SendMessage(message, contract);
    }

    protected void ClearError() => ErrorMessage = string.Empty;

    protected void SetError(string message) => ErrorMessage = message;

    protected void HandleException(Exception ex, string? context = null)
    {
        var message = context != null
            ? $"{context}: {ex.Message}"
            : ex.Message;
        SetError(message);
        LogError(message, ex);
    }

    protected async Task ExecuteAsync(
        Func<Task> operation,
        string? errorContext = null,
        bool showBusy = true)
    {
        if (showBusy) IsBusy = true;
        ClearError();

        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            //TODO
        }
        catch (Exception ex)
        {
            HandleException(ex, errorContext);
        }
        finally
        {
            if (showBusy) IsBusy = false;
        }
    }

    protected async Task<T?> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string? errorContext = null,
        bool showBusy = true)
    {
        if (showBusy) IsBusy = true;
        ClearError();

        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        catch (Exception ex)
        {
            HandleException(ex, errorContext);
            return default;
        }
        finally
        {
            if (showBusy) IsBusy = false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            OnDispose();
            CancelOperations();
            Disposables.Dispose();
        }

        _disposed = true;
    }

    protected virtual void OnDispose()
    {
    }
}
