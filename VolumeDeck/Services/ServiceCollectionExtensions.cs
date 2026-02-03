using Microsoft.Extensions.DependencyInjection;
using VolumeDeck.Services.Serial;

namespace VolumeDeck.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddVolumeDeckServices(this IServiceCollection services)
        {
            services.AddSingleton<SerialConnection>();
            services.AddSingleton<SessionVolumeController>();

            services.AddTransient<SerialPortFinder>();
            services.AddTransient<InputHandler>();

            services.AddHostedService<SerialWorker>();

            return services;
        }
    }
}
