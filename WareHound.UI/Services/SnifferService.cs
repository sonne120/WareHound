using System.Collections.ObjectModel;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;
using WareHound.UI.IPC;
using WareHound.UI.Infrastructure.Services;
using WareHound.UI.Models;

namespace WareHound.UI.Services
{
    public class SnifferService : ISnifferService, IDisposable
    {
        private readonly ISnifferInterop _snifferInterop;
        private readonly ILoggerService _logger;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeWaitHandle CreateEventW(
            IntPtr lpEventAttributes,
            bool bManualReset,
            bool bInitialState,
            [MarshalAs(UnmanagedType.LPWStr)] string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetEvent(SafeWaitHandle hEvent);

        private const string PipeName = "testpipe";
        private const string EventName = "Global\\sniffer";
        private const int PipeConnectionTimeoutMs = 5000;
        private const int PipeServerStartDelayMs = 500;
        private const int DummyPacketId = 1000;
        private const int ChannelCapacity = 10000;

        private SafeWaitHandle? _eventHandle;
        private NamedPipeClientStream? _pipeClient;
        private Thread? _pipeReaderThread;
        private CancellationTokenSource? _cts;
        private volatile bool _isCapturing;
        private int _packetNumber;
        private bool _disposed;
        private int _selectedDeviceIndex = 1;
        
        // Channel for async packet streaming
        private Channel<PacketInfo>? _packetChannel;

        public ObservableCollection<NetworkDevice> Devices { get; } = new();
        public bool IsCapturing => _isCapturing;
        public int SelectedDeviceIndex => _selectedDeviceIndex;

        public event Action<string>? ErrorOccurred;

        public SnifferService(ISnifferInterop snifferInterop, ILoggerService logger)
        {
            _snifferInterop = snifferInterop ?? throw new ArgumentNullException(nameof(snifferInterop));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                _logger.LogDebug("SnifferService: Initializing...");
                LoadDevices();
                _logger.LogDebug("SnifferService: Initialization complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("SnifferService: Initialization failed", ex);
                throw;
            }
        }

        public void SelectDevice(int deviceIndex)
        {
            _selectedDeviceIndex = deviceIndex;
            _logger.LogDebug($"Device selected: {deviceIndex}");
        }

        public void StartCapture()
        {
            StartCapture(_selectedDeviceIndex);
        }

        public void LoadDevices()
        {
            try
            {
                _logger.LogDebug("Loading network devices...");
                Devices.Clear();

                var deviceNames = _snifferInterop.GetDevices();
                _logger.Log($"Found {deviceNames.Count} network devices");

                for (int i = 0; i < deviceNames.Count; i++)
                {
                    var device = ParseDeviceName(deviceNames[i], i);
                    Devices.Add(device);
                    _logger.LogDebug($"Device {i}: {device.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load devices", ex);
                ErrorOccurred?.Invoke($"Failed to load devices: {ex.Message}");
            }
        }

        public void StartCapture(int deviceIndex)
        {
            if (_isCapturing)
            {
                _logger.LogDebug("StartCapture called but already capturing");
                return;
            }

            try
            {
                _logger.Log($"Starting capture on device {deviceIndex}");
                InitializeCapture(deviceIndex);
                _logger.Log("Capture started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start capture", ex);
                ErrorOccurred?.Invoke($"Failed to start capture: {ex.Message}");
                StopCapture();
            }
        }

        public void StopCapture()
        {
            if (!_isCapturing)
            {
                return;
            }

            try
            {
                _logger.Log("Stopping capture...");
                CleanupCapture();
                _logger.Log("Capture stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error stopping capture", ex);
                ErrorOccurred?.Invoke($"Error stopping capture: {ex.Message}");
            }
        }

        public IntPtr GetSnifferHandle()
        {
            try
            {
                return _snifferInterop.GetSnifferHandle();
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private NetworkDevice ParseDeviceName(string name, int fallbackIndex)
        {
            int underscoreIdx = name.IndexOf('_');
            int index = fallbackIndex + 1;
            string description = name;

            if (underscoreIdx > 0 && int.TryParse(name[..underscoreIdx], out int parsedIdx))
            {
                index = parsedIdx;
                description = name[(underscoreIdx + 1)..];
            }

            return new NetworkDevice
            {
                Index = index,
                Name = name,
                Description = description
            };
        }

        private void InitializeCapture(int deviceIndex)
        {
            _packetNumber = 0;
            _cts = new CancellationTokenSource();

            // Create bounded channel for async packet streaming
            _packetChannel = Channel.CreateBounded<PacketInfo>(new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = false
            });

            // Create Windows Event for synchronization
            _eventHandle = CreateEventW(IntPtr.Zero, true, false, EventName);
            if (_eventHandle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to create synchronization event. Win32 Error: {error}");
            }

            // Start C++ sniffer thread
            Task.Run(() => _snifferInterop.Initialize(deviceIndex));

            // Wait for pipe server to start
            Thread.Sleep(PipeServerStartDelayMs);

            // Connect to named pipe
            _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            _pipeClient.Connect(PipeConnectionTimeoutMs);

            // Signal event to start capture
            SetEvent(_eventHandle);

            // Select device
            _snifferInterop.SelectDevice(deviceIndex);

            _isCapturing = true;

            // Start pipe reader thread
            _pipeReaderThread = new Thread(PipeReaderLoop)
            {
                IsBackground = true,
                Name = "PacketReaderThread"
            };
            _pipeReaderThread.Start();
        }

        private void CleanupCapture()
        {
            _isCapturing = false;
            _cts?.Cancel();

            // Complete the channel to signal consumers that no more packets will be written
            _packetChannel?.Writer.TryComplete();

            _snifferInterop.Stop();

            _pipeClient?.Close();
            _pipeClient?.Dispose();
            _pipeClient = null;

            _eventHandle?.Dispose();
            _eventHandle = null;

            _pipeReaderThread?.Join(1000);
            _pipeReaderThread = null;
        }

        private void PipeReaderLoop()
        {
            int structSize = Marshal.SizeOf<SnapshotStruct>();
            byte[] buffer = new byte[structSize];

            _logger.LogDebug($"PipeReaderLoop started, struct size = {structSize}");

            while (_isCapturing && !(_cts?.IsCancellationRequested ?? true))
            {
                try
                {
                    if (_pipeClient == null || !_pipeClient.IsConnected)
                    {
                        _logger.LogDebug("Pipe disconnected");
                        break;
                    }

                    int bytesRead = _pipeClient.Read(buffer, 0, structSize);

                    if (bytesRead == structSize)
                    {
                        ProcessPacketBuffer(buffer);
                    }
                    else if (bytesRead > 0)
                    {
                        _logger.LogDebug($"Incomplete read: {bytesRead} of {structSize} bytes");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("PipeReader error", ex);
                    if (_isCapturing) Thread.Sleep(10);
                }
            }

            _logger.LogDebug("PipeReaderLoop ended");
        }

        private void ProcessPacketBuffer(byte[] buffer)
        {
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var snapshot = Marshal.PtrToStructure<SnapshotStruct>(handle.AddrOfPinnedObject());

                if (snapshot.Id == DummyPacketId)
                    return;

                _packetNumber++;
                var packet = PacketInfo.FromSnapshot(snapshot, _packetNumber);
                
                // Write to channel 
                var written = _packetChannel?.Writer.TryWrite(packet) ?? false;
                
                if (_packetNumber <= 5 || _packetNumber % 100 == 0)
                {
                    _logger.LogDebug($"Packet #{_packetNumber} written to channel: {written}");
                }
            }
            finally
            {
                handle.Free();
            }
        }

        public async IAsyncEnumerable<IList<PacketInfo>> GetPacketBatchesAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = _packetChannel;
            var retries = 0;
            while (channel == null && retries < 10 && !ct.IsCancellationRequested)
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
                channel = _packetChannel;
                retries++;
            }

            if (channel == null)
            {
                _logger.LogDebug("GetPacketBatchesAsync: No active channel after waiting");
                yield break;
            }

            _logger.LogDebug("GetPacketBatchesAsync: Starting to read packets");
            var reader = channel.Reader;
            
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                var batch = new List<PacketInfo>();
                
                while (batch.Count < 100 && reader.TryRead(out var packet))
                {
                    batch.Add(packet);
                }

                if (batch.Count > 0)
                {
                    yield return batch;
                }
            }
            
            _logger.LogDebug("GetPacketBatchesAsync: Stream completed");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                StopCapture();

                try
                {
                    _snifferInterop.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error disposing sniffer interop", ex);
                }
            }
        }
    }
}
