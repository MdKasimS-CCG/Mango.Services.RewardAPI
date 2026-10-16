using Microsoft.Extensions.Options;
using Mango.Services.RewardAPI.Data;
using Mango.Services.RewardAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Data.Common;
using System.Text;
using System.Text.Json;

namespace Mango.Services.RewardAPI.Messaging;

public sealed class OrderCreatedConsumer : BackgroundService
{
    private readonly OrderCreatedConsumerSettings _settings;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private IConnection? _connection;
    private IChannel? _channel;

    public OrderCreatedConsumer(
        IOptions<OrderCreatedConsumerSettings> options,
        ILogger<OrderCreatedConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _settings = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateSettings();

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: _settings.QueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; the finally block closes broker resources.
        }
        finally
        {
            if (_channel is not null)
                await _channel.DisposeAsync();

            if (_connection is not null)
                await _connection.DisposeAsync();
        }
    }

    private async Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs args)
    {
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.Span);
            var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(json)
                ?? throw new JsonException("OrderCreated payload was empty.");

            Validate(orderEvent);
            var pointsEarned = RewardPointsCalculator.Calculate(
                orderEvent.TotalAmount, orderEvent.Currency);

            var alreadyProcessed = await PersistRewardAsync(orderEvent, pointsEarned);
            if (alreadyProcessed)
                _logger.LogInformation("Order {OrderId} already has a reward record.", orderEvent.OrderId);

            await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
            _logger.LogInformation(
                "Persisted reward for order {OrderId}: {PointsEarned} points.",
                orderEvent.OrderId,
                pointsEarned);
        }
        catch (Exception ex) when (IsUniqueOrderIdViolation(ex))
        {
            _logger.LogInformation(ex, "Order {OrderId} was persisted concurrently; acknowledging duplicate delivery.",
                TryGetOrderId(args.Body));
            await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            var retry = IsTransientFailure(ex);
            _logger.LogError(ex,
                "Could not process OrderCreated delivery {DeliveryTag}; requeue={Requeue}.",
                args.DeliveryTag,
                retry);

            if (_channel is not null && _channel.IsOpen)
                await _channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: retry);
        }
    }

    private async Task<bool> PersistRewardAsync(OrderCreatedEvent orderEvent, int pointsEarned)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await dbContext.Rewards.AnyAsync(reward => reward.OrderId == orderEvent.OrderId))
            return true;

        dbContext.Rewards.Add(new Rewards
        {
            UserId = orderEvent.UserId,
            OrderId = orderEvent.OrderId,
            RewardsDate = AsUtc(orderEvent.OccurredAtUtc),
            // The legacy column has no defined use in this service; points use PointsEarned.
            RewardsActivity = 0,
            PointsEarned = pointsEarned
        });

        await dbContext.SaveChangesAsync();
        return false;
    }

    private static void Validate(OrderCreatedEvent orderEvent)
    {
        if (orderEvent.EventId == Guid.Empty ||
            orderEvent.EventType != "OrderCreated" ||
            orderEvent.ContractVersion != 1 ||
            orderEvent.OrderId <= 0 ||
            string.IsNullOrWhiteSpace(orderEvent.UserId) ||
            orderEvent.OccurredAtUtc == default ||
            orderEvent.Items is null)
        {
            throw new InvalidDataException("OrderCreated payload is invalid.");
        }
    }

    private static DateTime AsUtc(DateTime occurredAtUtc) => occurredAtUtc.Kind switch
    {
        DateTimeKind.Local => occurredAtUtc.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
        _ => occurredAtUtc
    };

    private static bool IsUniqueOrderIdViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException &&
                sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627) &&
                sqlException.Message.Contains("IX_Rewards_OrderId", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTransientFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException || current is DbException { IsTransient: true })
                return true;
        }

        return false;
    }

    private static int? TryGetOrderId(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<OrderCreatedEvent>(body.Span)?.OrderId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.HostName))
            throw new InvalidOperationException("MessageQueue:RabbitMQ:HostName is required.");
        if (string.IsNullOrWhiteSpace(_settings.UserName))
            throw new InvalidOperationException("MessageQueue:RabbitMQ:UserName is required.");
        if (string.IsNullOrWhiteSpace(_settings.Password))
            throw new InvalidOperationException("MessageQueue:RabbitMQ:Password is required.");
        if (_settings.Port is < 1 or > 65535)
            throw new InvalidOperationException("MessageQueue:RabbitMQ:Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(_settings.QueueName))
            throw new InvalidOperationException("TopicAndQueueNames:OrderCreatedRewardQueue is required.");
    }
}
