using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using FluentModbus;

namespace waveshare_8ch_io;

/// <summary>
/// Represents a client for interacting with a Waveshare 8-channel POE Ethernet I/O device.
/// </summary>
public sealed class DeviceClient : IDevice, IAsyncDisposable
{
    private const byte UnitId = 1;
    private readonly IPEndPoint _endpoint;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WaveshareTcpClient? _client;
    private TcpClient? _connection;
    private bool _disposed;

    /// <summary>Creates a client that connects on its first request.</summary>
    /// <param name="address">The IP address of the device.</param>
    /// <param name="port">The port number of the device.</param>
    public DeviceClient(IPAddress address, int port)
    {
        ArgumentNullException.ThrowIfNull(address);
        _endpoint = new IPEndPoint(address, port);
    }

    /// <summary>Closes the connection to the device.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ResetConnection();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public int ChannelCount => 8;

    /// <inheritdoc />
    public long MaxFlashDurationMs => 0x7FFF * 100;

    /// <inheritdoc />
    public bool HasInputControl => false;

    /// <inheritdoc />
    public Task SetInputState(int index, bool enabled, CancellationToken cancellationToken = default)
    {
        ValidateChannelIndex(index);
        throw new NotSupportedException("Device inputs can only be changed by hardware.");
    }

    /// <inheritdoc />
    public Task SetOutputState(int index, bool enabled, CancellationToken cancellationToken = default)
    {
        ValidateChannelIndex(index);
        return ExecuteAsync(async (client, token) =>
        {
            var modes = await client.ReadHoldingRegistersAsync<ushort>(UnitId, 0x1000 + index, 1, token)
                .ConfigureAwait(false);
            if (modes.Span[0] == 1)
            {
                throw new InvalidOperationException("Cannot set the output state in linkage mode.");
            }

            await client.WriteSingleCoilAsync(UnitId, index, enabled, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetOutputManual(int index, bool enabled, CancellationToken cancellationToken = default)
    {
        ValidateChannelIndex(index);
        return ExecuteAsync(async (client, token) =>
        {
            await client.WriteSingleRegisterAsync(UnitId, 0x1000 + index, (ushort)0, token).ConfigureAwait(false);
            await client.WriteSingleCoilAsync(UnitId, index, enabled, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetAllOutputStates(bool enabled, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async (client, token) =>
        {
            var values = new bool[ChannelCount];
            Array.Fill(values, enabled);
            try
            {
                await client.WriteMultipleCoilsAsync(UnitId, 0, values, token).ConfigureAwait(false);
            }
            catch (ModbusException exception) when (exception.ExceptionCode == ModbusExceptionCode.IllegalDataValue)
            {
                throw new InvalidOperationException("One or more outputs are in linkage mode.", exception);
            }

            return true;
        }, cancellationToken);

    /// <inheritdoc />
    public Task SetOutputLinkage(int index, int inputIndex, CancellationToken cancellationToken = default) =>
        SetOutputModeAsync(index, inputIndex, 1, cancellationToken);

    /// <inheritdoc />
    public Task SetOutputToggle(int index, int inputIndex, CancellationToken cancellationToken = default) =>
        SetOutputModeAsync(index, inputIndex, 2, cancellationToken);

    /// <inheritdoc />
    public Task SetOutputEdgeTrigger(int index, int inputIndex, CancellationToken cancellationToken = default) =>
        SetOutputModeAsync(index, inputIndex, 3, cancellationToken);

    /// <inheritdoc />
    /// <remarks>The duration must be a multiple of 100 ms.</remarks>
    public Task SetOutputFlash(int index, FlashMode flash, long durationMs,
        CancellationToken cancellationToken = default)
    {
        ValidateChannelIndex(index);
        if (durationMs < 0 || durationMs > MaxFlashDurationMs)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMs));
        }

        if (durationMs % 100 != 0)
        {
            throw new ArgumentException("Flash duration must be a multiple of 100 ms.", nameof(durationMs));
        }

        var address = flash switch
        {
            FlashMode.ForceOn => 0x0200 + index,
            FlashMode.ForceOff => 0x0400 + index,
            _ => throw new ArgumentOutOfRangeException(nameof(flash))
        };

        // Waveshare uses function 05 with a duration instead of a standard boolean coil value.
        return ExecuteAsync(async (client, token) =>
        {
            await client.WriteFlashAsync((ushort)address, (ushort)(durationMs / 100), token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<(bool[] Inputs, bool[] Outputs)> GetState(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async (client, token) =>
        {
            // FluentModbus returns packed bits backed by its reusable receive buffer.
            // Copy inputs before the next request overwrites that buffer.
            var inputs = UnpackBits(await client.ReadDiscreteInputsAsync(UnitId, 0, (ushort)ChannelCount, token)
                .ConfigureAwait(false));
            var outputs = UnpackBits(await client.ReadCoilsAsync(UnitId, 0, (ushort)ChannelCount, token)
                .ConfigureAwait(false));
            return (inputs, outputs);
        }, cancellationToken);

    private Task SetOutputModeAsync(int index, int inputIndex, ushort mode, CancellationToken cancellationToken)
    {
        ValidateChannelIndex(index);
        ValidateChannelIndex(inputIndex, nameof(inputIndex));
        if (index != inputIndex)
        {
            throw new NotSupportedException("The device only supports linking an output to the same-numbered input.");
        }

        return ExecuteAsync(async (client, token) =>
        {
            await client.WriteSingleRegisterAsync(UnitId, 0x1000 + index, mode, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    private void ValidateChannelIndex(int index, string paramName = "index")
    {
        if (index < 0 || index >= ChannelCount)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    /// <summary>
    /// Executes an asynchronous operation atomically, ensuring that only one operation is in progress at a time and
    /// that the client has not been disposed.
    /// </summary>
    private async Task<T> ExecuteAsync<T>(Func<WaveshareTcpClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var operationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            var token = operationCancellation.Token;
            try
            {
                if (_client is null)
                {
                    _connection = new TcpClient(_endpoint.AddressFamily);
                    await _connection.ConnectAsync(_endpoint.Address, _endpoint.Port, token).ConfigureAwait(false);
                    _client = new WaveshareTcpClient();
                    _client.Initialize(_connection, ModbusEndianness.BigEndian);
                }

                token.ThrowIfCancellationRequested();
                return await operation(_client, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (token.IsCancellationRequested)
            {
                ResetConnection();
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("The Modbus operation was canceled.", exception,
                        cancellationToken);
                }

                throw new TimeoutException("The Modbus operation timed out after 5 seconds.", exception);
            }
            catch
            {
                ResetConnection();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool[] UnpackBits(ReadOnlyMemory<byte> packed)
    {
        var values = new bool[ChannelCount];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = (packed.Span[index / 8] & (1 << (index % 8))) != 0;
        }

        return values;
    }

    private void ResetConnection()
    {
        _client?.Dispose();
        _client = null;
        _connection?.Dispose();
        _connection = null;
    }

    private sealed class WaveshareTcpClient : ModbusTcpClient
    {
        public async Task WriteFlashAsync(ushort address, ushort duration, CancellationToken cancellationToken)
        {
            var pdu = new byte[5];
            pdu[0] = (byte)ModbusFunctionCode.WriteSingleCoil;
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), address);
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), duration);
            var response = await TransceiveFrameAsync(UnitId, ModbusFunctionCode.WriteSingleCoil,
                writer => writer.Write(pdu), cancellationToken).ConfigureAwait(false);
            if (!response.Span.SequenceEqual(pdu))
            {
                throw new IOException("The device returned an invalid response to the flash command.");
            }
        }
    }
}
