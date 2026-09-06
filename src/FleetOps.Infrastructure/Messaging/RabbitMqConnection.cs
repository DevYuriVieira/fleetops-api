namespace FleetOps.Infrastructure.Messaging;

using FleetOps.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

public sealed class RabbitMqConnection : IRabbitMqConnection
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private bool _topologyInitialized;
    private bool _disposed;

    public RabbitMqConnection(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
        _connectionFactory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
    }

    public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsConnected)
        {
            return _connection!;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                return _connection!;
            }

            _logger.LogInformation(
                "Connecting to RabbitMQ at {Host}:{Port}, vhost: {VirtualHost}",
                _options.Host,
                _options.Port,
                _options.VirtualHost);

            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            _logger.LogInformation("Successfully connected to RabbitMQ broker.");
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = null, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return options is not null
            ? await connection.CreateChannelAsync(options, cancellationToken)
            : await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    public async Task InitializeTopologyAsync(CancellationToken cancellationToken = default)
    {
        if (_topologyInitialized)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_topologyInitialized)
            {
                return;
            }

            _logger.LogInformation("Declaring RabbitMQ exchange and queue topology...");

            await using var channel = await CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: _options.DeadLetterExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: _options.MaintenanceDlqName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.MaintenanceDlqName,
                exchange: _options.DeadLetterExchangeName,
                routingKey: "maintenance.completed.dlq",
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var queueArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = "maintenance.completed.dlq"
            };

            await channel.QueueDeclareAsync(
                queue: _options.MaintenanceQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.MaintenanceQueueName,
                exchange: _options.ExchangeName,
                routingKey: "maintenance.completed",
                arguments: null,
                cancellationToken: cancellationToken);

            _topologyInitialized = true;
            _logger.LogInformation("RabbitMQ topology initialized successfully.");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }

        _connectionLock.Dispose();
    }
}
