using System.IO.Ports;

namespace VolumeDeck.Services.Serial;

public class SerialPortFinder : IDisposable
{
    private readonly object _lock = new();

    private readonly int BaudRate;
    private readonly TimeSpan ScanInterval;
    private readonly TimeSpan HandshakeTimeout;

    // Keep track of ports we already tried recently to avoid hammering the same dead port.
    private readonly Dictionary<string, DateTime> NextAllowedAttemptUtc = new();
    private const string HandshakeKey = "VOLUME_KNOB_READY";
    private const string HandshakeQuestion = "VOLUME_KNOB_REQUEST";

    public SerialPortFinder(
        int baudRate = 9600,
        TimeSpan? scanInterval = null,
        TimeSpan? handshakeTimeout = null)
    {
        this.BaudRate = baudRate;
        this.ScanInterval = scanInterval ?? TimeSpan.FromSeconds(1);
        this.HandshakeTimeout = handshakeTimeout ?? TimeSpan.FromMilliseconds(600);
    }

    public async Task<string> FindSerialPortNameAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();

            foreach (var port in ports)
            {
                if (!ShouldAttempt(port))
                    continue;

                if (TryHandshake(port, out var response))
                {
                    return port;   // found it
                }
            }

            await Task.Delay(this.ScanInterval, ct); // wait 1 second
        }

        throw new OperationCanceledException(ct);
    }


    public void Dispose()
    {
    }

    private bool ShouldAttempt(string port)
    {
        lock (_lock)
        {
            if (this.NextAllowedAttemptUtc.TryGetValue(port, out var nextUtc))
            {
                if (DateTime.UtcNow < nextUtc)
                    return false;
            }

            // Attempt now; if fails, back off for a bit
            this.NextAllowedAttemptUtc[port] = DateTime.UtcNow.AddSeconds(2);
            return true;
        }
    }

    private bool TryHandshake(string portName, out string deviceResponse)
    {
        deviceResponse = "";

        try
        {
            using var sp = new SerialPort(portName, this.BaudRate)
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
            var deadline = DateTime.UtcNow + this.HandshakeTimeout;
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
            Console.WriteLine($"[Handshake] {portName} access denied (busy).");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Handshake] {portName} failed: {ex.Message}");
            return false;
        }
    }
}