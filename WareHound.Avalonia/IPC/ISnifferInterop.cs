namespace WareHound.Avalonia.IPC;

public interface ISnifferInterop : IDisposable
{
    List<string> GetDevices();
    void Initialize(int deviceIndex);
    void SelectDevice(int deviceIndex);
    void Start();
    void Stop();
    IntPtr GetSnifferHandle();
}
