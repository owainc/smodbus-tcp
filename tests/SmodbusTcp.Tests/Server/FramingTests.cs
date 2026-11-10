using System.IO.Pipelines;
using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class FramingTests
{
    [TestMethod]
    public async Task EndOfInputAtEveryPartialFrame_DoesNotDispatchIncompleteWrite()
    {
        var frame = Frame(16, 0, 20, 0, 2, 4, 0x12, 0x34, 0xAB, 0xCD);
        for (var length = 0; length < frame.Length; length++)
        {
            var handler = new RecordingHandler();
            await Exchange(handler, frame[..length], []);
            Assert.AreEqual(0, handler.Calls, $"Truncated at byte {length}");
        }
    }

    [TestMethod]
    public async Task RejectedRequest_DoesNotConsumeFollowingFrameOrLeakResponseData()
    {
        var bad = Frame(16, 0, 20, 0, 1, 2, 1);
        var good = Frame(16, 0, 20, 0, 1, 2, 0xAB, 0xCD);
        var handler = new RecordingHandler();
        await Exchange(handler, bad.Concat(good).ToArray(),
            Frame(0x90, 3).Concat(Frame(16, 0, 20, 0, 1)).ToArray());
        Assert.AreEqual(1, handler.Calls);
        CollectionAssert.AreEqual(new ushort[] { 0xABCD }, handler.Registers.ToArray());
    }

    [TestMethod]
    public async Task CompleteFrameFollowedByPartialFrame_OnlyDispatchesCompleteWrite()
    {
        var frame = Frame(15, 0, 20, 0, 1, 1, 1);
        var handler = new RecordingHandler();
        await Exchange(handler, frame.Concat(frame[..^1]).ToArray(), Frame(15, 0, 20, 0, 1));
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Cancellation_StopsIdleOrPartialFrameWithoutDispatch(bool partial)
    {
        var input = new Pipe();
        var output = new Pipe();
        using var cancellation = new CancellationTokenSource();
        var handler = new RecordingHandler();
        var run = new ModbusServer(handler).ProcessAsync(new Duplex(input.Reader, output.Writer), cancellation.Token);
        try
        {
            if (partial)
            {
                await input.Writer.WriteAsync(Frame(16, 0, 20, 0, 1, 2, 0, 1)[..^1]);
            }

            await cancellation.CancelAsync();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, handler.Calls);
        }
        finally
        {
            await input.Writer.CompleteAsync();
            await input.Reader.CompleteAsync();
            await output.Writer.CompleteAsync();
            await output.Reader.CompleteAsync();
        }
    }

    [TestMethod]
    public async Task FragmentationAtEveryByteBoundary_ProcessesExactlyOneWrite()
    {
        var frame = Frame(16, 0, 20, 0, 1, 2, 0xAB, 0xCD);
        for (var split = 1; split < frame.Length; split++)
        {
            var input = new Pipe();
            var output = new Pipe();
            var observed = new ObservedReader(input.Reader);
            var handler = new RecordingHandler();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var run = new ModbusServer(handler).ProcessAsync(new Duplex(observed, output.Writer), timeout.Token);
            try
            {
                await input.Writer.WriteAsync(frame.AsMemory(0, split), timeout.Token);
                // Wait until the server examines the incomplete frame, without timing assumptions.
                await observed.Examined.Task.WaitAsync(timeout.Token);
                Assert.AreEqual(0, handler.Calls);
                await input.Writer.WriteAsync(frame.AsMemory(split), timeout.Token);
                await input.Writer.CompleteAsync();
                await run.WaitAsync(timeout.Token);
                await output.Writer.CompleteAsync();
                var response = new byte[12];
                await output.Reader.AsStream(true).ReadExactlyAsync(response, timeout.Token);
                CollectionAssert.AreEqual(Frame(16, 0, 20, 0, 1), response);
                Assert.AreEqual(0, await output.Reader.AsStream(true).ReadAsync(new byte[1], timeout.Token));
                Assert.AreEqual(1, handler.Calls);
            }
            finally
            {
                await timeout.CancelAsync();
                try
                {
                    await run.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (OperationCanceledException)
                {
                }

                await input.Writer.CompleteAsync();
                await input.Reader.CompleteAsync();
                await output.Writer.CompleteAsync();
                await output.Reader.CompleteAsync();
            }
        }
    }

    private sealed class ObservedReader(PipeReader inner) : PipeReader
    {
        public TaskCompletionSource Examined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void AdvanceTo(SequencePosition consumed) => inner.AdvanceTo(consumed);

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            inner.AdvanceTo(consumed, examined);
            Examined.TrySetResult();
        }

        public override void CancelPendingRead() => inner.CancelPendingRead();
        public override void Complete(Exception? exception = null) => inner.Complete(exception);

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            inner.ReadAsync(cancellationToken);

        public override bool TryRead(out ReadResult result) => inner.TryRead(out result);
    }
}
