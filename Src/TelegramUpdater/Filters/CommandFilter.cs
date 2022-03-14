﻿namespace TelegramUpdater.Filters
{
    /// <summary>
    /// Filters messages with specified command
    /// </summary>
    /// <remarks>
    /// <b>Extra data:</b> <see cref="IEnumerable{String}"/> "args".
    /// </remarks>
    public class CommandFilter : Filter<Message>
    {
        private string[]? _commands;

        /// <summary>
        /// A set of commands to match without prefix.
        /// </summary>
        /// <remarks>
        /// Text of the command, 1-32 characters. Can contain only lowercase English letters,
        /// digits and underscores.
        /// </remarks>
        public string[] Commands
        {
            get => _commands!;
            private set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length == 0)
                    throw new ArgumentException($"Don't use empty collection as Commands");

                foreach (var command in value)
                {
                    if (string.IsNullOrEmpty(command))
                    {
                        throw new ArgumentException("Commands should not be null or empty.");
                    }
                }

                _commands = value;
            }
        }

        /// <summary>
        /// Command prefix ( mainly '/' )
        /// </summary>
        public char Prefix { get; } = '/';

        /// <summary>
        /// Command filter options.
        /// </summary>
        public CommandFilterOptions Options { get; } = default;

        /// <summary>
        /// Filters messages with specified command
        /// </summary>
        /// <remarks>
        /// Text of the command, 1-32 characters. Can contain only lowercase English letters,
        /// digits and underscores.
        /// </remarks>
        /// <param name="commands">Command that are allowed. default prefix '/' will be applied!</param>
        public CommandFilter(params string[] commands) : base()
        {
            Commands = commands;
        }

        /// <summary>
        /// Filters messages with specified command
        /// </summary>
        /// <remarks>
        /// Text of the command, 1-32 characters. Can contain only lowercase English letters,
        /// digits and underscores.
        /// </remarks>
        /// <param name="prefix">Prefix of command. default to '/'</param>
        /// <param name="commands">Command that are allowed</param>
        public CommandFilter(char prefix, params string[] commands)
        {
            Prefix = prefix;
            Commands = commands;
        }

        /// <summary>
        /// Filters messages with specified command
        /// </summary>
        /// <remarks>
        /// Text of the command, 1-32 characters. Can contain only lowercase English letters,
        /// digits and underscores.
        /// </remarks>
        /// <param name="prefix">Prefix of command. default to '/'</param>
        /// <param name="commands">Command that are allowed</param>
        /// <param name="argumentsMode">If command should carry arguments</param>
        public CommandFilter(
            char prefix,
            ArgumentsMode argumentsMode,
            params string[] commands)
        {
            Commands = commands;
            Prefix = prefix;
            Options = new CommandFilterOptions(argumentsMode);
        }

        /// <summary>
        /// Filters messages with specified command
        /// </summary>
        /// <remarks>
        /// Text of the command, 1-32 characters. Can contain only lowercase English letters,
        /// digits and underscores.
        /// </remarks>
        /// <param name="command">Command that are allowed</param>
        /// <param name="prefix">Prefix of command. default to '/'</param>
        /// <param name="argumentsMode">If command should carry arguments</param>
        public CommandFilter(
            string command,
            char prefix = '/',
            ArgumentsMode argumentsMode = ArgumentsMode.Idc)
        {
            Commands = new[] { command };
            Prefix = prefix;
            Options = new CommandFilterOptions(argumentsMode);
        }

        /// <summary>
        /// Filters messages with specified command
        /// </summary>
        /// <remarks>
        /// Text of the command, 1-32 characters. Can contain only lowercase English letters,
        /// digits and underscores.
        /// </remarks>
        /// <param name="command">Command that are allowed</param>
        /// <param name="prefix">Prefix of command. default to '/'</param>
        /// <param name="options">Options for command filter.</param>
        public CommandFilter(
            string command,
            CommandFilterOptions options,
            char prefix = '/')
        {
            Commands = new[] { command };
            Prefix = prefix;
            Options = options;
        }

        /// <summary>
        /// Filters messages with specified command
        /// </summary>
        /// <remarks>
        /// Text of the command, 1-32 characters. Can contain only lowercase English letters,
        /// digits and underscores.
        /// </remarks>
        /// <param name="commands">Commands that are allowed</param>
        /// <param name="prefix">Prefix of command. default to '/'</param>
        /// <param name="options">Options for command filter.</param>
        public CommandFilter(
            string[] commands,
            char prefix = '/',
            CommandFilterOptions options = default)
        {
            Commands = commands;
            Prefix = prefix;
            Options = options;
        }

        /// <inheritdoc/>
        public override bool TheyShellPass(Message input)
        {
            if (string.IsNullOrEmpty(input.Text)) return false;

            var args = input.Text.Split(Options.Separator);

            var command = args[0].ToLower().Trim();

            if (Options.ArgumentsMode == ArgumentsMode.Require &&
                args.Length < 2) return false;

            if (Options.ArgumentsMode == ArgumentsMode.NoArgs &&
                args.Length > 1) return false;

            var nakedArgs = args[1..];
            if (Options.JoinArgsFormIndex is not null)
            {
                var joined = string.Join(
                    Options.Separator,
                    nakedArgs[Options.JoinArgsFormIndex.Value..]);

                nakedArgs = nakedArgs[..Options.JoinArgsFormIndex.Value]
                    .Concat(new[] { joined }).ToArray();
            }

            AddOrUpdateData("args", nakedArgs);
            if (!string.IsNullOrEmpty(Options.BotUsername))
            {
                return Commands.Any(x => $"{Prefix}{x}@{Options.BotUsername}" == command);
            }
            else
            {
                return Commands.Any(x => command.StartsWith($"{Prefix}{x}"));
            }
        }

        /// <summary>
        /// Convert this to set of <see cref="BotCommand"/>.
        /// </summary>
        /// <remarks>
        /// Make sure there is a <see cref="CommandFilterOptions.Descriptions"/>
        /// and it contains descriptions for all commands in <see cref="Commands"/>.
        /// </remarks>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IEnumerable<(int priority, BotCommand command)> ToBotCommand()
        {
            if (Options.Descriptions is null)
                throw new InvalidOperationException(
                    "Commands should have a description to convert to BotCommand");

            int[] setPriorities;
            if (Options.SetCommandPriorities is null ||
                Options.SetCommandPriorities.Length != Commands.Length)
            {
                setPriorities = new int[Commands.Length];
            }
            else
            {
                setPriorities = Options.SetCommandPriorities;
            }


            if (Commands.Length != Options.Descriptions.Length)
                throw new InvalidOperationException(
                    "Descriptions count dose not match commands count");

            var commands = Commands.Zip(Options.Descriptions).Select(x => new BotCommand
            {
                Command = x.First,
                Description = x.Second
            });

            return setPriorities.Zip(commands);
        }
    }
}
