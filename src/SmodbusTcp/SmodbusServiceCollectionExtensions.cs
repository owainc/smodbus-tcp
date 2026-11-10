using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmodbusTcp;

/// <summary>
/// Dependency-injection registration extensions for the SmodbusTcp server.
/// </summary>
public static class SmodbusServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the shared server and function handler. Repeating this call with the same handler is harmless.
        /// </summary>
        /// <remarks>The server and handler are shared by all listeners. The handler must support concurrent requests.</remarks>
        /// <typeparam name="THandler">The handler implementation registered as a singleton.</typeparam>
        /// <returns>The supplied service collection for chaining calls.</returns>
        /// <exception cref="InvalidOperationException">
        /// A different registration already exists for <see cref="IModbusFunctionHandler" />.
        /// </exception>
        public IServiceCollection AddSmodbusServer<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            THandler>()
            where THandler : class, IModbusFunctionHandler
        {
            Func<IServiceProvider, IModbusFunctionHandler>
                resolveHandler = static p => p.GetRequiredService<THandler>();
            foreach (var descriptor in services.Where(d =>
                         !d.IsKeyedService && d.ServiceType == typeof(IModbusFunctionHandler)))
            {
                if (!resolveHandler.Equals(descriptor.ImplementationFactory))
                {
                    throw new InvalidOperationException("Only one Modbus function handler can be registered per host.");
                }
            }

            services.TryAddSingleton<ModbusServer>();
            services.TryAddSingleton<THandler>();
            services.TryAddSingleton(resolveHandler);

            return services;
        }

        /// <summary>
        /// Registers a raw TCP listener at the specified endpoint as a hosted service.
        /// </summary>
        /// <remarks>
        /// Each call adds a separate listener. Register the protocol server and handler with AddSmodbusServer before
        /// starting the host.
        /// </remarks>
        /// <param name="endpoint">The local address and port to bind; port zero requests an available port.</param>
        /// <returns>The supplied service collection for chaining calls.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint" /> is null.</exception>
        public IServiceCollection AddSmodbusTcpListener(IPEndPoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            // Each hosted service must resolve its own listener, even when several request port zero.
            var listenerKey = new object();
            services.AddKeyedSingleton<ModbusTcpServer>(listenerKey, (p, _) => new ModbusTcpServer(endpoint,
                p.GetRequiredService<ModbusServer>(), p.GetRequiredService<ILogger<ModbusTcpServer>>()));
            services.AddSingleton(p => p.GetRequiredKeyedService<ModbusTcpServer>(listenerKey));
            services.AddSingleton<IHostedService>(p => p.GetRequiredKeyedService<ModbusTcpServer>(listenerKey));
            return services;
        }
    }
}
