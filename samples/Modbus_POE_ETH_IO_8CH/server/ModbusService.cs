using SmodbusTcp;
using waveshare_8ch_io.OutputModes;

namespace waveshare_8ch_io;

/// <summary>
/// A service that implements the Modbus function handlers for the device.
/// </summary>
public class ModbusService(Device device) : ModbusFunctionHandlerBase, IModbusFunctionHandler
{
    private const ushort ModeAddress = 0x1000;

    /// <inheritdoc />
    async Task<ModbusResponseCode> IModbusFunctionHandler.WriteSingleCoil(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken)
    {
        // Flash addresses use non-standard coil values
        if (address is >= 0x0200 and <= 0x0207 or >= 0x0400 and <= 0x0407)
        {
            var durationMs = value * 100;
            if (durationMs > Device.MaxFlashDurationMs)
            {
                return ModbusResponseCode.IllegalDataValue;
            }

            var flashOn = address < 0x0400;
            var channel = address - (flashOn ? 0x0200 : 0x0400);
            await device.SetOutputFlash(channel, flashOn ? FlashMode.ForceOn : FlashMode.ForceOff, durationMs,
                cancellationToken);
            return ModbusResponseCode.Ok;
        }

        return await HandleWriteSingleCoil(unitId, address, value, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort firstAddress,
        ushort registerCount, ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ReadChannels(firstAddress, registerCount, responseWriter, device.GetOutputState);

    /// <inheritdoc />
    protected override Task<ModbusResponseCode> OnReadDiscreteInputs(byte unitId, ushort firstAddress,
        ushort registerCount, ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ReadChannels(firstAddress, registerCount, responseWriter, device.GetInputState);

    /// <inheritdoc />
    protected override Task<ModbusResponseCode> OnReadHoldingRegisters(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
    {
        if (firstAddress >= ModeAddress && firstAddress + registerCount <= ModeAddress + Device.ChannelCount)
        {
            for (var i = 0; i < registerCount; i++)
            {
                var mode = device.GetOutputMode(firstAddress - ModeAddress + i);
                responseWriter.WriteUInt16BigEndian(mode switch
                {
                    Manual => 0,
                    Linkage => 1,
                    Toggle => 2,
                    EdgeTrigger => 3
                });
            }
        }
        else if (registerCount == 1 && firstAddress == 0x4000)
        {
            responseWriter.WriteUInt16BigEndian(1); // Fixed device address.
        }
        else if (registerCount == 1 && firstAddress == 0x8000)
        {
            responseWriter.WriteUInt16BigEndian(100); // Simulator firmware version 1.00.
        }
        else
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        return Task.FromResult(ModbusResponseCode.Ok);
    }

    /// <inheritdoc />
    protected override async Task<ModbusResponseCode> OnWriteSingleRegister(byte unitId, ushort address,
        ushort value, CancellationToken cancellationToken = default)
    {
        if (address is < ModeAddress or >= ModeAddress + Device.ChannelCount)
        {
            return ModbusResponseCode.IllegalDataAddress;
        }

        if (value > 3)
        {
            return ModbusResponseCode.IllegalDataValue;
        }

        var index = address - ModeAddress;
        switch (value)
        {
            case 0:
                await device.SetOutputManual(index, device.GetOutputState(index), cancellationToken);
                break;
            case 1:
                await device.SetOutputLinkage(index, index, cancellationToken);
                break;
            case 2:
                await device.SetOutputToggle(index, index, cancellationToken);
                break;
            case 3:
                await device.SetOutputEdgeTrigger(index, index, cancellationToken);
                break;
        }

        return ModbusResponseCode.Ok;
    }

    /// <inheritdoc />
    protected override Task<ModbusResponseCode> OnWriteMultipleCoils(byte unitId, ushort firstAddress,
        ReadOnlyMemory<bool> values,
        CancellationToken cancellationToken = default)
        => HandleAsSingleWrites(unitId, firstAddress, values, cancellationToken);

    /// <inheritdoc />
    protected override async Task<ModbusResponseCode> OnWriteSingleCoil(byte unitId, ushort address, bool coilOn,
        CancellationToken cancellationToken = default)
    {
        var toggle = address is >= 0x0100 and <= 0x0107 or 0x01ff;
        var all = address is 0x00ff or 0x01ff;
        var index = toggle ? address - 0x0100 : address;
        if (!all && index >= Device.ChannelCount)
        {
            return ModbusResponseCode.IllegalDataAddress;
        }

        if (!toggle || coilOn)
        {
            var first = all ? 0 : index;
            var end = all ? Device.ChannelCount : index + 1;
            for (var i = first; i < end; i++)
            {
                if (device.GetOutputMode(i) is Linkage)
                {
                    return ModbusResponseCode.IllegalDataValue;
                }

                await device.SetOutputState(i, toggle ? !device.GetOutputState(i) : coilOn, cancellationToken);
            }
        }

        return ModbusResponseCode.Ok;
    }

    private static Task<ModbusResponseCode> ReadChannels(ushort firstAddress, ushort count,
        ModbusResponseWriter writer, Func<int, bool> getState)
    {
        if (firstAddress + count > Device.ChannelCount)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        byte states = 0;
        for (var i = 0; i < count; i++)
        {
            if (getState(firstAddress + i))
            {
                states |= (byte)(1 << i);
            }
        }

        writer.Write(states);
        return Task.FromResult(ModbusResponseCode.Ok);
    }
}
