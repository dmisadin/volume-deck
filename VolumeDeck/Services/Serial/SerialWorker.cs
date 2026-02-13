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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await serialConnection.IsSerialConnectionOpen())
                    await serialConnection.SendPing();
                else 
                    await serialConnection.Reconnect(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Serial connection check failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
