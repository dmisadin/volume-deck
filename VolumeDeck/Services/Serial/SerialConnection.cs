using NAudio.CoreAudioApi;
using System.IO.Ports;
using System.Text;
using VolumeDeck.Models.Enums;

namespace VolumeDeck.Services.Serial;

public class SerialConnection : IAsyncDisposable
{
    private readonly SerialPortFinder serialPortFinder;
    private readonly ILogger<SerialConnection> logger;

    private const int BaudRate = 9600;
    private const byte SOF = 0xAA;

    private SerialPort? Port;
    private readonly SemaphoreSlim ConnectLock = new(1, 1);
    private DateTime LastPongReceived = DateTime.MinValue;

    public event Action<string>? SerialLineReceived;

    public bool IsConnected => this.Port != null && this.Port.IsOpen;


    public SerialConnection(SerialPortFinder serialPortFinder,
							ILogger<SerialConnection> logger)
    {
        this.serialPortFinder = serialPortFinder;
        this.logger = logger;
    }

    public async Task FindSerialPortAndStartListeningAsync(CancellationToken cancellationToken)
    {
        string serialPortName = await this.serialPortFinder.FindSerialPortNameAsync(cancellationToken);

        this.StartSerialListening(serialPortName);
    }

    public void StartSerialListening(string port)
    {
        this.Port = new SerialPort(port, BaudRate)
        {
            NewLine = "\n",
            ReadTimeout = 500
        };

        this.Port.DataReceived += (_, __) =>
        {
            try
            {
                string line = this.Port!.ReadLine().Trim();
                logger.LogInformation("Serial.DataRecevied: " + line);
                this.LastPongReceived = DateTime.UtcNow;
                SerialLineReceived?.Invoke(line);
            }
            catch { }
        };

        try
        {
            this.Port.Open();
            this.LastPongReceived = DateTime.UtcNow;
            logger.LogInformation($"Listening on {port} @ {BaudRate} baud");
		}
		catch (Exception ex)
        {
            logger.LogError("Failed to open port:");
			logger.LogError(ex.Message);
			logger.LogError("Tip: Close Arduino Serial Monitor/Plotter (only one app can use the COM port).");
            return;
        }
    }

    // [SOF][TYPE][LENGTH][STRING]
    public async Task SendFrame(FrameType type, string payload)
    {
        await this.IsSerialConnectionOpen();

        byte[] data = Encoding.UTF8.GetBytes(payload ?? "");

        if (data.Length > 62)
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload too long (max 62 bytes).");

        byte[] frame = new byte[3 + data.Length];
        frame[0] = SOF;
        frame[1] = (byte)type;
        frame[2] = (byte)data.Length;
        Array.Copy(data, 0, frame, 3, data.Length);

        this.Port?.Write(frame, 0, frame.Length);
    }

    public async Task<bool> IsSerialConnectionOpen()
    {
        var age = DateTime.UtcNow - LastPongReceived;

        if (this.Port != null 
            && this.Port.IsOpen 
            && LastPongReceived != DateTime.MinValue 
            && age < TimeSpan.FromSeconds(15))
        {
            return true;
        }

        return false;
    }

    public async Task Reconnect(CancellationToken ct)
    {
        await this.ConnectLock.WaitAsync(ct);

        try
        {
            this.Close();

            await FindSerialPortAndStartListeningAsync(ct);

            if (this.Port == null || !this.Port.IsOpen)
                throw new InvalidOperationException("Serial port could not be opened.");
        }
        catch
        {
        }
        finally
        {
            this.ConnectLock.Release();
        }
    }

    public async Task SendPing()
    {
        await this.SendFrame(FrameType.Ping, "");
    }

    public void Close()
    {
        try
        {
            DisposePort();
        }
        catch { }
        finally
        {
            this.LastPongReceived = DateTime.MinValue;
        }
    }
    private void DisposePort()
    {
        if (Port != null)
        {
            if (Port.IsOpen)
                Port.Close();
            Port.Dispose();
            Port = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        DisposePort();
        await Task.CompletedTask;
    }
}
