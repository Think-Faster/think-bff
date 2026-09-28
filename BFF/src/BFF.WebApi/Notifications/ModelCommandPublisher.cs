using System.Text.Json;
using System.Text.Json.Serialization;
using BFF.Models.Enums;
using RabbitMQ.Client;

namespace BFF.WebApi.Notifications;

/// <summary>Кто отдал команду: sub — id учётки в tf-auth, login — из токена, если есть.</summary>
public sealed record CommandIssuer(string Sub, string? Login);

/// <summary>
/// Команды модели в exchange tf.model.commands (Think-Faster ML/INTEGRATION.md §13.3): решения диспетчера
/// по прогнозу (decision.take / reject / mute / reopen) и подтверждённое происшествие (decision.confirmed).
/// Ключ маршрута — вид команды, command_id — id решения: повтор модель подтвердит без действия.
/// Топологию заводит think-infra (rabbitmq/definitions.json, право записи у tf-bff) — здесь не объявляется.
/// </summary>
public interface IModelCommandPublisher
{
    Task PublishAsync(string kind, Guid commandId, object payload, CommandIssuer issuer, string? requestId, CancellationToken ct);
}

public sealed class RabbitMqModelCommandPublisher : IModelCommandPublisher, IAsyncDisposable
{
    private const string Exchange = "tf.model.commands";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lazy<Task<IConnection>> _connection;

    public RabbitMqModelCommandPublisher(IConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RABBIT_HOST"] ?? "tf-rabbit",
            Port = int.TryParse(configuration["RABBIT_PORT"], out var port) ? port : 5672,
            VirtualHost = configuration["RABBIT_VHOST"] ?? "tf",
            UserName = configuration["RABBIT_USER"] ?? "tf-bff",
            Password = configuration["TF_RABBIT_BFF_PASSWORD"] ?? string.Empty,
            AutomaticRecoveryEnabled = true,
        };

        _connection = new Lazy<Task<IConnection>>(() => factory.CreateConnectionAsync("tf-bff-commands"));
    }

    public async Task PublishAsync(
        string kind, Guid commandId, object payload, CommandIssuer issuer, string? requestId, CancellationToken ct)
    {
        var envelope = new
        {
            Schema = 1,
            CommandId = commandId,
            Kind = kind,
            IssuedAt = DateTimeOffset.UtcNow,
            IssuedBy = issuer,
            RequestId = requestId,
            Payload = payload,
        };

        var connection = await _connection.Value.WaitAsync(ct);
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        try
        {
            var props = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = commandId.ToString(),
            };

            await channel.BasicPublishAsync(
                exchange: Exchange,
                routingKey: kind,
                mandatory: true,
                basicProperties: props,
                body: JsonSerializer.SerializeToUtf8Bytes(envelope, Json),
                cancellationToken: ct);
        }
        finally
        {
            await channel.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            var connection = await _connection.Value;
            await connection.CloseAsync();
        }
    }
}
