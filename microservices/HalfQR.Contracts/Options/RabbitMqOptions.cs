namespace HalfQR.Contracts.Options;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "halfqr";

    public string Password { get; set; } = string.Empty;

    public string RenderQueueName { get; set; } = "halfqr.render.jobs";
}