namespace DeltaZulu.Forward;

/// <summary>
/// Why a field is absent from a record, at <b>collection</b> time.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the type-contract catalogue's <c>KqlNullReason</c>, whose members
/// are specified and closed (CON-0015). It is redeclared here rather than
/// referenced because Forward is consumed by the agent and must not take a
/// dependency on the platform that owns the catalogue; the two must agree member
/// for member, and the wire values below are the contract that holds them
/// together.
/// </para>
/// <para>
/// This is <b>not</b> the conversion-loss enumeration. "The process exited" and
/// "the decimal overflowed" are different kinds of fact, and merging them would
/// leave no consumer able to tell a collection gap from a representation
/// failure — which is exactly the distinction provenance exists to record.
/// Conversion loss is <c>DeltaZulu.Kql.KqlLossReason</c>.
/// </para>
/// <para>
/// The enumeration is closed. Never switch over it with a <c>_ =&gt;</c>
/// fallthrough arm: a fallthrough silently absorbs any member added later.
/// Wire values are explicit and must never be renumbered — a renumbering
/// silently rewrites the meaning of every row already written.
/// </para>
/// </remarks>
public enum ForwardNullReason
{
    /// <summary>The field cannot exist at this host's collection tier.</summary>
    NotAvailableAtTier = 0,

    /// <summary>The process had already exited when the field was resolved.</summary>
    ProcessExited = 1,

    /// <summary>Resolution was refused by the operating system.</summary>
    AccessDenied = 2,

    /// <summary>The file had been deleted before it could be read.</summary>
    FileDeleted = 3,

    /// <summary>Collection of this field is disabled by policy.</summary>
    PolicyDisabled = 4,

    /// <summary>Hashing the referenced content failed.</summary>
    HashFailed = 5,

    /// <summary>An enrichment source was unavailable at resolution time.</summary>
    EnrichmentSourceUnavailable = 6,

    /// <summary>The record source that would have carried this field never arrived.</summary>
    RecordSourceMissing = 7,
}

/// <summary>
/// Where a record's event time came from.
/// </summary>
/// <remarks>
/// <para>
/// A fallback-assigned timestamp is indistinguishable from an observed one the
/// instant it is written, unless the fallback records itself. This enumeration is
/// that record, and it cannot be reconstructed later at any cost.
/// </para>
/// <para>
/// Anything other than <see cref="ParsedFromRecord"/> or
/// <see cref="SourceEnvelope"/> means the event time is an <b>estimate</b>. A
/// detection with a tight time window should be able to see that.
/// </para>
/// </remarks>
public enum ForwardTimestampOrigin
{
    /// <summary>The event body carried its own timestamp and it parsed.</summary>
    ParsedFromRecord = 0,

    /// <summary>Taken from a transport header, for example the RFC 5424 TIMESTAMP.</summary>
    SourceEnvelope = 1,

    /// <summary>File mtime, channel-supplied time, or an API response field.</summary>
    SourceMetadata = 2,

    /// <summary>The agent's own clock at read time — no event time existed.</summary>
    AgentObservation = 3,

    /// <summary>Inherited from the preceding record after a parse failure.</summary>
    PreviousRecordCarried = 4,

    /// <summary>Stamped on arrival at an aggregation node.</summary>
    CollectorReceipt = 5,
}

/// <summary>
/// The collection tier that produced a record.
/// </summary>
/// <remarks>
/// A null's meaning is tier-dependent: the same absent field is correct
/// behaviour on one tier and a failure on another. Without the tier on the
/// record, a Tier C host behaving perfectly and a Tier A host failing are
/// indistinguishable, and no aggregate can recover the difference.
/// </remarks>
public enum ForwardCollectionTier
{
    /// <summary>Unstated. Present so an unset value is never silently read as tier A.</summary>
    Unspecified = 0,

    /// <summary>Windows, Sysmon-derived: full detail, kernel-collected.</summary>
    A = 1,

    /// <summary>Windows, ETW Kernel-Process plus userland enrichment: partial detail.</summary>
    B = 2,

    /// <summary>Windows, Security 4688: minimal detail, LSASS-mediated.</summary>
    C = 3,

    /// <summary>Linux: identity reconstructed in userland, a weaker guarantee than tier A.</summary>
    Linux = 4,
}
