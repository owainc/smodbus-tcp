using waveshare_8ch_io.OutputModes;

namespace waveshare_8ch_io
{
    /// <summary>
    /// A simulated WaveShare 8-Channel Ethernet Digital Input/Output Module.
    /// </summary>
    public class Device : IDevice
    {
        /// <summary>
        /// Gets the number of input and output channels of the device (8).
        /// </summary>
        public const int ChannelCount = 8;

        /// <summary>
        /// Gets the maximum duration for flash mode in milliseconds (0x7FFF units of 100 ms).
        /// </summary>
        public const int MaxFlashDurationMs = 0x7fff * 100;

        private readonly IDeviceLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Device" /> class with 8 input and output channels.
        /// </summary>
        public Device() : this(new NullDeviceLogger())
        {
        }

        /// <summary>Creates a device with a logger for successful commands.</summary>
        public Device(IDeviceLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            Inputs = new InputChannel[ChannelCount];
            Outputs = new OutputChannel[ChannelCount];

            for (var i = 0; i < ChannelCount; i++)
            {
                Inputs[i] = new InputChannel();
                Outputs[i] = new OutputChannel();
            }
        }

        /// <summary>
        /// Gets the output channels of the device.
        /// </summary>
        private OutputChannel[] Outputs { get; }

        /// <summary>
        /// Gets the input channels of the device.
        /// </summary>
        private InputChannel[] Inputs { get; }

        /// <inheritdoc />
        int IDevice.ChannelCount { get; } = ChannelCount;

        /// <inheritdoc />
        long IDevice.MaxFlashDurationMs { get; } = MaxFlashDurationMs;

        /// <inheritdoc />
        public bool HasInputControl { get; } = true;

        /// <inheritdoc />
        public Task SetInputState(int index, bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannelIndex(index);
            Inputs[index].Set(enabled);
            _logger.Log($"input {index + 1} {(enabled ? "on" : "off")}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetOutputManual(int index, bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannelIndex(index);

            UnlinkInput(index);
            Outputs[index].SetMode(new Manual(enabled));
            _logger.Log($"output {index + 1} {(enabled ? "on" : "off")}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetOutputLinkage(int index, int inputIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannelIndex(index);
            ValidateChannelIndex(inputIndex, nameof(inputIndex));

            UnlinkInput(index);
            Outputs[index].SetMode(new Linkage(Inputs[inputIndex]));
            Inputs[inputIndex].LinkOutput(Outputs[index]);
            _logger.Log($"output {index + 1} link {inputIndex + 1}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetOutputToggle(int index, int inputIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannelIndex(index);
            ValidateChannelIndex(inputIndex, nameof(inputIndex));

            if (inputIndex is < 0 or >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(inputIndex));
            }

            UnlinkInput(index);
            Outputs[index].SetMode(new Toggle(false, Inputs[inputIndex]));
            Inputs[inputIndex].LinkOutput(Outputs[index]);
            _logger.Log($"output {index + 1} toggle {inputIndex + 1}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetOutputEdgeTrigger(int index, int inputIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannelIndex(index);
            ValidateChannelIndex(inputIndex, nameof(inputIndex));

            UnlinkInput(index);
            Outputs[index].SetMode(new EdgeTrigger(false, Inputs[inputIndex]));
            Inputs[inputIndex].LinkOutput(Outputs[index]);
            _logger.Log($"output {index + 1} edge {inputIndex + 1}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetOutputFlash(int index, FlashMode flash, long durationMs,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannelIndex(index);

            if (durationMs is < 0 or > MaxFlashDurationMs)
            {
                throw new ArgumentOutOfRangeException(nameof(durationMs));
            }

            if (flash is not (FlashMode.ForceOn or FlashMode.ForceOff))
            {
                throw new ArgumentOutOfRangeException(nameof(flash));
            }

            Outputs[index].Flash = new FlashState(flash, Environment.TickCount64 + durationMs);
            _logger.Log(FormattableString.Invariant(
                $"output {index + 1} flash {(flash == FlashMode.ForceOn ? "on" : "off")} {durationMs}"));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<(bool[] Inputs, bool[] Outputs)> GetState(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inputs = new bool[ChannelCount];
            var outputs = new bool[ChannelCount];

            for (var i = 0; i < ChannelCount; i++)
            {
                inputs[i] = Inputs[i].Enabled;
                outputs[i] = Outputs[i].GetEnabled(Environment.TickCount64);
            }

            return Task.FromResult((inputs, outputs));
        }

        /// <inheritdoc />
        public Task SetOutputState(int index, bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannelIndex(index);
            var output = Outputs[index];
            switch (output.Mode)
            {
                case Linkage:
                    throw new InvalidOperationException("Cannot set the output state in linkage mode.");
                case Manual:
                    output.SetMode(new Manual(enabled));
                    break;
                case Toggle toggle:
                    output.SetMode(toggle with { Enabled = enabled });
                    toggle.Input.LinkOutput(output);
                    break;
                case EdgeTrigger edge:
                    output.SetMode(edge with { Enabled = enabled });
                    edge.Input.LinkOutput(output);
                    break;
            }

            _logger.Log($"output {index + 1} state {(enabled ? "on" : "off")}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task SetAllOutputStates(bool enabled, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < ChannelCount; i++)
            {
                await SetOutputState(i, enabled, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the current state of an input channel.
        /// </summary>
        public bool GetInputState(int index)
        {
            ValidateChannelIndex(index);
            return Inputs[index].Enabled;
        }

        /// <summary>
        /// Gets the current state of an output channel.
        /// </summary>
        public bool GetOutputState(int index)
        {
            ValidateChannelIndex(index);
            return Outputs[index].GetEnabled(Environment.TickCount64);
        }

        /// <summary>
        /// Gets the current output mode of an output channel.
        /// </summary>
        public OutputMode GetOutputMode(int index)
        {
            ValidateChannelIndex(index);
            return Outputs[index].Mode;
        }

        private void UnlinkInput(int outputIndex)
        {
            ValidateChannelIndex(outputIndex);

            switch (Outputs[outputIndex].Mode)
            {
                case Linkage linkage:
                    linkage.Input.UnlinkOutput(Outputs[outputIndex]);
                    break;
                case Toggle toggle:
                    toggle.Input.UnlinkOutput(Outputs[outputIndex]);
                    break;
                case EdgeTrigger edgeTrigger:
                    edgeTrigger.Input.UnlinkOutput(Outputs[outputIndex]);
                    break;
            }
        }

        private void ValidateChannelIndex(int index, string paramName = "index")
        {
            if (index is < 0 or >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(paramName);
            }
        }
    }

    namespace OutputModes
    {
        /// <summary>
        /// Manual mode - controlled by Modbus commands.
        /// </summary>
        public readonly record struct Manual(bool Enabled = false);

        /// <summary>
        /// Linkage mode - output is linked to an input channel.
        /// Note: Flash mode is not supported in linkage mode.
        /// </summary>
        public readonly record struct Linkage(InputChannel Input);

        /// <summary>
        /// Toggle mode - output is toggled by an input channel becoming active (rising edge).
        /// </summary>
        public readonly record struct Toggle(bool Enabled, InputChannel Input);

        /// <summary>
        /// Edge trigger mode - output is toggled on input channel edge - rising or falling.
        /// </summary>
        public readonly record struct EdgeTrigger(bool Enabled, InputChannel Input);
    }

    /// <summary>
    /// Defines the output mode for an output channel.
    /// </summary>
    public union OutputMode(
        Manual,
        Linkage,
        Toggle,
        EdgeTrigger);

    /// <summary>
    /// Defines the flash state for an output channel.
    /// </summary>
    public readonly record struct FlashState(
        FlashMode Mode,
        long Until);

    /// <summary>
    /// Represents an output channel of the device, which can operate in different modes and can be linked to input
    /// channels.
    /// </summary>
    public class OutputChannel
    {
        /// <summary>
        /// Gets or sets the mode of the output channel.
        /// </summary>
        public OutputMode Mode { get; private set; } = new Manual();

        /// <summary>
        /// Gets or sets the flash state of the output channel.
        /// </summary>
        public FlashState? Flash { get; set; }

        public void SetMode(OutputMode mode)
        {
            // if existing mode is linked to input, unlink it
            switch (Mode)
            {
                case Linkage linkage:
                    linkage.Input.UnlinkOutput(this);
                    break;
                case Toggle toggle:
                    toggle.Input.UnlinkOutput(this);
                    break;
                case EdgeTrigger edgeTrigger:
                    edgeTrigger.Input.UnlinkOutput(this);
                    break;
            }

            Mode = mode;
        }

        /// <summary>
        /// Gets a value indicating whether the output channel is enabled based on its mode and flash state.
        /// </summary>
        /// <param name="currentTime">The current time in ticks.</param>
        /// <returns>True if the output channel is enabled; otherwise, false.</returns>
        public bool GetEnabled(long currentTime)
        {
            return Mode switch
            {
                Linkage linkage => linkage.Input.Enabled,
                Manual manual => ApplyFlash(manual.Enabled, currentTime),
                Toggle toggle => ApplyFlash(toggle.Enabled, currentTime),
                EdgeTrigger edgeTrigger => ApplyFlash(edgeTrigger.Enabled, currentTime),
                null => throw new InvalidOperationException("The output mode is uninitialized.")
            };
        }

        private bool ApplyFlash(bool enabled, long currentTime)
        {
            if (Flash is { } flash && currentTime < flash.Until)
            {
                return flash.Mode switch
                {
                    FlashMode.ForceOn => true,
                    FlashMode.ForceOff => false,
                    _ => throw new InvalidOperationException($"Unknown flash mode: {flash.Mode}")
                };
            }

            return enabled;
        }

        /// <summary>
        /// Handles a change in the linked input channel's enabled state.
        /// </summary>
        /// <param name="enabled">The new enabled state of the linked input channel.</param>
        public void LinkedInputChanged(bool enabled)
        {
            // Handle input change based on the current mode
            switch (Mode)
            {
                case Linkage:
                    // In linkage mode, the output state is directly controlled by the input
                    break;
                case Toggle toggle:
                    if (enabled)
                    {
                        // Toggle the output state on rising edge
                        Mode = toggle with { Enabled = !toggle.Enabled };
                    }

                    break;
                case EdgeTrigger edgeTrigger:
                    // Toggle the output state on any edge (rising or falling)
                    Mode = edgeTrigger with { Enabled = !edgeTrigger.Enabled };
                    break;
            }
        }
    }

    /// <summary>
    /// Simulated input channel state (would set via hardware in a real device).
    /// </summary>
    public sealed class InputChannel
    {
        // Links identify physical channels, even if a subclass implements value equality.
        private readonly HashSet<OutputChannel> _linkedOutputs = [with(ReferenceEqualityComparer.Instance)];
        private readonly Lock _sync = new();

        /// <summary>
        /// Gets or sets a value indicating whether the input channel is enabled (active).
        /// </summary>
        public bool Enabled { get; private set; }

        /// <summary>
        /// Links the input channel to an output channel, allowing the output to be controlled by the input.
        /// </summary>
        public void LinkOutput(OutputChannel output)
        {
            lock (_sync)
            {
                _linkedOutputs.Add(output);
            }
        }

        /// <summary>
        /// Unlinks the input channel from an output channel, preventing the output from being controlled by the input.
        /// </summary>
        public void UnlinkOutput(OutputChannel output)
        {
            lock (_sync)
            {
                _linkedOutputs.Remove(output);
            }
        }

        /// <summary>
        /// Sets the enabled state of the input channel and notifies linked outputs of the change.
        /// </summary>
        public void Set(bool enabled)
        {
            // Keep link changes from invalidating enumeration while notifying outputs.
            lock (_sync)
            {
                if (Enabled == enabled)
                {
                    return;
                }

                Enabled = enabled;
                foreach (var output in _linkedOutputs)
                {
                    output.LinkedInputChanged(enabled);
                }
            }
        }
    }
}
