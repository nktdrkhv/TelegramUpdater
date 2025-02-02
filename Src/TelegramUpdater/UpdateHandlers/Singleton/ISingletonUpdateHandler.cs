﻿namespace TelegramUpdater.UpdateHandlers.Singleton;

/// <summary>
/// Interface for normal update handler ( known as singleton handlers )
/// </summary>
public interface ISingletonUpdateHandler : IUpdateHandler
{
    /// <summary>
    /// Type of update.
    /// </summary>
    public UpdateType UpdateType { get; }

    /// <summary>
    /// Checks if an update can be handled in this
    /// <see cref="ISingletonUpdateHandler"/>.
    /// </summary>
    /// <param name="update">The update.</param>
    /// <param name="updater">The updater instance.</param>
    /// <returns></returns>
    public bool ShouldHandle(IUpdater updater, Update update);
}
