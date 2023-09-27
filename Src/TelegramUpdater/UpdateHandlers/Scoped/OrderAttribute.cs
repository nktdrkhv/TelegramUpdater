namespace TelegramUpdater.UpdateHandlers.Scoped;

/// <summary>
/// Defines the order of handling flow
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class OrderAttribute : Attribute
{
    /// <summary>
    /// Order itself
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>
    /// Add an order to ScopedHandler
    /// </summary>
    public OrderAttribute(int priority) => Priority = priority;
}