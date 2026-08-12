namespace DeltaZulu.Forward;

/// <summary>
/// A bounded set of recently seen batch UUIDs that can span sessions when the collector shares
/// the same instance between them. At-least-once delivery makes
/// duplicate batches guaranteed rather than incidental, and per ADR-7 the receiving side (the
/// collector) is responsible for deduplicating before decode; this window is the mechanism.
/// The window is not tied to a <see cref="ForwardSession" /> and therefore can be reused by
/// fresh sessions after reconnect.
/// </summary>
public sealed class ForwardDedupWindow
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Queue<Guid> _order;
    private readonly HashSet<Guid> _seen;

    /// <summary>Initializes a dedup window bounded to the given number of batch identifiers.</summary>
    public ForwardDedupWindow(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Dedup window capacity must be positive.");
        }

        _capacity = capacity;
        _seen = new HashSet<Guid>(capacity);
        _order = new Queue<Guid>(capacity);
    }

    /// <summary>Gets the configured window capacity.</summary>
    public int Capacity => _capacity;

    /// <summary>Gets the number of batch identifiers currently tracked.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _seen.Count;
            }
        }
    }

    /// <summary>Determines whether the given batch identifier is currently tracked as already seen.</summary>
    public bool Contains(Guid batchId)
    {
        lock (_gate)
        {
            return _seen.Contains(batchId);
        }
    }

    /// <summary>
    /// Records the batch identifier as seen if it is not already present, evicting the oldest
    /// entry once the window is full.
    /// </summary>
    /// <returns><see langword="true" /> if the batch had not been seen before (admit it for processing); <see langword="false" /> if it is a duplicate.</returns>
    public bool TryAdmit(Guid batchId)
    {
        lock (_gate)
        {
            if (!_seen.Add(batchId))
            {
                return false;
            }

            _order.Enqueue(batchId);
            if (_order.Count > _capacity)
            {
                _seen.Remove(_order.Dequeue());
            }

            return true;
        }
    }

    /// <summary>
    /// Removes a previously admitted batch identifier when processing did not reach durable
    /// commit, allowing a later delivery to retry it.
    /// </summary>
    internal void Remove(Guid batchId)
    {
        lock (_gate)
        {
            if (!_seen.Remove(batchId))
            {
                return;
            }

            // Removal after a failed commit is uncommon, so rebuilding the bounded FIFO is
            // preferable to carrying another index solely for this path.
            var retained = _order.Where(id => id != batchId).ToArray();
            _order.Clear();
            foreach (var id in retained)
            {
                _order.Enqueue(id);
            }
        }
    }
}
