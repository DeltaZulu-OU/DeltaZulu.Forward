namespace DeltaZulu.Forward;

/// <summary>
/// A single KQL-aligned log record forwarded over DeltaZulu.Forward. <see cref="Fields" />
/// is restricted, after normalization, to the ten KQL scalar types (<see cref="bool" />,
/// <see cref="long" />, <see cref="double" />, <see cref="string" />,
/// <see cref="DateTimeOffset" />, <see cref="TimeSpan" />, <see cref="Guid" />,
/// <see cref="decimal" />, dynamic maps/arrays, and <see langword="null" />) — never an
/// upstream producer's internal event model.
/// </summary>
public sealed record ForwardLogRecord
{
    /// <summary>Gets the identifier of the delivery this record was produced for.</summary>
    public required string DeliveryId { get; init; }

    /// <summary>Gets the identifier of the agent that produced this record.</summary>
    public required string AgentId { get; init; }

    /// <summary>Gets the type of the source this record was read from.</summary>
    public required string SourceType { get; init; }

    /// <summary>Gets the name of the source this record was read from.</summary>
    public required string SourceName { get; init; }

    /// <summary>Gets the identifier of the profile that shaped this record, if any.</summary>
    public string? ProfileId { get; init; }

    /// <summary>Gets the version of the profile that shaped this record, if any.</summary>
    public string? ProfileVersion { get; init; }

    /// <summary>Gets the platform of the agent that produced this record, if known.</summary>
    public string? Platform { get; init; }

    /// <summary>Gets the hostname of the agent that produced this record, if known.</summary>
    public string? Hostname { get; init; }

    /// <summary>Gets the identifier of this record, unique within its batch.</summary>
    public required string RecordId { get; init; }

    /// <summary>Gets the instant this record was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the record's field values. After normalization, every value is one of the ten
    /// KQL scalar types, a dynamic map (<see cref="IReadOnlyDictionary{TKey, TValue}" />),
    /// a dynamic array (<see cref="IReadOnlyList{T}" />), or <see langword="null" />.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Fields { get; init; }

    // ---- FWD-CONTRACT-v2 provenance -------------------------------------------------
    //
    // Every field below records something knowable only at the moment of writing. None
    // of it can be backfilled: rows written without these cannot be given them later,
    // because the information was never captured. That is why they are contract fields
    // now rather than a contract revision later.
    //
    // All are optional so a v1 producer still round-trips, and all are nullable rather
    // than defaulted so "not sent" stays distinguishable from "sent as the zero value".

    /// <summary>Gets the collection tier that produced this record, if stated.</summary>
    /// <remarks>
    /// A null field's meaning is tier-dependent, so without this the same absence is
    /// both correct behaviour and a failure, and nothing downstream can tell which.
    /// </remarks>
    public ForwardCollectionTier? CollectionTier { get; init; }

    /// <summary>Gets the reason each absent field is absent, keyed by field name.</summary>
    /// <remarks>
    /// Sparse by design: only fields whose absence has a recorded cause appear. The
    /// map's absence is not the same as an empty map — the first means no producer
    /// stated any reason, the second means a producer stated there were none.
    /// </remarks>
    public IReadOnlyDictionary<string, ForwardNullReason>? NullReasons { get; init; }

    /// <summary>
    /// Gets the version of the tier-by-field availability matrix in force when this
    /// record was written.
    /// </summary>
    /// <remarks>
    /// A row written under matrix v3 must be interpreted under v3. Treating the matrix
    /// as current state means adding an enrichment tier next year silently rewrites the
    /// meaning of every null already in the lake.
    /// </remarks>
    public int? MatrixVersion { get; init; }

    /// <summary>Gets where this record's event time came from.</summary>
    /// <remarks>
    /// The cheapest field here and the one whose omission is least recoverable: without
    /// it, every event time in the lake is of unknown provenance, permanently.
    /// </remarks>
    public ForwardTimestampOrigin? TimestampOrigin { get; init; }

    /// <summary>Gets the identifier of the parser that extracted this record's fields.</summary>
    public string? ParserId { get; init; }

    /// <summary>Gets the version of the parser that extracted this record's fields.</summary>
    public int? ParserVersion { get; init; }

    /// <summary>Gets a hash of the rulebase in force at extraction.</summary>
    /// <remarks>
    /// Distinct hashes for one <see cref="ParserId" /> across a fleet mean a partial
    /// rollout or a stale relay — a condition with no other symptom.
    /// </remarks>
    public string? RulebaseHash { get; init; }

    /// <summary>
    /// Gets the identifier of the agent that performed extraction, when that is not the
    /// agent that acquired the record.
    /// </summary>
    /// <remarks>
    /// Under a multi-hop topology parsing may happen at any hop, so attributing a bad
    /// extraction requires knowing which hop did it.
    /// </remarks>
    public string? ParserLocationAgentId { get; init; }

    /// <summary>Gets the identifier of the agent that first acquired this record.</summary>
    /// <remarks>
    /// Distinct from <see cref="AgentId" />, which names the sender. On a single hop the
    /// two agree; across relays they do not, and only this one identifies the origin.
    /// </remarks>
    public string? OriginAgentId { get; init; }

    /// <summary>Gets the number of forwarding hops this record has traversed.</summary>
    /// <remarks>Loop prevention, and the only observable of the fleet's actual topology.</remarks>
    public int? HopCount { get; init; }
}

/// <summary>A batch of <see cref="ForwardLogRecord" /> values forwarded as a single unit.</summary>
public sealed record ForwardLogBatch
{
    /// <summary>Gets the batch identifier, the unit of deduplication and acknowledgement.</summary>
    public required Guid BatchId { get; init; }

    /// <summary>Gets the instant this batch was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the records carried by this batch.</summary>
    public required IReadOnlyList<ForwardLogRecord> Records { get; init; }
}
