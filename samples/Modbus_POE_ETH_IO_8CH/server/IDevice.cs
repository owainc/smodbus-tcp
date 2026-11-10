namespace waveshare_8ch_io;

/// <summary>
/// Represents a local/remote simulated WaveShare 8-Channel Ethernet Digital Input/Output Module.
/// </summary>
public interface IDevice
{
    /// <summary>
    /// Gets the number of input/output channels on the device.
    /// </summary>
    int ChannelCount { get; }

    /// <summary>
    /// Gets the maximum duration for flash mode in milliseconds (0x7FFF units of 100 ms).
    /// </summary>
    long MaxFlashDurationMs { get; }

    /// <summary>
    /// Gets a value indicating whether the device has input control.
    /// </summary>
    bool HasInputControl { get; }

    /// <summary>
    /// Sets the state of an input channel if supported (see <see cref="HasInputControl" />).
    /// </summary>
    Task SetInputState(int index, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an output channel's state without changing its mode. Invalid in linkage mode.
    /// </summary>
    /// <exception cref="InvalidOperationException">The output is in linkage mode.</exception>
    Task SetOutputState(int index, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets all output states without changing modes, stopping at the first output in linkage mode.
    /// Earlier successful writes are not rolled back.
    /// </summary>
    /// <exception cref="InvalidOperationException">One or more outputs are in linkage mode.</exception>
    Task SetAllOutputStates(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets normal/manual mode for an output channel, which is controlled by Modbus commands.
    /// </summary>
    Task SetOutputManual(int index, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets linkage mode for an output channel, which links the output to an input channel.
    /// </summary>
    Task SetOutputLinkage(int index, int inputIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets toggle mode for an output channel, which toggles the output on input channel activation (rising edge).
    /// </summary>
    Task SetOutputToggle(int index, int inputIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets edge trigger mode for an output channel, which toggles the output on input channel edge (rising or falling).
    /// </summary>
    Task SetOutputEdgeTrigger(int index, int inputIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the flash mode for an output channel, which forces the output to a specific state for a defined duration.
    /// </summary>
    Task SetOutputFlash(int index, FlashMode flash, long durationMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of the device.
    /// </summary>
    Task<(bool[] Inputs, bool[] Outputs)> GetState(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the flash mode for an output channel.
/// </summary>
public enum FlashMode
{
    /// <summary>
    /// Output forced active (on) for the duration.
    /// </summary>
    ForceOn,

    /// <summary>
    /// Output forced inactive (off) for the duration.
    /// </summary>
    ForceOff
}
