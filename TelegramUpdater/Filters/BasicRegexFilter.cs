﻿using System.Text.RegularExpressions;

namespace TelegramUpdater.Filters
{
    public class BasicRegexFilter<T> : Filter<T>
    {
        public BasicRegexFilter(
            Func<T, string?> getText,
            string pattern,
            RegexOptions? regexOptions = default)
                : base(x =>
                {
                    var text = getText(x);

                    if (string.IsNullOrEmpty(text)) return false;

                    var matches = Regex.Matches(
                        text, pattern, regexOptions ?? RegexOptions.None, TimeSpan.FromSeconds(3));

                    if (matches.Count > 0)
                    {
                        return true;
                    }

                    return false;
                })
        { }
    }

    public sealed class StringRegex : BasicRegexFilter<string>
    {
        public StringRegex(string pattern, RegexOptions? regexOptions = null)
            : base(x => x, pattern, regexOptions)
        {
        }
    }

    public sealed class CallbackQueryRegex : BasicRegexFilter<CallbackQuery>
    {
        public CallbackQueryRegex(
            string pattern,
            RegexOptions? regexOptions = RegexOptions.None)
                : base(x => x.Data, pattern, regexOptions)
        {
        }
    }

    public sealed class MessageTextRegex : BasicRegexFilter<Message>
    {
        public MessageTextRegex(
            string pattern,
            bool catchCaption = false,
            RegexOptions? regexOptions = RegexOptions.None)
                : base(x =>
                {
                    return x switch
                    {
                        { Text: { } text } => text,
                        { Caption: { } caption } when catchCaption => caption,
                        _ => null
                    };
                }, pattern, regexOptions)
        { }
    }
}
