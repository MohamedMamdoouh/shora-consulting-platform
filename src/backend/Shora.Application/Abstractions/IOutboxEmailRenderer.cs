using Shora.Application.Common.Results;
using Shora.Application.Email.Outbox;
using Shora.Domain.Entities;

namespace Shora.Application.Abstractions;

public interface IOutboxEmailRenderer
{
    Task<Result<OutboxEmailRenderResult>> RenderAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}
