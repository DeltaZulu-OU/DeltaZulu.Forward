namespace DeltaZulu.Forward.Tests;

/// <summary>
/// FWD-CONTRACT-v2 provenance. The point of these fields is that they cannot be
/// reconstructed after the fact, so the tests that matter are the ones proving a
/// value survives the wire exactly and that an absent value stays absent rather
/// than becoming a plausible default.
/// </summary>
[TestClass]
public sealed class ForwardProvenanceTests
{
    [TestMethod]
    public void EveryProvenanceFieldRoundTrips()
    {
        var record = Minimal() with
        {
            CollectionTier = ForwardCollectionTier.B,
            NullReasons = new Dictionary<string, ForwardNullReason>
            {
                ["CommandLine"] = ForwardNullReason.ProcessExited,
                ["ImageHash"] = ForwardNullReason.HashFailed,
                ["OriginalFileName"] = ForwardNullReason.NotAvailableAtTier,
            },
            MatrixVersion = 3,
            TimestampOrigin = ForwardTimestampOrigin.AgentObservation,
            ParserId = "windows-process-creation",
            ParserVersion = 7,
            RulebaseHash = "sha256:9f2c",
            ParserLocationAgentId = "relay-02",
            OriginAgentId = "endpoint-41",
            HopCount = 2,
        };

        var decoded = RoundTrip(record);

        Assert.AreEqual(ForwardCollectionTier.B, decoded.CollectionTier);
        Assert.AreEqual(3, decoded.MatrixVersion);
        Assert.AreEqual(ForwardTimestampOrigin.AgentObservation, decoded.TimestampOrigin);
        Assert.AreEqual("windows-process-creation", decoded.ParserId);
        Assert.AreEqual(7, decoded.ParserVersion);
        Assert.AreEqual("sha256:9f2c", decoded.RulebaseHash);
        Assert.AreEqual("relay-02", decoded.ParserLocationAgentId);
        Assert.AreEqual("endpoint-41", decoded.OriginAgentId);
        Assert.AreEqual(2, decoded.HopCount);

        Assert.IsNotNull(decoded.NullReasons);
        Assert.AreEqual(3, decoded.NullReasons!.Count);
        Assert.AreEqual(ForwardNullReason.ProcessExited, decoded.NullReasons["CommandLine"]);
        Assert.AreEqual(ForwardNullReason.HashFailed, decoded.NullReasons["ImageHash"]);
        Assert.AreEqual(ForwardNullReason.NotAvailableAtTier, decoded.NullReasons["OriginalFileName"]);
    }

    [TestMethod]
    public void AbsentProvenanceStaysAbsent_RatherThanBecomingADefault()
    {
        // The whole design rests on "not stated" being distinguishable from "stated
        // as the zero value". A producer that never set CollectionTier must not be
        // read as having claimed tier A, and one that stated no null reasons must not
        // be confused with one that stated none existed.
        var decoded = RoundTrip(Minimal());

        Assert.IsNull(decoded.CollectionTier);
        Assert.IsNull(decoded.NullReasons);
        Assert.IsNull(decoded.MatrixVersion);
        Assert.IsNull(decoded.TimestampOrigin);
        Assert.IsNull(decoded.ParserId);
        Assert.IsNull(decoded.ParserVersion);
        Assert.IsNull(decoded.RulebaseHash);
        Assert.IsNull(decoded.ParserLocationAgentId);
        Assert.IsNull(decoded.OriginAgentId);
        Assert.IsNull(decoded.HopCount);
    }

    [TestMethod]
    public void EmptyNullReasonMapIsDistinctFromAnAbsentOne()
    {
        var decoded = RoundTrip(Minimal() with
        {
            NullReasons = new Dictionary<string, ForwardNullReason>(),
        });

        // "I checked and there were none" is a different claim from "I did not say".
        Assert.IsNotNull(decoded.NullReasons);
        Assert.AreEqual(0, decoded.NullReasons!.Count);
    }

    [TestMethod]
    public void UnspecifiedTierIsCarriedDistinctlyFromTierA()
    {
        // Unspecified exists precisely so an unset enum cannot be read as tier A.
        var decoded = RoundTrip(Minimal() with
        {
            CollectionTier = ForwardCollectionTier.Unspecified,
        });

        Assert.AreEqual(ForwardCollectionTier.Unspecified, decoded.CollectionTier);
        Assert.AreNotEqual(ForwardCollectionTier.A, decoded.CollectionTier);
    }

    [TestMethod]
    public void NullReasonWireValuesAreStable()
    {
        // Renumbering these silently rewrites the meaning of every row already
        // written, so the numbers are asserted rather than left to declaration order.
        Assert.AreEqual(0, (int)ForwardNullReason.NotAvailableAtTier);
        Assert.AreEqual(1, (int)ForwardNullReason.ProcessExited);
        Assert.AreEqual(2, (int)ForwardNullReason.AccessDenied);
        Assert.AreEqual(3, (int)ForwardNullReason.FileDeleted);
        Assert.AreEqual(4, (int)ForwardNullReason.PolicyDisabled);
        Assert.AreEqual(5, (int)ForwardNullReason.HashFailed);
        Assert.AreEqual(6, (int)ForwardNullReason.EnrichmentSourceUnavailable);
        Assert.AreEqual(7, (int)ForwardNullReason.RecordSourceMissing);

        // CON-0015 fixes the membership at exactly these eight.
        Assert.AreEqual(8, Enum.GetValues<ForwardNullReason>().Length);
    }

    [TestMethod]
    public void TimestampOriginWireValuesAreStable()
    {
        Assert.AreEqual(0, (int)ForwardTimestampOrigin.ParsedFromRecord);
        Assert.AreEqual(1, (int)ForwardTimestampOrigin.SourceEnvelope);
        Assert.AreEqual(2, (int)ForwardTimestampOrigin.SourceMetadata);
        Assert.AreEqual(3, (int)ForwardTimestampOrigin.AgentObservation);
        Assert.AreEqual(4, (int)ForwardTimestampOrigin.PreviousRecordCarried);
        Assert.AreEqual(5, (int)ForwardTimestampOrigin.CollectorReceipt);
        Assert.AreEqual(6, Enum.GetValues<ForwardTimestampOrigin>().Length);
    }

    [TestMethod]
    public void OnlyParsedOrEnvelopeOriginsAreObservedRatherThanEstimated()
    {
        // Encoded as a test because it is the interpretation rule a consumer needs
        // and it lives nowhere executable otherwise.
        ForwardTimestampOrigin[] observed =
        [
            ForwardTimestampOrigin.ParsedFromRecord,
            ForwardTimestampOrigin.SourceEnvelope,
        ];
        ForwardTimestampOrigin[] estimated =
        [
            ForwardTimestampOrigin.SourceMetadata,
            ForwardTimestampOrigin.AgentObservation,
            ForwardTimestampOrigin.PreviousRecordCarried,
            ForwardTimestampOrigin.CollectorReceipt,
        ];

        CollectionAssert.AreEquivalent(
            Enum.GetValues<ForwardTimestampOrigin>(),
            observed.Concat(estimated).ToArray(),
            "A new TimestampOrigin member must be classified as observed or estimated.");
    }

    [TestMethod]
    public void AV1PayloadWithNoProvenanceStillDecodes()
    {
        // Backward compatibility: the rollout has a compatibility window during which
        // v1 producers are still sending.
        var batch = new ForwardLogBatch
        {
            BatchId = Guid.NewGuid(),
            Records = [Minimal()],
        };

        var decoded = ForwardLogBatchCodec.Decode(ForwardLogBatchCodec.Encode(batch));

        Assert.AreEqual(1, decoded.Records.Count);
        Assert.AreEqual("r-1", decoded.Records[0].RecordId);
        Assert.IsNull(decoded.Records[0].CollectionTier);
    }

    private static ForwardLogRecord Minimal() => new()
    {
        DeliveryId = "d-1",
        AgentId = "a-1",
        SourceType = "file",
        SourceName = "/var/log/auth.log",
        RecordId = "r-1",
        CreatedAt = DateTimeOffset.UnixEpoch,
        Fields = new Dictionary<string, object?> { ["msg"] = "hello" },
    };

    private static ForwardLogRecord RoundTrip(ForwardLogRecord record)
    {
        var batch = new ForwardLogBatch { BatchId = Guid.NewGuid(), Records = [record] };
        return ForwardLogBatchCodec.Decode(ForwardLogBatchCodec.Encode(batch)).Records[0];
    }
}
