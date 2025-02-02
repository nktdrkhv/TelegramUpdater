﻿using Telegram.Bot.Types.ReplyMarkups;
using TelegramUpdater.RainbowUtilities;

namespace TelegramUpdater.UpdateHandlers.Scoped.ReadyToUse;

/// <summary>
/// Abstract scoped update handler for <see cref="UpdateType.Message"/>.
/// </summary>
public abstract class MessageHandler : AnyHandler<Message>
{
    /// <summary>
    /// Set handling priority of this handler.
    /// </summary>
    /// <param name="group">Handling priority group, The lower the sooner to process.</param>
    protected MessageHandler(int group = default)
        : base(x => x.Message, group)
    {
    }

    #region Extension Methods
    /// <inheritdoc cref="Message.From"/>.
    protected User? From => ActualUpdate.From;

    /// <inheritdoc cref="Message.Chat"/>.
    protected Chat Chat => ActualUpdate.Chat;

    /// <inheritdoc cref="Message.ReplyToMessage"/>.
    protected Message? RepliedTo => ActualUpdate.ReplyToMessage;

    /// <inheritdoc cref="Message.MessageId"/>.
    protected int Id => ActualUpdate.MessageId;
    /// <inheritdoc cref="Message.MessageThreadId"/>.
    protected int? ThreadId => ActualUpdate?.MessageThreadId;
    /// <summary>
    /// Check if the message is replied to another.
    /// </summary>
    protected bool IsReplied => RepliedTo is not null;

    /// <inheritdoc cref="TelegramBotClientExtensions.SendTextMessageAsync(
    /// ITelegramBotClient, ChatId, string, int?, ParseMode?,
    /// IEnumerable{MessageEntity}?, bool?, bool?, bool?, int?, bool?,
    /// IReplyMarkup?, CancellationToken)"/>.
    /// <remarks>This methos sends a message to the <see cref="Message.Chat"/></remarks>
    protected async Task<Message> ResponseAsync(
        string text, int? messageThreadId = null,
        ParseMode? parseMode = default,
        IEnumerable<MessageEntity>? entities = default,
        bool? disableWebPagePreview = default,
        bool? disableNotification = default,
        bool? protectContents = default,
        bool? sendMessageAsReply = default,
        bool? allowSendingWithoutReply = default,
        IReplyMarkup? replyMarkup = default,
        CancellationToken cancellationToken = default)
        => await BotClient.SendTextMessageAsync(
            Chat.Id, text, messageThreadId ?? ThreadId, parseMode, entities, disableWebPagePreview, disableNotification,
            protectContents, (sendMessageAsReply ?? false) ? Id : 0, allowSendingWithoutReply, replyMarkup,
            cancellationToken);

    /// <inheritdoc cref="TelegramBotClientExtensions.SendTextMessageAsync(
    /// ITelegramBotClient, ChatId, string, int?, ParseMode?,
    /// IEnumerable{MessageEntity}?, bool?, bool?, bool?, int?, bool?,
    /// IReplyMarkup?, CancellationToken)"/>.
    protected async Task<Message> SendTextMessageAsync(
        ChatId chatId, string text, int? messageThreadId = null, ParseMode? parseMode = default,
        IEnumerable<MessageEntity>? entities = default,
        bool? disableWebPagePreview = default,
        bool? disableNotification = default,
        bool? protectContents = default,
        int? replyToMessageId = default,
        bool? allowSendingWithoutReply = default,
        IReplyMarkup? replyMarkup = default,
        CancellationToken cancellationToken = default)
        => await BotClient.SendTextMessageAsync(
            chatId, text, messageThreadId ?? ThreadId, parseMode, entities, disableWebPagePreview, disableNotification,
            protectContents, replyToMessageId, allowSendingWithoutReply, replyMarkup, cancellationToken);

    /// <inheritdoc cref="TelegramBotClientExtensions.SendTextMessageAsync(
    /// ITelegramBotClient, ChatId, string, int?, ParseMode?,
    /// IEnumerable{MessageEntity}?, bool?, bool?, bool?, int?, bool?,
    /// IReplyMarkup?, CancellationToken)"/>.
    protected async Task<Message> SendTextMessageAsync(
        string text, int? messageThreadId = null, ParseMode? parseMode = default,
        IEnumerable<MessageEntity>? entities = default,
        bool? disableWebPagePreview = default,
        bool? disableNotification = default,
        bool? protectContents = default,
        int? replyToMessageId = default,
        bool? allowSendingWithoutReply = default,
        IReplyMarkup? replyMarkup = default,
        CancellationToken cancellationToken = default)
        => await BotClient.SendTextMessageAsync(
            Chat.Id, text, messageThreadId ?? ThreadId, parseMode, entities, disableWebPagePreview, disableNotification,
            protectContents, replyToMessageId, allowSendingWithoutReply, replyMarkup, cancellationToken);

    /// <inheritdoc cref="TelegramBotClientExtensions.DeleteMessageAsync(
    /// ITelegramBotClient, ChatId, int, CancellationToken)"/>
    protected async Task DeleteAsync(CancellationToken cancellationToken = default) =>
        await BotClient.DeleteMessageAsync(Chat.Id, Id, cancellationToken);

    /// <summary>
    /// Asks a user to input an text message and waits for it.
    /// </summary>
    public async Task<string?> AwaitTextInputAsync(
        TimeSpan timeOut,
        string text,
        int? messageThreadId = null,
        ParseMode? parseMode = default,
        IEnumerable<MessageEntity>? entities = default,
        bool? disableWebPagePreview = default,
        bool? disableNotification = default,
        bool? protectContents = default,
        bool? sendMessageAsReply = default,
        bool? allowSendingWithoutReply = default,
        IReplyMarkup? replyMarkup = default,
        Func<CancellationToken, Task>? onTimeOut = default,
        Func<
            IUpdater,
            ShiningInfo<long, Update>, Task>? onUnrelatedUpdate = default,
        CancellationToken cancellationToken = default)
    {
        if (text is not null)
            await ResponseAsync(
                text, messageThreadId ?? ThreadId, parseMode, entities, disableWebPagePreview,
                disableNotification, protectContents, sendMessageAsReply, allowSendingWithoutReply,
                replyMarkup, cancellationToken: cancellationToken);

        var update = await AwaitMessageAsync(
            FilterCutify.Text(), timeOut, onUnrelatedUpdate, cancellationToken);
        if (update == null)
        {
            if (onTimeOut is not null)
                await onTimeOut(cancellationToken);
            return null;
        }

        return update.Update.Text;
    }
    #endregion
}
