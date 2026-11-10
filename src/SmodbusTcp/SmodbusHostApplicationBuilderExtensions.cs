using System.Net;
using Microsoft.Extensions.Hosting;

namespace SmodbusTcp;

/// <summary>
/// Extensions for configuring raw TCP listeners on a host application builder.
/// </summary>
public static class SmodbusHostApplicationBuilderExtensions
{
    extension(IHostApplicationBuilder host)
    {
        /// <summary>
        /// Hosts a raw TCP Modbus listener. Register a handler with
        /// <see
        ///     cref="SmodbusServiceCollectionExtensions.AddSmodbusServer{THandler}(Microsoft.Extensions.DependencyInjection.IServiceCollection)" />
        /// first.
        /// </summary>
        /// <param name="port">The listening port (502 by default); zero requests an available port.</param>
        /// <returns>The supplied builder for chaining calls.</returns>
        /// <remarks>Binds to all IPv4 interfaces.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="port" /> is outside 0 to 65535.</exception>
        public IHostApplicationBuilder UseSmodbusTcpListener(int port = 502)
        {
            host.Services.AddSmodbusTcpListener(new IPEndPoint(IPAddress.Any, port));
            return host;
        }

        /// <summary>
        /// Hosts a raw TCP Modbus listener. Register a handler with
        /// <see
        ///     cref="SmodbusServiceCollectionExtensions.AddSmodbusServer{THandler}(Microsoft.Extensions.DependencyInjection.IServiceCollection)" />
        /// first.
        /// </summary>
        /// <param name="endpoint">The local address and port to bind; port zero requests an available port.</param>
        /// <returns>The supplied builder for chaining calls.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint" /> is null when services are configured.</exception>
        public IHostApplicationBuilder UseSmodbusTcpListener(IPEndPoint endpoint)
        {
            host.Services.AddSmodbusTcpListener(endpoint);
            return host;
        }
    }
}
