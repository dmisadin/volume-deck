using System.IO.Ports;

namespace VolumeDeck.Services.Serial;

public class SerialConnection
{
    private readonly InputHandler inputHandler;
    private readonly SerialPortFinder serialPortFinder;

    private const int BaudRate = 9600;

    private SerialPort? Port;
    private readonly object lockObj = new();

    public SerialConnection(InputHandler inputHandler, 
                            SerialPortFinder serialPortFinder)
    {
        this.inputHandler = inputHandler;
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
                this.inputHandler.HandleSerialLine(line);
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
}
