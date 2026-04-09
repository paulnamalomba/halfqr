namespace HaveQR.Contracts.Options;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "haveqr";

    public string Password { get; set; } = "haveqr_dev_password";

    public string RenderQueueName { get; set; } = "haveqr.render.jobs";
}