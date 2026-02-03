using Microsoft.Extensions.Hosting;
using VolumeDeck.Services;

class Program
{
    public static async Task Main(string[] args)
    {

        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services => { services.AddVolumeDeckServices(); })
            .Build();

        await host.RunAsync();
    }
}
