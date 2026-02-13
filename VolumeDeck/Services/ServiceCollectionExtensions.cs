using Microsoft.Extensions.DependencyInjection;
using VolumeDeck.Services.Serial;

namespace VolumeDeck.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddVolumeDeckServices(this IServiceCollection services)
        {
            services.AddSingleton<SerialConnection>();

            services.AddTransient<SerialPortFinder>();

            services.AddHostedService<SerialWorker>();
            services.AddHostedService<SessionVolumeController>();

            return services;
        }
    }
}
