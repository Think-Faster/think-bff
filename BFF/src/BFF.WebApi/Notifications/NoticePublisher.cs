using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;

namespace BFF.WebApi.Notifications;

/// <summary>Тело сообщения tf.notifications (routing key "email") — контракт задан инфраструктурой
/// (репозиторий tf.infra, сервис tf-mail), не нами; менять состав/имена полей нельзя.</summary>
public sealed record EmailNotice(
    int Schema,
    Guid NoticeId,
    string Subject,
    string Text,
    NoticeTo To,
    object? TicketId = null,
    string? Kind = null,
    string? RequestId = null);

public sealed record NoticeTo(IReadOnlyList<string> Emails);

public interface INoticePublisher
{
    Task PublishEmailAsync(EmailNotice notice, CancellationToken ct);
}

/// <summary>Публикует уведомления в exchange tf.notifications — реальную отправку письма делает
/// отдельный сервис tf-mail (у него забирает очередь tf.notify.email); BFF не имеет и не должен иметь
/// прямого доступа к SMTP. НЕ объявляет exchange/очереди (ExchangeDeclare/QueueDeclare) — топологию
/// заводит инфраструктура, попытка объявить своё вернёт ACCESS_REFUSED и закроет канал.</summary>
public sealed class RabbitMqNoticePublisher : INoticePublisher, IAsyncDisposable
{
    private const string Exchange = "tf.notifications";
    private const string EmailRoutingKey = "email";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lazy<Task<IConnection>> _connection;

    public RabbitMqNoticePublisher(IConfiguration configuration)
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

        // Lazy<Task<T>> — соединение поднимается один раз, на первую публикацию, и живёт весь процесс
        // (раздел 4, п.5 задания: долгоживущее соединение, одно на приложение); параллельные первые
        // вызовы дожидаются той же задачи без гонки за созданием второго соединения.
        _connection = new Lazy<Task<IConnection>>(() => factory.CreateConnectionAsync("tf-bff"));
    }

    public async Task PublishEmailAsync(EmailNotice notice, CancellationToken ct)
    {
        var connection = await _connection.Value.WaitAsync(ct);

        // Раздел 4, п.5: канал не шарить между потоками одновременно — свой канал на публикацию,
        // соединение при этом одно на всё приложение.
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        try
        {
            var props = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = notice.NoticeId.ToString(),
            };

            // publisherConfirmationTrackingEnabled: ждёт ack брокера; nack/basic.return (mandatory —
            // сообщение без маршрута) бросает PublishException, которую вызывающий код трактует как
            // ошибку доставки (EmailSendStatus.Failed).
            await channel.BasicPublishAsync(
                exchange: Exchange,
                routingKey: EmailRoutingKey,
                mandatory: true,
                basicProperties: props,
                body: JsonSerializer.SerializeToUtf8Bytes(notice, Json),
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
