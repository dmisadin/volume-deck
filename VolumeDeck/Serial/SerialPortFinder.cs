using System.IO.Ports;

namespace VolumeDeck.Serial;

public class SerialPortFinder : IDisposable
{
    private readonly object _lock = new();

    private readonly int _baudRate;
    private readonly TimeSpan _scanInterval;
    private readonly TimeSpan _handshakeTimeout;

    private Timer? _timer;

    // Keep track of ports we already tried recently to avoid hammering the same dead port.
    private readonly Dictionary<string, DateTime> _nextAllowedAttemptUtc = new();

    public string? ConnectedPortName { get; private set; }

    public event Action<string>? OnConnected;
    public event Action<string>? OnLog;

    private const string HandshakeKey = "VOLUME_KNOB_READY";
    private const string HandshakeQuestion = "VOLUME_KNOB_REQUEST";

    public SerialPortFinder(
        int baudRate = 9600,
        TimeSpan? scanInterval = null,
        TimeSpan? handshakeTimeout = null)
    {
        _baudRate = baudRate;
        _scanInterval = scanInterval ?? TimeSpan.FromSeconds(1);
        _handshakeTimeout = handshakeTimeout ?? TimeSpan.FromMilliseconds(600);
    }

    public void Start()
    {
        _timer = new Timer(_ => ScanOnce(), null, TimeSpan.Zero, _scanInterval);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void ScanOnce()
    {
        try
        {
            // Already connected? Optionally you can verify it's still present.
            // For now: if connected and still exists, do nothing.
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();

            lock (_lock)
            {
                if (ConnectedPortName != null && ports.Contains(ConnectedPortName, StringComparer.OrdinalIgnoreCase))
                    return;
            }

            foreach (var port in ports)
            {
                if (!ShouldAttempt(port))
                    continue;

                if (TryHandshake(port, out var response))
                {
                    lock (_lock)
                    {
                        ConnectedPortName = port;
                    }

                    OnLog?.Invoke($"Connected to {port} ({response})");
                    OnConnected?.Invoke(port);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ScanOnce] {ex.Message}");
        }
    }

    private bool ShouldAttempt(string port)
    {
        lock (_lock)
        {
            if (_nextAllowedAttemptUtc.TryGetValue(port, out var nextUtc))
            {
                if (DateTime.UtcNow < nextUtc)
                    return false;
            }

            // Attempt now; if fails, back off for a bit
            _nextAllowedAttemptUtc[port] = DateTime.UtcNow.AddSeconds(2);
            return true;
        }
    }

    private bool TryHandshake(string portName, out string deviceResponse)
    {
        deviceResponse = "";

        try
        {
            using var sp = new SerialPort(portName, _baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 150,
                WriteTimeout = 150,

                // Reduce chance of resetting Arduino on open (varies by driver/device)
                DtrEnable = false,
                RtsEnable = false
            };

            sp.Open();

            // Clear any buffered garbage
            try { sp.DiscardInBuffer(); } catch { }
            try { sp.DiscardOutBuffer(); } catch { }

            // Send request
            sp.WriteLine(HandshakeQuestion);

            // Read until timeout window expires
            var deadline = DateTime.UtcNow + _handshakeTimeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var line = sp.ReadLine().Trim();
                    if (line.Equals(HandshakeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        deviceResponse = line;
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    // keep trying until deadline
                }
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            OnLog?.Invoke($"[Handshake] {portName} access denied (busy).");
            return false;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[Handshake] {portName} failed: {ex.Message}");
            return false;
        }
    }
}