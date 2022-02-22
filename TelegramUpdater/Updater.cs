﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramUpdater.ExceptionHandlers;
using TelegramUpdater.RainbowUtlities;
using TelegramUpdater.UpdateHandlers;
using TelegramUpdater.UpdateHandlers.ScopedHandlers;
using TelegramUpdater.UpdateWriters;

namespace TelegramUpdater
{
    /// <summary>
    /// Fetch updates from telegram and handle them.
    /// </summary>
    public class Updater : IUpdater
    {
        private readonly ITelegramBotClient _botClient;
        private readonly List<ISingletonUpdateHandler> _updateHandlers;
        private readonly List<IScopedHandlerContainer> _scopedHandlerContainers;
        private readonly List<IExceptionHandler> _exceptionHandlers;
        private readonly ILogger<IUpdater> _logger;
        private UpdaterOptions _updaterOptions;
        private User? _me = null;

        // Updater can use this to cancel update processing when it's needed.
        private CancellationTokenSource? _emergencyCancel;

        // Updater can use this to change the behavior on scoped handlers.
        // If it's present, then DI will be available inside scoped handlers
        private readonly IServiceProvider? _serviceDescriptors;

        // This the main class responseable for queueing updates
        // It handles everything related to process priotiry and more
        private readonly Rainbow<long, Update> _rainbow;

        /// <summary>
        /// Creates an instance of updater to fetch updates from telegram and handle them.
        /// </summary>
        /// <param name="botClient">Telegram bot client</param>
        /// <param name="updaterOptions">Options for this updater.</param>
        /// <param name="serviceDescriptors">Optional service provider.</param>
        public Updater(
            ITelegramBotClient botClient,
            UpdaterOptions updaterOptions = default,
            IServiceProvider? serviceDescriptors = default)
        {
            _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
            _updaterOptions = updaterOptions;

            _serviceDescriptors = serviceDescriptors;

            _updateHandlers = new List<ISingletonUpdateHandler>();
            _exceptionHandlers = new List<IExceptionHandler>();
            _scopedHandlerContainers = new List<IScopedHandlerContainer>();

            _rainbow = new Rainbow<long, Update>(
                updaterOptions.MaxDegreeOfParallelism?? Environment.ProcessorCount,
                x => x.GetSenderId() ?? 0,
                ShineCallback, ShineErrors);

            if (_updaterOptions.Logger == null)
            {
                using var _loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });

                _logger = _loggerFactory.CreateLogger<Updater>();
            }
            else
            {
                _logger = _updaterOptions.Logger;
            }

            _logger.LogInformation("Logger initialized.");
        }

        /// <inheritdoc/>
        public UpdaterOptions UpdaterOptions => _updaterOptions;

        /// <inheritdoc/>
        public ITelegramBotClient BotClient => _botClient;

        /// <inheritdoc/>
        public ILogger<IUpdater> Logger => _logger;

        /// <inheritdoc/>
        public Rainbow<long, Update> Rainbow => _rainbow;

        /// <inheritdoc/>
        public UpdateType[] AllowedUpdates => _updaterOptions.AllowedUpdates;

        /// <inheritdoc/>
        public Updater AddUpdateHandler(ISingletonUpdateHandler updateHandler)
        {
            _updateHandlers.Add(updateHandler);
            _logger.LogInformation($"Added new singleton handler.");
            return this;
        }

        /// <inheritdoc/>
        public Updater AddScopedHandler(IScopedHandlerContainer scopedHandlerContainer)
        {
            var _h = scopedHandlerContainer.GetType();
            _scopedHandlerContainers.Add(scopedHandlerContainer);
            _logger.LogInformation("Added new scoped handler :: {Name}.", _h.Name);
            return this;
        }

        /// <inheritdoc/>
        public Updater AddExceptionHandler(IExceptionHandler exceptionHandler)
        {
            _exceptionHandlers.Add(exceptionHandler);
            _logger.LogInformation(
                "Added exception handler for {ExceptionType}",
                exceptionHandler.ExceptionType);
            return this;
        }

        /// <inheritdoc/>
        public async ValueTask WriteAsync(Update update, CancellationToken cancellationToken = default)
        {
            await Rainbow.EnqueueAsync(update, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StartAsync<TWriter>(CancellationToken cancellationToken = default)
            where TWriter: UpdateWriterAbs, new()
        {
            if (cancellationToken == default)
            {
                _logger.LogInformation("Start's CancellationToken set to CancellationToken in UpdaterOptions");
                cancellationToken = _updaterOptions.CancellationToken;
            }

            if (_updaterOptions.AllowedUpdates == null)
            {
                // I need to recreate the options since it's readonly.
                _updaterOptions = new UpdaterOptions(
                    UpdaterOptions.MaxDegreeOfParallelism,
                    UpdaterOptions.Logger,
                    UpdaterOptions.CancellationToken,
                    UpdaterOptions.FlushUpdatesQueue,
                    DetectAllowedUpdates()); // Auto detect allowed updates

                _logger.LogInformation("Detected allowed updates automatically {allowed}",
                    string.Join(", ", AllowedUpdates.Select(x=> x.ToString())));
            }

            // Link tokens. so we can use _emergencyCancel when required.
            _emergencyCancel = new CancellationTokenSource();
            using var liked = CancellationTokenSource.CreateLinkedTokenSource(
                _emergencyCancel.Token, cancellationToken);

            var writer = new TWriter();
            writer.SetUpdater(this);

            _logger.LogInformation("Start reading updates from {writer}", typeof(TWriter));
            await writer.ExecuteAsync(liked.Token);
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await StartAsync<SimpleUpdateWriter>(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<User> GetMeAsync()
        {
            if (_me == null)
            {
                _me = await _botClient.GetMeAsync();
            }

            return _me;
        }

        private UpdateType[] DetectAllowedUpdates()
            => _updateHandlers
                .Select(x => x.UpdateType)
                .Concat(_scopedHandlerContainers.Select(x => x.UpdateType))
                .Distinct()
                .ToArray();

        private Task ShineErrors(Exception exception, CancellationToken cancellationToken)
        {
            Logger.LogError(exception: exception, message: "Error in Rainbow!");
            return Task.CompletedTask;
        }

        private async Task ShineCallback(
            ShiningInfo<long, Update> shiningInfo, CancellationToken cancellationToken)
        {
            if (shiningInfo == null)
                return;

            if (_serviceDescriptors != null)
            {
                await ProcessUpdateFromServices(shiningInfo, cancellationToken);
            }
            else
            {
                await ProcessUpdate(shiningInfo, cancellationToken);
            }
        }

        private async Task ProcessUpdateFromServices(
            ShiningInfo<long, Update> shiningInfo, CancellationToken cancellationToken)
        {
            try
            {

                if (_serviceDescriptors == null)
                    throw new InvalidOperationException("Can't ProcessUpdateFromServices when there is no ServiceProvider.");

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var scopedHandlers = _scopedHandlerContainers
                    .Where(x => x.ShouldHandle(shiningInfo.Value));

                if (!scopedHandlers.Any())
                { 
                    return;
                }

                // valid handlers for an update should process one by one
                // This change can be cut off when throwing an specified exception
                // Other exceptions are redirected to ExceptionHandler.
                foreach (var container in scopedHandlers)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    using var scope = _serviceDescriptors.CreateScope();

                    var handler = container.CreateInstance(scope);

                    if (handler != null)
                    {
                        if (!await HandleHandler(shiningInfo, handler, cancellationToken))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(exception: e, "Error in ProcessUpdateFromServices.");
            }
        }

        private async Task ProcessUpdate(
            ShiningInfo<long, Update> shiningInfo, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var singletonhandlers = _updateHandlers
                    .Where(x => x.ShouldHandle(shiningInfo.Value))
                    .Select(x => (IUpdateHandler)x);

                var scopedHandlers = _scopedHandlerContainers
                    .Where(x => x.ShouldHandle(shiningInfo.Value))
                    .Select(x => x.CreateInstance())
                    .Where(x => x != null)
                    .Cast<IScopedUpdateHandler>()
                    .Select(x => (IUpdateHandler)x);

                var handlers = singletonhandlers.Concat(scopedHandlers)
                    .OrderBy(x => x.Group);

                if (!(handlers.Any() || scopedHandlers.Any()))
                {
                    return;
                }

                // valid handlers for an update should process one by one
                // This change can be cut off when throwing an specified exception
                // Other exceptions are redirected to ExceptionHandler.
                foreach (var handler in handlers)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!await HandleHandler(shiningInfo, handler, cancellationToken))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(exception: ex, "Error in ProcessUpdate.");
            }
        }

        /// <summary>
        /// Returns false to break.
        /// </summary>
        private async Task<bool> HandleHandler(
            ShiningInfo<long, Update> shiningInfo,
            IUpdateHandler handler,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            // Handle the shit.
            try
            {
                await handler.HandleAsync(this, shiningInfo);
            }
            // Cut handlers chain.
            catch (StopPropagation)
            {
                return false;
            }
            catch (ContinuePropagation)
            {
                return true;
            }
            catch (Exception ex)
            {
                // Do exception handlers
                var exHandlers = _exceptionHandlers
                    .Where(x => x.TypeIsMatched(ex.GetType()))
                    .Where(x => x.IsAllowedHandler(handler.GetType()))
                    .Where(x => x.MessageMatched(ex.Message));

                foreach (var exHandler in exHandlers)
                {
                    await exHandler.Callback(this, ex);
                }
            }
            finally
            {
                if (handler is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public void EmergencyCancel()
        {
            _logger.LogWarning("Emergency cancel triggered.");
            _emergencyCancel?.Cancel();
        }
    }
}