using System.Collections.Concurrent;
namespace DigiLine.API.Realtime;
public sealed record StaffSubscription(string ConnectionId, Guid SessionId, Guid StaffId, Guid Version, DateTime ExpiresAt);
public sealed class StaffSubscriptions
{
    private readonly ConcurrentDictionary<(string, Guid), StaffSubscription> subscriptions = new();
    public void Add(StaffSubscription subscription) => subscriptions[(subscription.ConnectionId, subscription.SessionId)] = subscription;
    public void Remove(string connection, Guid session) => subscriptions.TryRemove((connection, session), out _);
    public void RemoveConnection(string connection) { foreach (var key in subscriptions.Keys.Where(x => x.Item1 == connection)) subscriptions.TryRemove(key, out _); }
    public StaffSubscription[] ForSession(Guid session) => subscriptions.Values.Where(x => x.SessionId == session && x.ExpiresAt > DateTime.UtcNow).ToArray();
}
