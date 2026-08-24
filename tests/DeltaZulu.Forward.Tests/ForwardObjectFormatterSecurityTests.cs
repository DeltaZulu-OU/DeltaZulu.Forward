using System.Buffers;
using MessagePack;

namespace DeltaZulu.Forward.Tests;

/// <summary>
/// The decode path is reached by anything that can open a Forward session, so its
/// inputs are hostile by default.
///
/// <para>
/// <see cref="MessagePackSecurity.UntrustedData"/> was already selected in
/// <see cref="ForwardMessagePackOptions"/>, but this formatter never consulted
/// <c>options.Security</c>, so the setting was decorative. The depth limit in
/// particular was entirely unenforced: before the fix a 5,000-deep nested map
/// decoded with no exception at all.
/// </para>
/// </summary>
[TestClass]
public sealed class ForwardObjectFormatterSecurityTests
{
    [TestMethod]
    public void DeeplyNestedMap_IsRejected_RatherThanOverflowingTheStack()
    {
        // Each level is a tagged [Map, {"k": <next>}] pair. The recursion in
        // Deserialize follows the payload's nesting, so without a depth guard the
        // payload's nesting IS the call-stack depth: a few hundred KB of crafted
        // input takes the process down with a StackOverflowException, which cannot
        // be caught and kills the collector rather than the batch.
        var payload = BuildNestedMap(depth: 5_000);

        // DepthStep enforces MaximumObjectGraphDepth (500 under UntrustedData) and
        // signals with InsufficientExecutionStackException. The exception type is
        // incidental; what matters is that this is a catchable failure that rejects
        // the batch, rather than a StackOverflowException that cannot be caught and
        // takes the collector down with it.
        Assert.ThrowsExactly<InsufficientExecutionStackException>(() => Decode(payload));
    }

    [TestMethod]
    public void DeeplyNestedArray_IsRejected_RatherThanOverflowingTheStack()
    {
        var payload = BuildNestedArray(depth: 5_000);

        Assert.ThrowsExactly<InsufficientExecutionStackException>(() => Decode(payload));
    }

    [TestMethod]
    public void ModestlyNestedMap_StillRoundTrips()
    {
        // The guard must reject hostile depth without breaking legitimate nesting.
        var payload = BuildNestedMap(depth: 8);

        var decoded = Decode(payload);

        var level = decoded as IReadOnlyDictionary<string, object?>;
        for (var i = 0; i < 7; i++)
        {
            Assert.IsNotNull(level, $"nesting collapsed at level {i}");
            level = level!["k"] as IReadOnlyDictionary<string, object?>;
        }
    }

    [TestMethod]
    public void OverstatedMapHeader_FailsCheaply()
    {
        // A sender declares int.MaxValue entries and supplies none.
        //
        // Measured, not assumed: this already failed cheaply BEFORE the pre-sizing
        // was removed, because MessagePackReader.ReadMapHeader validates the
        // declared count against the bytes actually remaining and throws before the
        // formatter allocates anything. Removing the pre-size is therefore defence
        // in depth — it keeps the allocation proportional to what is really read
        // rather than to what the header claims — not the closure of an exploitable
        // hole. The depth guard is the defect that was genuinely open.
        var writer = new ArrayBufferWriter<byte>();
        var w = new MessagePackWriter(writer);
        w.WriteArrayHeader(2);
        w.Write((byte)8); // ForwardValueTag.Map
        w.WriteMapHeader(int.MaxValue);
        w.Flush();

        Assert.ThrowsExactly<EndOfStreamException>(() => Decode(writer.WrittenMemory));
    }

    [TestMethod]
    public void OverstatedArrayHeader_FailsCheaply()
    {
        var writer = new ArrayBufferWriter<byte>();
        var w = new MessagePackWriter(writer);
        w.WriteArrayHeader(2);
        w.Write((byte)9); // ForwardValueTag.Array
        w.WriteArrayHeader(int.MaxValue);
        w.Flush();

        Assert.ThrowsExactly<EndOfStreamException>(() => Decode(writer.WrittenMemory));
    }

    [TestMethod]
    public void MapKeysRoundTripUnderTheCollisionResistantComparer()
    {
        // Swapping StringComparer.Ordinal for the randomly seeded comparer must not
        // change lookup semantics: keys are still matched by exact ordinal value.
        var writer = new ArrayBufferWriter<byte>();
        var w = new MessagePackWriter(writer);
        w.WriteArrayHeader(2);
        w.Write((byte)8);
        w.WriteMapHeader(3);
        foreach (var key in new[] { "alpha", "Alpha", "ALPHA" })
        {
            w.Write(key);
            w.WriteArrayHeader(2);
            w.Write((byte)3); // String
            w.Write(key + "-value");
        }

        w.Flush();

        var map = (IReadOnlyDictionary<string, object?>)Decode(writer.WrittenMemory)!;

        // Case-distinct keys stay distinct — the comparer is collision resistant,
        // not case insensitive.
        Assert.AreEqual(3, map.Count);
        Assert.AreEqual("alpha-value", map["alpha"]);
        Assert.AreEqual("Alpha-value", map["Alpha"]);
        Assert.AreEqual("ALPHA-value", map["ALPHA"]);
    }

    private static object? Decode(ReadOnlyMemory<byte> payload)
    {
        var reader = new MessagePackReader(payload);
        return ForwardObjectFormatter.Instance.Deserialize(ref reader, ForwardMessagePackOptions.Instance);
    }

    private static ReadOnlyMemory<byte> BuildNestedMap(int depth)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        for (var i = 0; i < depth; i++)
        {
            writer.WriteArrayHeader(2);
            writer.Write((byte)8); // Map
            writer.WriteMapHeader(1);
            writer.Write("k");
        }

        writer.WriteNil();
        writer.Flush();
        return buffer.WrittenMemory;
    }

    private static ReadOnlyMemory<byte> BuildNestedArray(int depth)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        for (var i = 0; i < depth; i++)
        {
            writer.WriteArrayHeader(2);
            writer.Write((byte)9); // Array
            writer.WriteArrayHeader(1);
        }

        writer.WriteNil();
        writer.Flush();
        return buffer.WrittenMemory;
    }
}
