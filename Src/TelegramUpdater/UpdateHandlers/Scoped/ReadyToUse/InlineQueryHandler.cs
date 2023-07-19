using Telegram.Bot.Types.InlineQueryResults;

namespace TelegramUpdater.UpdateHandlers.Scoped.ReadyToUse;

/// <summary>
/// Abstract scoped update handler for <see cref="UpdateType.Message"/>.
/// </summary>
public abstract class InlineQueryHandler
    : AnyHandler<InlineQuery>
{
    /// <summary>
    /// Set handling priority of this handler.
    /// </summary>
    /// <param name="group">Handling priority group, The lower the sooner to process.</param>
    protected InlineQueryHandler(int group = default)
        : base(x => x.InlineQuery, group)
    {
    }

    #region Extension Methods
    /// <inheritdoc cref="InlineQuery.From"/>.
    protected User From => ActualUpdate.From;

    /// <inheritdoc cref="InlineQuery.Id"/>.
    protected string Id => ActualUpdate.Id;

    /// <inheritdoc cref="InlineQuery.Query"/>.
    protected string Query => ActualUpdate.Query;

    /// <inheritdoc cref="InlineQuery.ChatType"/>.
    protected ChatType? ChatType => ActualUpdate.ChatType;

    /// <inheritdoc cref="TelegramBotClientExtensions.AnswerInlineQueryAsync(
    /// ITelegramBotClient, string, IEnumerable{InlineQueryResult},
    /// int?, bool?, string?, InlineQueryResultsButton?, CancellationToken)"/>.
    public async Task AnswerAsync(
        IEnumerable<InlineQueryResult> results,
        int? cacheTime = null,
        bool? isPersonal = null,
        string? nextOffset = null,
        InlineQueryResultsButton? button = null,
        CancellationToken cancellationToken = default)
    {
        await BotClient.AnswerInlineQueryAsync(
            Id, results, cacheTime, isPersonal, nextOffset,
            button, cancellationToken);
    }

    #endregion
}
