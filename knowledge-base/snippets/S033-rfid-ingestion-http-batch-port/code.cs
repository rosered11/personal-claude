// IngestionBatchPort.cs
// Hexagonal driving port: the ONLY edge-facing surface for the edge -> central WAN hop
// (D033). Stateless HTTPS/mTLS batch API; internal at-least-once publish reuses the
// platform's existing canonical-event-topic pipeline (EDA folded in internally, never
// exposed across the WAN hop itself). No MediatR/AutoMapper, per repo standard.

public sealed record EventEnvelope(
    string EventId,          // edge-generated, globally unique -> idempotency key
    string EventType,
    string SchemaVersion,
    string SiteId,
    DateTimeOffset OccurredAt,
    JsonElement Payload);

public sealed record BatchIngestRequest(string SiteId, IReadOnlyList<EventEnvelope> Events);

public enum EventAckStatus { Accepted, DuplicateIgnored, Rejected }

public sealed record EventAck(string EventId, EventAckStatus Status, string? Reason = null);

public sealed record BatchIngestResponse(IReadOnlyList<EventAck> Acks);

// Driven port: the Ingestion Service core depends on this abstraction, not a concrete
// broker/queue client. Implementation performs schema_version check, event_id dedupe,
// append-to-event-store, and publish-to-canonical-topics -- entirely internal, and
// unaffected by whatever transport protocol delivered the envelope.
public interface IEventIngestionPipeline
{
    Task<EventAck> IngestAsync(EventEnvelope envelope, CancellationToken ct);
}

[ApiController]
[Route("v1/events")]
public sealed class IngestionController : ControllerBase
{
    private readonly IEventIngestionPipeline _pipeline;

    public IngestionController(IEventIngestionPipeline pipeline) => _pipeline = pipeline;

    // Stateless: no session, no per-client affinity -- any replica behind the load
    // balancer can serve any site, satisfying the "scale to campaign peak, no
    // per-client session state" constraint directly.
    [HttpPost("batch")]
    [Authorize(AuthenticationSchemes = "ClientCertificate")]
    public async Task<ActionResult<BatchIngestResponse>> Batch(
        [FromBody] BatchIngestRequest request, CancellationToken ct)
    {
        if (request.Events.Count == 0)
            return BadRequest("Batch must contain at least one event.");

        // Bounded concurrency keeps the synchronous ack cheap under 7.7/11.11 peak load
        // without the endpoint itself becoming a serialized bottleneck.
        var acks = new EventAck[request.Events.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, request.Events.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct },
            async (i, token) => acks[i] = await _pipeline.IngestAsync(request.Events[i], token));

        // Every event_id gets an explicit verdict in THIS response -- the edge deletes
        // only the event_ids marked Accepted/DuplicateIgnored from its offline buffer.
        // Directly satisfies "edge must know for certain which batch server received."
        return Ok(new BatchIngestResponse(acks));
    }
}

// EdgeIngestionClient.cs -- runs on the DC Site Server / Store Gateway.
public sealed class EdgeIngestionClient
{
    private readonly HttpClient _http;               // mTLS client cert configured per site
    private readonly IOfflineEventBuffer _buffer;     // local, durable, FIFO-ordered (>=24h capacity)

    public EdgeIngestionClient(HttpClient http, IOfflineEventBuffer buffer)
    {
        _http = http;
        _buffer = buffer;
    }

    // Called by a background loop on reconnect / periodic tick; safe to call repeatedly
    // -- fully idempotent because every event carries its original edge-generated
    // event_id regardless of retry count.
    public async Task FlushAsync(string siteId, CancellationToken ct)
    {
        foreach (var batch in _buffer.ReadOrderedBatches(maxBatchSize: 500))
        {
            var request = new BatchIngestRequest(siteId, batch);
            try
            {
                var response = await _http.PostAsJsonAsync("v1/events/batch", request, ct);
                if (!response.IsSuccessStatusCode) continue; // leave in buffer, retry next tick

                var body = await response.Content.ReadFromJsonAsync<BatchIngestResponse>(ct);
                var confirmed = body!.Acks
                    .Where(a => a.Status is EventAckStatus.Accepted or EventAckStatus.DuplicateIgnored)
                    .Select(a => a.EventId);

                // Only purge what the server explicitly confirmed -- never assume
                // success from a bare 2xx, since a partial-batch rejection still
                // returns 200 with mixed per-event verdicts.
                _buffer.Acknowledge(confirmed);
            }
            catch (HttpRequestException)
            {
                // WAN down / firewall drop -- batch stays in the buffer, order
                // preserved, next tick retries. Plain HTTPS means this failure mode is
                // a simple connect-timeout, not a broker session/reconnect state
                // machine to manage.
                break;
            }
        }
    }
}
