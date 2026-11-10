namespace waveshare_8ch_io;

/// <summary>
/// A background service that runs the terminal UI for monitoring the device state.
/// </summary>
public sealed class TerminalUiService(Device device, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ApplicationStopping also covers shutdown initiated by another hosted service.
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, lifetime.ApplicationStopping);

        try
        {
            await TerminalUi.RunAsync(device, "Modbus device monitor", cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Host shutdown is a normal way to close the UI.
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
