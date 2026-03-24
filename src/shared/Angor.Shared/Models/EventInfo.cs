namespace Angor.Shared.Models;

/// <summary>
/// Wrapper class that contains event metadata along with the deserialized content.
/// </summary>
/// <typeparam name="T">The type of the deserialized event content.</typeparam>
public class EventInfo<T>
{
    /// <summary>
    /// The unique identifier of the Nostr event.
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// The deserialized content of the event.
    /// </summary>
    public T Data { get; }

    public EventInfo(string eventId, T data)
    {
        EventId = eventId;
        Data = data;
    }
}
