using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class CustomInterfaceHandlerTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UnimplementedFunctions_ReturnIllegalFunction(bool useBaseHandler)
    {
        IModbusFunctionHandler handler = useBaseHandler ? new EmptyHandler() : new UnsupportedHandler();
        await Exchange(handler, Frame(1, 0, 20, 0, 1), Frame(0x81, 1));
        await Exchange(handler, Frame(2, 0, 20, 0, 1), Frame(0x82, 1));
        await Exchange(handler, Frame(3, 0, 20, 0, 1), Frame(0x83, 1));
        await Exchange(handler, Frame(4, 0, 20, 0, 1), Frame(0x84, 1));
        await Exchange(handler, Frame(5, 0, 20, 0xFF, 0), Frame(0x85, 1));
        await Exchange(handler, Frame(6, 0, 20, 0x12, 0x34), Frame(0x86, 1));
        await Exchange(handler, Frame(15, 0, 20, 0, 1, 1, 1), Frame(0x8F, 1));
        await Exchange(handler, Frame(16, 0, 20, 0, 1, 2, 0x12, 0x34), Frame(0x90, 1));
    }

    [TestMethod]
    public async Task PartialImplementation_UsesCustomMethodAndDefaults()
    {
        var handler = new HoldingRegisterHandler();
        await Exchange(handler, Frame(3, 0, 20, 0, 1), Frame(3, 2, 0x12, 0x34));
        await Exchange(handler, Frame(4, 0, 20, 0, 1), Frame(0x84, 1));
        await Exchange(handler, Frame(6, 0, 20, 0x12, 0x34), Frame(0x86, 1));
    }

    [TestMethod]
    public async Task ReimplementedInterface_CanForwardEveryFunctionToBaseDispatch()
    {
        var handler = new ForwardingHandler();
        await Exchange(handler, Frame(1, 0, 20, 0, 1), Frame(1, 1, 1));
        await Exchange(handler, Frame(2, 0, 20, 0, 1), Frame(2, 1, 1));
        await Exchange(handler, Frame(3, 0, 20, 0, 1), Frame(3, 2, 0x12, 0x34));
        await Exchange(handler, Frame(4, 0, 20, 0, 1), Frame(4, 2, 0x12, 0x34));
        await Exchange(handler, Frame(5, 0, 20, 0xFF, 0), Frame(5, 0, 20, 0xFF, 0));
        await Exchange(handler, Frame(6, 0, 20, 0x12, 0x34), Frame(6, 0, 20, 0x12, 0x34));
        await Exchange(handler, Frame(15, 0, 20, 0, 1, 1, 1), Frame(15, 0, 20, 0, 1));
        await Exchange(handler, Frame(16, 0, 20, 0, 1, 2, 0x12, 0x34), Frame(16, 0, 20, 0, 1));
        Assert.AreEqual(8, handler.Forwarded);

        await Exchange(handler, Frame(5, 2, 3, 0x12, 0x34), Frame(5, 2, 3, 0x12, 0x34));
        Assert.AreEqual(1, handler.CustomCoils);
    }

    private sealed class UnsupportedHandler : IModbusFunctionHandler;

    private sealed class HoldingRegisterHandler : IModbusFunctionHandler
    {
        public Task<ModbusResponseCode> ReadHoldingRegisters(byte unitId, ushort firstAddress, ushort registerCount,
            ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
            => ModbusFunctionHandlerHelpers.HandleRead(ModbusFunctionCode.ReadHoldingRegisters, registerCount,
                responseWriter, () =>
                {
                    responseWriter.WriteUInt16BigEndian(0x1234);
                    return Task.FromResult(ModbusResponseCode.Ok);
                });
    }

    private sealed class ForwardingHandler : RecordingHandler, IModbusFunctionHandler
    {
        public int Forwarded { get; private set; }
        public int CustomCoils { get; private set; }

        Task<ModbusResponseCode> IModbusFunctionHandler.ReadCoils(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken token)
        {
            Forwarded++;
            return HandleReadCoils(unitId, address, count, writer, token);
        }

        Task<ModbusResponseCode> IModbusFunctionHandler.ReadDiscreteInputs(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken token)
        {
            Forwarded++;
            return HandleReadDiscreteInputs(unitId, address, count, writer, token);
        }

        Task<ModbusResponseCode> IModbusFunctionHandler.ReadInputRegisters(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken token)
        {
            Forwarded++;
            return HandleReadInputRegisters(unitId, address, count, writer, token);
        }

        Task<ModbusResponseCode> IModbusFunctionHandler.ReadHoldingRegisters(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken token)
        {
            Forwarded++;
            return HandleReadHoldingRegisters(unitId, address, count, writer, token);
        }

        Task<ModbusResponseCode> IModbusFunctionHandler.WriteSingleCoil(byte unitId, ushort address, ushort value,
            CancellationToken token)
        {
            if (address == 0x0203 && value == 0x1234)
            {
                CustomCoils++;
                return Task.FromResult(ModbusResponseCode.Ok);
            }

            Forwarded++;
            return HandleWriteSingleCoil(unitId, address, value, token);
        }

        Task<ModbusResponseCode> IModbusFunctionHandler.WriteSingleRegister(byte unitId, ushort address, ushort value,
            CancellationToken token)
        {
            Forwarded++;
            return HandleWriteSingleRegister(unitId, address, value, token);
        }

        Task<ModbusResponseCode> IModbusFunctionHandler.WriteMultipleCoils(byte unitId, MultiCoilWrite packet,
            CancellationToken token)
        {
            Forwarded++;
            return HandleWriteMultipleCoils(unitId, packet, token);
        }

        Task<ModbusResponseCode> IModbusFunctionHandler.WriteMultipleRegisters(byte unitId, MultiRegisterWrite packet,
            CancellationToken token)
        {
            Forwarded++;
            return HandleWriteMultipleRegisters(unitId, packet, token);
        }
    }
}
