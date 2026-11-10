using Microsoft.Extensions.Logging.Abstractions;
using SmodbusTcp;
using waveshare_8ch_io;

var showUi = args.Any(x => x.Equals("ui", StringComparison.OrdinalIgnoreCase));
var logCommands = args.Any(x => x.Equals("log", StringComparison.OrdinalIgnoreCase));

if (showUi && logCommands)
{
    Console.Error.WriteLine("Choose either ui or log mode.");
    Environment.ExitCode = 1;
    return;
}

var hostArgs = args.Where(x => !x.Equals("ui", StringComparison.OrdinalIgnoreCase) &&
                               !x.Equals("log", StringComparison.OrdinalIgnoreCase)).ToArray();
var host = Host.CreateDefaultBuilder(hostArgs)
    .ConfigureLogging(logging =>
    {
        if (showUi || logCommands)
        {
            logging.ClearProviders();
            logging.AddProvider(NullLoggerProvider.Instance);
        }
    })
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<IDeviceLogger>(_ => logCommands
            ? new ConsoleDeviceLogger()
            : new NullDeviceLogger());

        if (showUi)
        {
            services.AddHostedService<TerminalUiService>();
        }

        services.AddSingleton<Device>();
        services.AddSmodbusServer<ModbusService>();
    })
    .UseSmodbusKestrel()
    .Build();

await host.RunAsync().ConfigureAwait(false);
