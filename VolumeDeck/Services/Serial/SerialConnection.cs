using System.IO.Ports;
using System.Text;
using VolumeDeck.Models.Enums;

namespace VolumeDeck.Services.Serial;

public class SerialConnection
{
    private readonly SerialPortFinder serialPortFinder;

    private const int BaudRate = 9600;
    private const byte SOF = 0xAA;

    private SerialPort? Port;
    private readonly object lockObj = new();

    public event Action<string>? SerialLineReceived;

    public SerialConnection(SerialPortFinder serialPortFinder)
    {
        this.serialPortFinder = serialPortFinder;
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
                SerialLineReceived?.Invoke(line);
            }
            catch { }
        };

        try
        {
            this.Port.Open();
            Console.WriteLine($"Listening on {port} @ {BaudRate} baud");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open port:");
            Console.WriteLine(ex.Message);
            Console.WriteLine("Tip: Close Arduino Serial Monitor/Plotter (only one app can use the COM port).");
            return;
        }

        // this.Port.Close();
    }

    // [SOF][TYPE][LENGTH][STRING]
    public void SendFrame(FrameType type, string payload)
    {
        if (this.Port == null || !this.Port.IsOpen)
            throw new NullReferenceException("Serial Port is not open.");

        byte[] data = Encoding.UTF8.GetBytes(payload ?? "");

        if (data.Length > 62)
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload too long (max 62 bytes).");

        byte[] frame = new byte[3 + data.Length];
        frame[0] = SOF;
        frame[1] = (byte)type;
        frame[2] = (byte)data.Length;
        Array.Copy(data, 0, frame, 3, data.Length);

        this.Port.Write(frame, 0, frame.Length);
    }
}
