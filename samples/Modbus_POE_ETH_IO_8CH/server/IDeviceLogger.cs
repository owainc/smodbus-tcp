namespace waveshare_8ch_io;

/// <summary>Records successful device writes in command parser syntax.</summary>
public interface IDeviceLogger
{
    void Log(string command);
}

/// <summary>Writes one command per line to standard output.</summary>
public sealed class ConsoleDeviceLogger : IDeviceLogger
{
    public void Log(string command) => Console.WriteLine(command);
}

/// <summary>Discards device commands.</summary>
public sealed class NullDeviceLogger : IDeviceLogger
{
    public void Log(string command)
    {
    }
}
