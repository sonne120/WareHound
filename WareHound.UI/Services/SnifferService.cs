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
        private const string EventName = "Local\\sniffer";
        private const int PipeConnectionTimeoutMs = 8000;
        private const int PipeServerStartDelayMs = 500;
        private const int DummyPacketId = 1000;
        private const int ChannelCapacity = 10000;

        private SafeWaitHandle? _eventHandle;
        private NamedPipeClientStream? _pipeClient;
        private Thread? _pipeReaderThread;
        private CancellationTokenSource? _cts;
        private volatile bool _isCapturing;
        private volatile bool _isStartingCapture;
        private int _packetNumber;
        private bool _disposed;
        private int _selectedDeviceIndex = 1;

        private Channel<PacketInfo>? _packetChannel;
        private bool _isLoadingDevices;

        public ObservableCollection<NetworkDevice> Devices { get; } = new();
        public bool IsCapturing => _isCapturing;
        public int SelectedDeviceIndex => _selectedDeviceIndex;
        public bool IsLoadingDevices => _isLoadingDevices;

        public event Action<string>? ErrorOccurred;
        public event Action? DevicesLoaded;
        public event Action? DevicesLoadingStarted;

        public SnifferService(ISnifferInterop snifferInterop, ILoggerService logger)
        {
            _snifferInterop = snifferInterop ?? throw new ArgumentNullException(nameof(snifferInterop));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogDebug("SnifferService: Initialized (devices will be loaded on demand)");
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
                _isLoadingDevices = true;
                DevicesLoadingStarted?.Invoke();

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

                DevicesLoaded?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load devices", ex);
                ErrorOccurred?.Invoke($"Failed to load devices: {ex.Message}");
            }
            finally
            {
                _isLoadingDevices = false;
            }
        }

        public async Task LoadDevicesAsync(CancellationToken cancellationToken = default)
        {
            await LoadDevicesAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }

        public Task LoadDevicesAsync(TimeSpan timeout)
        {
            return LoadDevicesAsync(timeout, CancellationToken.None);
        }

        public async Task LoadDevicesAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_isLoadingDevices)
            {
                _logger.LogDebug("LoadDevicesAsync: Already loading, skipping");
                return;
            }

            try
            {
                _isLoadingDevices = true;
                DevicesLoadingStarted?.Invoke();

                _logger.LogDebug("Loading network devices asynchronously...");

                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                List<string> deviceNames;
                try
                {
                    deviceNames = await Task.Run(() => _snifferInterop.GetDevices(), linkedCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException($"Device enumeration timed out after {timeout.TotalSeconds} seconds");
                }

                _logger.Log($"Found {deviceNames.Count} network devices");


                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    Devices.Clear();
                    for (int i = 0; i < deviceNames.Count; i++)
                    {
                        var device = ParseDeviceName(deviceNames[i], i);
                        Devices.Add(device);
                        _logger.LogDebug($"Device {i}: {device.DisplayName}");
                    }
                });

                DevicesLoaded?.Invoke();
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Device loading was cancelled");
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.LogError("Device loading timed out", ex);
                ErrorOccurred?.Invoke(ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load devices asynchronously", ex);
                ErrorOccurred?.Invoke($"Failed to load devices: {ex.Message}");
                throw;
            }
            finally
            {
                _isLoadingDevices = false;
            }
        }

        public void StartCapture(int deviceIndex)
        {
            if (_isCapturing || _isStartingCapture)
            {
                _logger.LogDebug("StartCapture called but already capturing or starting");
                return;
            }

            _isStartingCapture = true;
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
            finally
            {
                _isStartingCapture = false;
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


            _packetChannel = Channel.CreateBounded<PacketInfo>(new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = false
            });


            _eventHandle = CreateEventW(IntPtr.Zero, true, false, EventName);
            if (_eventHandle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to create synchronization event. Win32 Error: {error}");
            }

            _snifferInterop.Initialize(deviceIndex);
            Thread.Sleep(PipeServerStartDelayMs);

            //SetEvent(_eventHandle);

            _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.In);
            _pipeClient.Connect(PipeConnectionTimeoutMs);

            _snifferInterop.SelectDevice(deviceIndex);
            _snifferInterop.Start();

            _isCapturing = true;

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

            _packetChannel?.Writer.TryComplete();

            _snifferInterop.Stop();
            _snifferInterop.Reset();

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
            int headerSize = Marshal.SizeOf<SnapshotHeader>();
            byte[] headerBuffer = new byte[headerSize];

            _logger.LogDebug($"PipeReaderLoop started, header size = {headerSize}");

            while (_isCapturing && !(_cts?.IsCancellationRequested ?? true))
            {
                try
                {
                    if (_pipeClient == null || !_pipeClient.IsConnected)
                    {
                        _logger.LogDebug("Pipe disconnected");
                        break;
                    }

                    int bytesReadTotal = 0;
                    while (bytesReadTotal < headerSize)
                    {
                        int read = _pipeClient.Read(headerBuffer, bytesReadTotal, headerSize - bytesReadTotal);
                        if (read == 0)
                        {
                            _logger.LogDebug("Pipe EOF — server closed connection");
                            return;
                        }
                        bytesReadTotal += read;
                    }

                    ProcessPacketBuffer(headerBuffer);
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
                var header = Marshal.PtrToStructure<SnapshotHeader>(handle.AddrOfPinnedObject());

                if (header.Id == DummyPacketId)
                {
                    DrainPayload(header.CaptureLen);
                    return;
                }

                if (header.CaptureLen > 65536)
                {
                    _logger.LogWarning($"[PipeReader] Invalid frame capture length: {header.CaptureLen}. Skipping.");
                    return;
                }

                byte[]? rawData = null;
                if (header.CaptureLen > 0)
                {
                    rawData = new byte[header.CaptureLen];
                    int totalRead = 0;
                    while (totalRead < header.CaptureLen)
                    {
                        if (_pipeClient == null) break;
                        int read = _pipeClient.Read(rawData, totalRead, (int)header.CaptureLen - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }
                }

                var snapshot = header.ToSnapshot(rawData);

                _packetNumber++;
                var packet = PacketInfo.FromSnapshot(snapshot, _packetNumber);

                var written = _packetChannel?.Writer.TryWrite(packet) ?? false;

                if (_packetNumber <= 5 || _packetNumber % 100 == 0)
                {
                    _logger.LogDebug($"Packet #{_packetNumber} {packet.Protocol} {packet.SourceIp}:{packet.SourcePort} -> {packet.DestIp}:{packet.DestPort} (written: {written})");
                }
            }
            finally
            {
                handle.Free();
            }
        }

        private void DrainPayload(uint length)
        {
            if (length == 0 || _pipeClient == null) return;
            byte[] sink = new byte[Math.Min(length, 4096u)];
            uint remaining = length;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, (uint)sink.Length);
                int read = _pipeClient.Read(sink, 0, toRead);
                if (read == 0) break;
                remaining -= (uint)read;
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
