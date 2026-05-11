using System.Threading.Channels;

namespace BiasAudit.Api.Services;

public interface IBackgroundAuditQueue
{
    ValueTask EnqueueAsync(Guid auditId, CancellationToken cancellationToken);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class BackgroundAuditQueue : IBackgroundAuditQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid auditId, CancellationToken cancellationToken) =>
        _queue.Writer.WriteAsync(auditId, cancellationToken);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAsync(cancellationToken);
}
