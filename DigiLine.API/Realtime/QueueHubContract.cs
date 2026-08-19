namespace DigiLine.API.Realtime;

public static class QueueHubContract
{
    public const string QueueChanged = "QueueChanged";

    public static string SessionGroup(Guid sessionId) =>
        $"session:{sessionId:N}";
}
