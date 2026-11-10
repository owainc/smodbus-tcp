using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace waveshare_8ch_io;

/// <summary>
/// Provides a terminal-based user interface for interacting with a Modbus POE ETH IO 8CH device.
/// </summary>
internal static class TerminalUi
{
    /// <summary>
    /// Runs the terminal user interface for the specified device with the given title.
    /// </summary>
    public static async Task RunAsync(IDevice device, string title, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var app = Application.Create();
        app.Init();

        using var window = new Window { Title = $"{title} (Esc to quit)" };
        app.Keyboard.KeyDown += (_, key) =>
        {
            if (key == Key.Esc)
            {
                key.Handled = true;
                app.RequestStop(window);
            }
        };
        var stateFrame = new FrameView
        {
            Title = "Device state",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(9)
        };
        var inputHeading = new Label
        {
            Text = "INPUTS",
            X = 1,
            Y = 2
        };
        var outputHeading = new Label
        {
            Text = "OUTPUTS",
            X = 1,
            Y = 4
        };
        ChannelStateLabel[] inputChannels =
        [
            .. Enumerable.Range(1, device.ChannelCount)
                .Select(channel => new ChannelStateLabel
                {
                    X = 9 + (channel - 1) * 7,
                    Y = 2,
                    Width = 6,
                    Height = 1
                })
        ];
        ChannelStateLabel[] outputChannels =
        [
            .. Enumerable.Range(1, device.ChannelCount)
                .Select(channel => new ChannelStateLabel
                {
                    X = 9 + (channel - 1) * 7,
                    Y = 4,
                    Width = 6,
                    Height = 1
                })
        ];
        stateFrame.Add(inputHeading, outputHeading);
        for (var channel = 1; channel <= device.ChannelCount; channel++)
        {
            stateFrame.Add(new Label
            {
                Text = $" CH {channel}",
                X = 9 + (channel - 1) * 7,
                Y = 0,
                Width = 6,
                Height = 1
            });
        }

        stateFrame.Add([.. inputChannels]);
        stateFrame.Add([.. outputChannels]);
        window.Add(stateFrame);

        var help = new Label
        {
            Text =
                CommandParser.GetHelp(device),
            X = 1,
            Y = Pos.AnchorEnd(8),
            Width = Dim.Fill(2),
            Height = 2
        };
        window.Add(help);

        var commandFrame = new FrameView
        {
            Title = "Command",
            X = 0,
            Y = Pos.AnchorEnd(5),
            Width = Dim.Fill(),
            Height = 5
        };
        var commandPrompt = new Label
        {
            Text = "> ",
            X = 1,
            Y = 1,
            Width = 2,
            Height = 1
        };
        var commandField = new TextField
        {
            X = 3,
            Y = 1,
            Width = Dim.Fill(2)
        };
        var status = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(2),
            Height = 1,
            Text = $"Channels are numbered 1-{device.ChannelCount}. Press Esc to exit."
        };
        commandFrame.Add(commandPrompt, commandField);
        window.Add(commandFrame, status);
        commandField.Initialized += (_, _) => commandField.SetFocus();

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = lifetime.Token;
        var refreshTask = Task.CompletedTask;
        var commandTask = Task.CompletedTask;

        async Task RefreshStateAsync()
        {
            try
            {
                var state = await device.GetState(token).ConfigureAwait(false);
                app.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    for (var i = 0; i < device.ChannelCount; i++)
                    {
                        inputChannels[i].IsOn = state.Inputs[i];
                        outputChannels[i].IsOn = state.Outputs[i];
                    }
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                app.Invoke(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        ShowStatus(exception.Message);
                    }
                });
            }
        }

        var statusVersion = 0;

        void ShowStatus(string message)
        {
            status.Text = message;
            var version = ++statusVersion;
            app.AddTimeout(TimeSpan.FromSeconds(5), () =>
            {
                if (version == statusVersion)
                {
                    status.Text = string.Empty;
                }

                return false;
            });
        }

        async Task ExecuteCommandAsync(string command)
        {
            try
            {
                var (success, result) =
                    await CommandParser.TryExecuteCommandAsync(command, device, token).ConfigureAwait(false);
                app.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    ShowStatus(result);
                    if (success)
                    {
                        commandField.Value = string.Empty;
                    }
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                app.Invoke(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        ShowStatus(exception.Message);
                    }
                });
            }
        }

        commandField.Accepting += (_, e) =>
        {
            e.Handled = true;
            if (commandTask.IsCompleted)
            {
                commandTask = ExecuteCommandAsync(commandField.Value ?? string.Empty);
            }
        };

        // Do not overlap polls when a remote device responds slowly.
        window.Initialized += (_, _) => refreshTask = RefreshStateAsync();
        app.AddTimeout(TimeSpan.FromSeconds(1), () =>
        {
            if (refreshTask.IsCompleted && !token.IsCancellationRequested)
            {
                refreshTask = RefreshStateAsync();
            }

            return !token.IsCancellationRequested;
        });
        try
        {
            await app.RunAsync(window, token).ConfigureAwait(false);
        }
        finally
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(refreshTask, commandTask).ConfigureAwait(false);
        }
    }

    private sealed class ChannelStateLabel : Label
    {
        private bool _isOn;

        public bool IsOn
        {
            set
            {
                _isOn = value;
                Text = _isOn ? "● ON  " : "○ OFF ";
            }
        }

        protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute attribute)
        {
            if (role == VisualRole.Normal)
            {
                attribute = _isOn
                    ? new Attribute(Color.Black, Color.Green)
                    : new Attribute(Color.Gray, Color.Black);
                return true;
            }

            return base.OnGettingAttributeForRole(in role, ref attribute);
        }
    }
}

/// <summary>Parses and executes commands shared by the terminal UI and command line.</summary>
internal static class CommandParser
{
    public static string GetHelp(IDevice device) =>
        $"{(device.HasInputControl ? "input|i <1-8> <on|off> | " : "")}output|o <1-8|all> [state] <on|off>\noutput|o <1-8> link|toggle|edge <input-1-8> | output|o <1-8> flash <on|off> <duration-ms 0-{device.MaxFlashDurationMs}>";

    public static async Task<(bool Success, string Result)> TryExecuteCommandAsync(
        string command, IDevice device, CancellationToken cancellationToken = default)
    {
        var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (args.Length is 3 or 4 &&
            (args[0].Equals("output", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("o", StringComparison.OrdinalIgnoreCase)) &&
            args[1].Equals("all", StringComparison.OrdinalIgnoreCase) &&
            (args.Length == 3 || args[2].Equals("state", StringComparison.OrdinalIgnoreCase)) &&
            TryOnOff(args[^1], out var allEnabled))
        {
            try
            {
                await device.SetAllOutputStates(allEnabled, cancellationToken).ConfigureAwait(false);
                return (true, $"All outputs set {(allEnabled ? "ON" : "OFF")} without changing modes.");
            }
            catch (InvalidOperationException)
            {
                return (false, "Failed: one or more outputs are in linkage mode. Earlier outputs may have changed.");
            }
        }

        if (device.HasInputControl && args.Length == 3 &&
            (args[0].Equals("input", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("i", StringComparison.OrdinalIgnoreCase)) &&
            TryChannel(device.ChannelCount, args[1], out var inputIndex) &&
            TryOnOff(args[2], out var inputEnabled))
        {
            await device.SetInputState(inputIndex, inputEnabled, cancellationToken).ConfigureAwait(false);
            return (true, $"Input {inputIndex + 1} set {(inputEnabled ? "ON" : "OFF")}.");
        }

        if (args.Length >= 3 &&
            (args[0].Equals("output", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("o", StringComparison.OrdinalIgnoreCase)) &&
            TryChannel(device.ChannelCount, args[1], out var outputIndex))
        {
            var mode = args[2].ToLowerInvariant();
            if (mode == "state" && args.Length == 4 && TryOnOff(args[3], out var outputEnabled))
            {
                await device.SetOutputState(outputIndex, outputEnabled, cancellationToken).ConfigureAwait(false);
                return (true, $"Output {outputIndex + 1} set {(outputEnabled ? "ON" : "OFF")} without changing mode.");
            }

            if (args.Length == 3 && TryOnOff(mode, out var enabled))
            {
                await device.SetOutputManual(outputIndex, enabled, cancellationToken).ConfigureAwait(false);
                return (true, $"Output {outputIndex + 1} set {(enabled ? "ON" : "OFF")}.");
            }

            if (mode is "link" or "toggle" or "edge" &&
                args.Length == 4 && TryChannel(device.ChannelCount, args[3], out var linkedInputIndex))
            {
                switch (mode)
                {
                    case "link":
                        await device.SetOutputLinkage(outputIndex, linkedInputIndex, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case "toggle":
                        await device.SetOutputToggle(outputIndex, linkedInputIndex, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case "edge":
                        await device.SetOutputEdgeTrigger(outputIndex, linkedInputIndex, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                }

                return (true, $"Output {outputIndex + 1} set to {mode} input {linkedInputIndex + 1}.");
            }

            if (mode == "flash" &&
                args.Length == 5 &&
                TryOnOff(args[3], out var flashEnabled) &&
                int.TryParse(args[4], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var durationMs))
            {
                try
                {
                    await device.SetOutputFlash(outputIndex, flashEnabled ? FlashMode.ForceOn : FlashMode.ForceOff,
                        durationMs, cancellationToken).ConfigureAwait(false);
                    return (true,
                        $"Output {outputIndex + 1} flashing {(flashEnabled ? "ON" : "OFF")} for {durationMs} ms.");
                }
                catch (ArgumentException)
                {
                }
            }
        }

        return (false, "Invalid command or argument. Use the command syntax shown in help.");
    }

    private static bool TryChannel(int channelCount, string value, out int index)
    {
        if (int.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var channel) &&
            channel >= 1 && channel <= channelCount)
        {
            index = channel - 1;
            return true;
        }

        index = -1;
        return false;
    }

    private static bool TryOnOff(string value, out bool enabled)
    {
        if (value.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            enabled = true;
            return true;
        }

        if (value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            enabled = false;
            return true;
        }

        enabled = false;
        return false;
    }
}
