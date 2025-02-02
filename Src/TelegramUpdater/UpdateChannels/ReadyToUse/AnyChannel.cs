﻿namespace TelegramUpdater.UpdateChannels.ReadyToUse;

/// <summary>
/// Create channel for any type of <see cref="Update"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class AnyChannel<T> : AbstractChannel<T> where T : class
{
    internal AnyChannel(
        UpdateType updateType,
        Func<Update, T?> getT,
        TimeSpan timeOut,
        IFilter<T>? filter)
        : base(updateType, getT, timeOut, filter)
    { }
}
