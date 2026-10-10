using System.Text;
using System.Text.Json;
using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Messages;
using HalfQR.Contracts.Options;
using HalfQR.QrEngine.Rendering;
using HalfQR.QrEngine.Storage;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HalfQR.Worker;

public class Worker(
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IRenderJobStore renderJobStore,
    IQrRenderService renderService,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Worker failed while consuming RabbitMQ jobs. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var options = rabbitMqOptions.Value;
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.RenderQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<RenderJobQueuedMessage>(args.Body.Span);

                if (message is null)
                {
                    throw new InvalidOperationException("Received an empty or invalid RabbitMQ job message.");
                }

                await ProcessAsync(message.JobId, cancellationToken);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process queued render job message.");
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: options.RenderQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        logger.LogInformation("HalfQR.Worker is consuming queue {QueueName}.", options.RenderQueueName);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        finally
        {
            await channel.BasicCancelAsync(consumerTag, cancellationToken: CancellationToken.None);
        }
    }

    private async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var state = await renderJobStore.GetAsync(jobId, cancellationToken);

        if (state is null)
        {
            logger.LogWarning("Skipping render job {JobId} because the job record could not be found.", jobId);
            return;
        }

        state = state with
        {
            Status = QrJobStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            FailureReason = null,
        };
        await renderJobStore.SaveAsync(state, cancellationToken);

        try
        {
            var rendered = await renderService.RenderAsync(state.Request, cancellationToken);
            var svgArtifact = await renderJobStore.SaveArtifactAsync(jobId, "svg", "image/svg+xml", Encoding.UTF8.GetBytes(rendered.SvgMarkup), cancellationToken);
            var pngArtifact = await renderJobStore.SaveArtifactAsync(jobId, "png", "image/png", rendered.PngBytes, cancellationToken);

            state = state with
            {
                Status = QrJobStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow,
                EncodedPayload = rendered.EncodedPayload,
                ResolvedTargetUrl = rendered.ResolvedTargetUrl,
                ConfigurationHash = rendered.ConfigurationHash,
                PayloadHash = rendered.PayloadHash,
                Artifacts = [svgArtifact, pngArtifact],
            };

            await renderJobStore.SaveAsync(state, cancellationToken);
            logger.LogInformation("Completed render job {JobId}.", jobId);
        }
        catch (Exception exception)
        {
            state = state with
            {
                Status = QrJobStatus.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                // Render validation errors are written for users; anything else may contain internal detail.
                FailureReason = exception is InvalidOperationException
                    ? exception.Message
                    : "Rendering failed due to an internal error.",
            };

            await renderJobStore.SaveAsync(state, cancellationToken);
            logger.LogError(exception, "Render job {JobId} failed.", jobId);
        }
    }
}
