namespace FleetOps.Infrastructure.Messaging;

using RabbitMQ.Client;

public interface IRabbitMqConnection : IAsyncDisposable
{
    bool IsConnected { get; }
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
    Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = null, CancellationToken cancellationToken = default);
    Task InitializeTopologyAsync(CancellationToken cancellationToken = default);
}
