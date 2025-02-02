﻿namespace TelegramUpdater.UpdateChannels.ReadyToUse;

/// <summary>
/// An <see cref="IGenericUpdateChannel{T}"/> for <see cref="UpdateType.Message"/>.
/// </summary>
public sealed class MessageChannel : AnyChannel<Message>
{
    /// <summary>
    /// Initialize a new instance of <see cref="MessageChannel"/>
    /// to use as <see cref="IGenericUpdateChannel{T}"/>.
    /// </summary>
    /// <param name="timeOut">Timeout to wait for channel.</param>
    /// <param name="filter">Filter suitable update to channel within <paramref name="timeOut"/>.</param>
    public MessageChannel(TimeSpan timeOut, IFilter<Message>? filter = default)
        : base(UpdateType.Message, x => x.Message, timeOut, filter)
    {
    }
}
