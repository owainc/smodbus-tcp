using System.Net;
using waveshare_8ch_io;

await using var device = new DeviceClient(IPAddress.Loopback, 502);
if (args.Length == 1 && args[0].Equals("ui", StringComparison.OrdinalIgnoreCase))
{
    await TerminalUi.RunAsync(device, "Modbus device client");
    return 0;
}

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: client ui | <command>");
    Console.Error.WriteLine(CommandParser.GetHelp(device));
    return 1;
}

try
{
    var (success, result) = await CommandParser.TryExecuteCommandAsync(string.Join(' ', args), device);
    var output = success ? Console.Out : Console.Error;
    await output.WriteLineAsync(result);
    if (!success)
    {
        await output.WriteLineAsync(CommandParser.GetHelp(device));
    }

    return success ? 0 : 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
