using System.Text.Json;
using HalfQR.Contracts.Messages;
using HalfQR.Contracts.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace HalfQR.PublicApi.Services;

internal sealed class RabbitMqJobDispatcher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqJobDispatcher> logger)
{
    public async Task DispatchAsync(RenderJobQueuedMessage message, CancellationToken cancellationToken)
    {
        var rabbitMq = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = rabbitMq.HostName,
            Port = rabbitMq.Port,
            UserName = rabbitMq.UserName,
            Password = rabbitMq.Password,
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: rabbitMq.RenderQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: rabbitMq.RenderQueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation("Queued render job {JobId} on RabbitMQ queue {QueueName}.", message.JobId, rabbitMq.RenderQueueName);
    }
}