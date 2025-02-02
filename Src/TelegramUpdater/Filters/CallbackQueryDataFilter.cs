﻿namespace TelegramUpdater.Filters
{
    /// <summary>
    /// A filter on <see cref="CallbackQuery.Data"/>
    /// </summary>
    public class CallbackQueryDataFilter : Filter<CallbackQuery>
    {
        /// <summary>
        /// A filter on <see cref="CallbackQuery.Data"/>
        /// </summary>
        public CallbackQueryDataFilter(Func<string, bool> filter)
            : base((_, x) => x.Data != null && filter(x.Data))
        { }
    }
}
