using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace SmodbusTcp;

/// <summary>
/// Extensions for configuring SmodbusTcp connection handling with Kestrel.
/// </summary>
/// <remarks>Register the shared protocol server and handler with AddSmodbusServer before starting the host.</remarks>
public static class KestrelExtensions
{
    extension(KestrelServerOptions kestrel)
    {
        /// <summary>
        /// Adds a Modbus TCP endpoint to Kestrel without configuring an HTTP application pipeline.
        /// </summary>
        /// <remarks>
        /// The optional endpoint configuration runs before the Modbus connection handler is added.
        /// </remarks>
        /// <param name="port">The listening port (502 by default); zero requests an available port.</param>
        /// <param name="configure">Optional endpoint configuration, such as TLS, applied before adding the Modbus handler.</param>
        /// <returns>The supplied options instance for chaining calls.</returns>
        public KestrelServerOptions UseSmodbusTcp(
            int port = 502,
            Action<ListenOptions>? configure = null)
        {
            kestrel.ListenAnyIP(port, listen =>
            {
                configure?.Invoke(listen);
                listen.UseSmodbusTcp();
            });
            return kestrel;
        }
    }

    extension(IWebHostBuilder web)
    {
        /// <summary>
        /// Adds a Modbus TCP endpoint to Kestrel without changing the HTTP application pipeline.
        /// </summary>
        /// <remarks>
        /// Use this overload when the host also serves HTTP. The optional endpoint configuration
        /// runs before the Modbus connection handler is added.
        /// </remarks>
        /// <param name="port">The listening port (502 by default); zero requests an available port.</param>
        /// <param name="configure">Optional endpoint configuration, such as TLS, applied before adding the Modbus handler.</param>
        /// <returns>The supplied builder for chaining calls.</returns>
        public IWebHostBuilder UseSmodbusTcp(
            int port = 502,
            Action<ListenOptions>? configure = null) =>
            web.UseKestrel().ConfigureKestrel(k => k.UseSmodbusTcp(port, configure));
    }

    extension(IHostBuilder host)
    {
        /// <summary>
        /// Configures a dedicated generic host to serve Modbus TCP with Kestrel.
        /// </summary>
        /// <remarks>
        /// This convenience overload installs an empty HTTP application pipeline. Do not use it
        /// when the host also serves HTTP; configure the endpoint with
        /// <see cref="KestrelServerOptions" /> and the Kestrel-options overload instead.
        /// The optional endpoint configuration runs before the Modbus connection handler is added.
        /// </remarks>
        /// <param name="port">The listening port (502 by default); zero requests an available port.</param>
        /// <param name="configure">Optional endpoint configuration, such as TLS, applied before adding the Modbus handler.</param>
        /// <returns>The supplied builder for chaining calls.</returns>
        public IHostBuilder UseSmodbusKestrel(
            int port = 502,
            Action<ListenOptions>? configure = null
        )
        {
            return host.ConfigureWebHost(web =>
            {
                web.UseSmodbusTcp(port, configure);
                web.Configure(_ => { });
            });
        }
    }

    extension(IConnectionBuilder connectionBuilder)
    {
        /// <summary>
        /// Adds the SmodbusTcp connection handler to a Kestrel endpoint.
        /// </summary>
        /// <returns>The supplied builder for chaining calls.</returns>
        public IConnectionBuilder UseSmodbusTcp() =>
            connectionBuilder.UseConnectionHandler<ModbusConnectionHandler>();
    }
}
