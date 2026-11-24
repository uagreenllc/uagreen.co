namespace UaGreen.Site.Api.Infrastructure.Chat;

public sealed class SiteChatOptions
{
    public string ApiKey { get; set; } = "";
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
