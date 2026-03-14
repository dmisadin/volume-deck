using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VolumeDeck.Services;

class Program
{
    public static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureLogging(logging => logging.AddEventLog())
            .ConfigureServices(services => { services.AddVolumeDeckServices(); })
            .Build();

        await host.RunAsync();
    }
}
