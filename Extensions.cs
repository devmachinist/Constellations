using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;

namespace Devmachinist.Constellations.AspNetCore
{
    public static class Extensions
    {
        /// <summary>
        /// Configure dependency injection for a singleton instance of a constellation.
        /// </summary>
        /// <param name="services">This IServiceCollection</param>
        /// <param name="configureOptions">Configure the Constellation for use</param>
        /// <returns>This IServiceCollection</returns>
        public static IServiceCollection UseConstellation(this IServiceCollection services, Action<Constellation> configureOptions, X509Certificate2? cert = null)
        {
                // Configure Constellation options
                var options = new Constellation();
                if (cert != null)
            {
                options.SetX509Cert(cert);
            }
                configureOptions(options);

                // Register Constellation as a singleton
                services.AddSingleton<Constellation>(sp =>
                {
                    return options;
                });
            return services;
        }
        /// <summary>
        /// Configure dependency injection for scoped instances of constellations. Meant for client usage only.
        /// </summary>
        /// <param name="services">This IServiceCollection</param>
        /// <param name="configureOptions">Configure the scoped Constellation</param>
        /// <returns>This IServiceCollection</returns>
        public static IServiceCollection UseConstellations(this IServiceCollection services, Action<Constellation> configureOptions, X509Certificate2? cert = null)
        {
                // Configure Constellation options

                // Register Constellation as a scoped object
                services.AddScoped<Constellation>(sp =>
                {
                    var name = Guid.NewGuid().ToString();
                    var options = new Constellation(name);
                    if(cert != null)
                    {
                        options.SetX509Cert(cert);
                    }
                    configureOptions(options);
                    return options;
                });
            return services;
        }
    }
}
