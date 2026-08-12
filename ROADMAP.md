# Roadmap and integration gates

DeltaZulu.Forward's binary framing, typed handshake, credit-window flow
control, UUID deduplication, schema exchange, and logging provider are already
implemented. Remaining work is primarily integration and operational
validation rather than construction of another Forward state machine.

## Cross-repository reconciliation

Before planning DeltaZulu.Agent integration, reconcile its description of a
RELP-derived text transport and Avro payloads with this repository's fixed
16-byte binary header and MessagePack `ForwardLogBatch` contract. Determine
whether the Agent documentation is stale, Avro names a separate future catalog
encoding, or the repositories need a design-reconciliation ADR. Pending that
decision, scope Agent work as wiring the daemon to `ForwardSession` and
`ForwardConnection`, not reimplementing the protocol.

When DeltaZulu.Platform's schema-versioning work is designed, evaluate how
rule-to-parser-version binding composes with Forward's fingerprint negotiation
and `SchemaRequest`/`SchemaResponse` exchange. Transport schema discovery and
rule compatibility are adjacent guarantees, not interchangeable ones.

## Delivery correctness gate

Collectors may provide one shared `ForwardDedupWindow` to successive accepted
sessions, protecting reconnect redelivery while the process and bounded entry
remain alive. That is not durable idempotency. Before production alert
materialization, verify end to end that a batch acknowledged immediately before
a disconnect cannot be applied twice after either reconnect or collector
restart. The durable ingest layer must key its independent idempotency check on
the batch UUID.

## Production framework gate

The package currently targets experimental `net10.0`. Production adoption by
DeltaZulu.Agent is blocked until the maintainers explicitly decide whether to
multi-target a long-term-support framework and validate the selected deployment
runtime.

## Constraint: no fallback wire format

Do not add NDJSON or another degraded outage format to Forward. Spooling and
replay belong in the caller's transport adapter (for example,
`DeltaZulu.DurableBuffer`); a fallback here would become a second permanent
consumer contract and undermine type fidelity.
