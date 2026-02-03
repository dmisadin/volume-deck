using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VolumeDeck.Services.Serial;

public sealed class SerialWorker : BackgroundService
{
    private readonly SerialConnection serialConnection;
    private readonly ILogger<SerialWorker> logger;

    public SerialWorker(SerialConnection serialConnection, ILogger<SerialWorker> logger)
    {
        this.serialConnection = serialConnection;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SerialWorker starting...");

        await serialConnection.FindSerialPortAndStartListeningAsync(stoppingToken);
    }
}
