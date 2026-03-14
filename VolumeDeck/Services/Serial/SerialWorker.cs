using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VolumeDeck.Services.Serial;

public sealed class SerialWorker : BackgroundService
{
    private readonly SerialConnection serialConnection;
    private readonly ILogger<SerialWorker> logger;

    public SerialWorker(SerialConnection serialConnection, ILogger<SerialWorker> logger)
    {
        this.serialConnection = serialConnection ?? throw new ArgumentNullException(nameof(serialConnection));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SerialWorker starting.");

        stoppingToken.Register(() =>
        {
            logger.LogInformation("SerialWorker stopping, disposing SerialConnection...");
            serialConnection.DisposeAsync().AsTask().Wait();
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Checking serial connection.");

                if (await serialConnection.IsSerialConnectionOpen())
                    await serialConnection.SendPing();
                else 
                    await serialConnection.Reconnect(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogError("Operation canceled.");
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
