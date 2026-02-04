namespace WareHound.Avalonia.IPC.Grpc;

public interface IHostedGrpcService
{
    Task StartAsync(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
