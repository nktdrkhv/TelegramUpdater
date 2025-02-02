﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot.Args;
using TelegramUpdater.ExceptionHandlers;
using TelegramUpdater.RainbowUtilities;
using TelegramUpdater.UpdateHandlers;
using TelegramUpdater.UpdateHandlers.Scoped;
using TelegramUpdater.UpdateHandlers.Singleton;

namespace TelegramUpdater;

/// <summary>
/// Fetch updates from telegram and handle them.
/// </summary>
public sealed class Updater : IUpdater
{
    private readonly ITelegramBotClient _botClient;
    private readonly List<ISingletonUpdateHandler> _updateHandlers;
    private readonly List<IScopedUpdateHandlerContainer> _scopedHandlerContainers;
    private readonly List<IExceptionHandler> _exceptionHandlers;
    private readonly ILogger<IUpdater> _logger;
    private readonly Type? _preUpdateProcessorType;
    private readonly ConcurrentDictionary<string, object> _extraData;
    private UpdaterOptions _updaterOptions;
    private User? _me = null;

    // Updater can use this to cancel update processing when it's needed.
    private CancellationTokenSource? _emergencyCancel;

    // Updater can use this to change the behavior on scoped handlers.
    // If it's present, then DI will be available inside scoped handlers
    private readonly IServiceProvider? _serviceDescriptors;

    // This the main class responseable for queueing updates
    // It handles everything related to process priority and more
    private readonly Rainbow<long, Update> _rainbow;

    /// <summary>
    /// Creates an instance of updater to fetch updates from
    /// telegram and handle them.
    /// </summary>
    /// <param name="botClient">Telegram bot client</param>
    /// <param name="updaterOptions">Options for this updater.</param>
    /// <param name="serviceDescriptors">
    /// Optional service provider.
    /// </param>
    /// <param name="preUpdateProcessorType">
    /// Type of a class that will be initialized on every incoming update.
    /// It should be a sub-class of <see cref="AbstractPreUpdateProcessor"/>.
    /// <para>
    /// Your class should have a parameterless ctor if
    /// <paramref name="serviceDescriptors"/>
    /// is <see langword="null"/>.
    /// otherwise you can use items which are in services.
    /// </para>
    /// <para>
    /// Don't forget to add this to service collections if available.
    /// </para>
    /// </param>
    /// <param name="customKeyResolver">
    /// If you wanna customize the way updater resolves a sender id from
    /// <see cref="Update"/> 
    /// ( as queue keys ), you can pass your own. <b>Use with care!</b>
    /// </param>
    /// <param name="outgoingRateControl">
    /// <b>[BETA]</b> - Applies a wait if you cross the telegram limits border.
    /// <c>"Too Many Requests: retry after xxx"</c> Error.
    /// </param>
    public Updater(
        ITelegramBotClient botClient,
        UpdaterOptions updaterOptions = default,
        IServiceProvider? serviceDescriptors = default,
        Type? preUpdateProcessorType = default,
        Func<Update, long>? customKeyResolver = default,
        bool outgoingRateControl = false)
    {
        _botClient = botClient ??
            throw new ArgumentNullException(nameof(botClient));
        _extraData = new();

        if (outgoingRateControl)
            _botClient.OnApiResponseReceived += OnApiResponseReceived;

        _updaterOptions = updaterOptions;
        _preUpdateProcessorType = preUpdateProcessorType;

        if (_preUpdateProcessorType is not null)
        {
            if (!typeof(AbstractPreUpdateProcessor)
                .IsAssignableFrom(preUpdateProcessorType))
            {
                throw new InvalidOperationException(
                    $"Input type for preUpdateProcessorType " +
                    "( {preUpdateProcessorType} ) should be an" +
                    " instance of AbstractPreUpdateProcessor.");
            }

            if (serviceDescriptors is null)
            {
                if (preUpdateProcessorType
                    .GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"Input type for preUpdateProcessorType " +
                        "( {preUpdateProcessorType} ) should have" +
                        " an empty ctor when there's no service provider.");
                }
            }
        }

        _serviceDescriptors = serviceDescriptors;

        _updateHandlers = new List<ISingletonUpdateHandler>();
        _exceptionHandlers = new List<IExceptionHandler>();
        _scopedHandlerContainers = new List<IScopedUpdateHandlerContainer>();

        _rainbow = new Rainbow<long, Update>(
            updaterOptions.MaxDegreeOfParallelism ??
                Environment.ProcessorCount,

            customKeyResolver ?? QueueKeyResolver,
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

    private async ValueTask OnApiResponseReceived(
        ITelegramBotClient botClient,
        ApiResponseEventArgs args,
        CancellationToken cancellationToken = default)
    {
        if (args.ResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var failedApiResponseMessage = await args.ResponseMessage.Content
                .ReadAsStreamAsync(cancellationToken);

            var jsonObject = await JsonDocument.ParseAsync(
                failedApiResponseMessage, cancellationToken: cancellationToken);

            var description = jsonObject.RootElement.GetProperty("description")
                .GetString();

            if (description is null) return;

            var regex = new Regex(
                "^Too Many Requests: retry after (?<tryAfter>[0-9]*)$");
            Match match = regex.Match(description);
            if (match.Success)
            {
                var tryAfterSeconds = int.Parse(match.Groups["tryAfter"].Value);

                Logger.LogWarning("A wait of {seconds} is required! caused by {method}",
                    tryAfterSeconds, args.ApiRequestEventArgs.Request.MethodName);
                await Task.Delay(tryAfterSeconds * 1000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Creates an instance of updater to fetch updates from
    /// telegram and handle them.
    /// </summary>
    /// <param name="botToken">Your telegram bot token.</param>
    /// <param name="updaterOptions">Options for this updater.</param>
    /// <param name="preUpdateProcessorType">
    /// Type of a class that will be initialized on every incoming update.
    /// It should be a sub-class of <see cref="AbstractPreUpdateProcessor"/>.
    /// <para>
    /// Your class should have a parameterless ctor.
    /// </para>
    /// <para>
    /// Don't forget to add this to service collections if available.
    /// </para>
    /// </param>
    /// <param name="customKeyResolver">
    /// If you wanna customize the way updater resolves a sender id
    /// from <see cref="Update"/> 
    /// ( as queue keys ), you can pass your own. <b>Use with care!</b>
    /// </param>
    public Updater(string botToken,
        UpdaterOptions updaterOptions = default,
        Type? preUpdateProcessorType = default,
        Func<Update, long>? customKeyResolver = default)
        : this(new TelegramBotClient(botToken), updaterOptions,
              preUpdateProcessorType: preUpdateProcessorType,
              customKeyResolver: customKeyResolver)
    { }

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
    public IEnumerable<IScopedUpdateHandlerContainer> ScopedHandlerContainers => _scopedHandlerContainers;

    /// <inheritdoc/>
    public IEnumerable<ISingletonUpdateHandler> SingletonUpdateHandlers => _updateHandlers;

    /// <inheritdoc/>
    public object this[string key]
    {
        get => _extraData[key];
        set => _extraData.AddOrUpdate(key, value, (key, value) => value);
    }

    public bool TryRemove(string key, out object? value) => _extraData.TryRemove(key, out value);

    /// <inheritdoc/>
    public void EmergencyCancel()
    {
        _logger.LogWarning("Emergency cancel triggered.");
        _emergencyCancel?.Cancel();
    }

    /// <inheritdoc/>
    public bool ContainsKey(string key) => _extraData.ContainsKey(key);

    /// <inheritdoc/>
    public Updater AddSingletonUpdateHandler(ISingletonUpdateHandler updateHandler)
    {
        _updateHandlers.Add(updateHandler);
        _logger.LogInformation($"Added new singleton handler.");
        return this;
    }

    /// <inheritdoc/>
    public Updater AddScopedUpdateHandler(
        IScopedUpdateHandlerContainer scopedHandlerContainer)
    {
        _scopedHandlerContainers.Add(scopedHandlerContainer);
        _logger.LogInformation("Added new scoped handler {handler}",
            scopedHandlerContainer.ScopedHandlerType);
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
    public async ValueTask WriteAsync(
        Update update, CancellationToken cancellationToken = default)
    {
        await Rainbow.EnqueueAsync(update, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StartAsync<TWriter>(
        CancellationToken cancellationToken = default)
        where TWriter : UpdateWriterAbs, new()
    {
        if (cancellationToken == default)
        {
            _logger.LogInformation(
                "Start's CancellationToken set to " +
                "CancellationToken in UpdaterOptions");
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

            _logger.LogInformation(
                "Detected allowed updates automatically {allowed}",
                string.Join(", ", AllowedUpdates.Select(x => x.ToString()))
            );
        }

        // Link tokens. so we can use _emergencyCancel when required.
        _emergencyCancel = new CancellationTokenSource();
        using var liked = CancellationTokenSource.CreateLinkedTokenSource(
            _emergencyCancel.Token, cancellationToken);

        var writer = new TWriter();
        writer.SetUpdater(this);

        _logger.LogInformation(
            "Start reading updates from {writer}", typeof(TWriter));
        await writer.ExecuteAsync(liked.Token);
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

    private long QueueKeyResolver(Update update)
    {
        var userId = update.GetSenderId();

        if (userId is null)
        {
            if (UpdaterOptions.SwitchChatId)
            {
                var chatId = update.GetChatId();
                if (chatId is null) return 0;
                return chatId.Value;
            }
            return 0;
        }
        return userId.Value;
    }

    private UpdateType[] DetectAllowedUpdates()
        => _updateHandlers
            .Select(x => x.UpdateType)
            .Concat(_scopedHandlerContainers.Select(x => x.UpdateType))
            .Distinct()
            .ToArray();

    private Task ShineErrors(
        Exception exception, CancellationToken cancellationToken)
    {
        Logger.LogError(exception: exception, message: "Error in Rainbow!");
        return Task.CompletedTask;
    }

    private async Task ShineCallback(
        ShiningInfo<long, Update> shiningInfo,
        CancellationToken cancellationToken)
    {
        if (shiningInfo == null)
            return;

        var servicesAvailabe = _serviceDescriptors is not null;

        if (_preUpdateProcessorType != null)
        {
            AbstractPreUpdateProcessor processor;
            if (servicesAvailabe)
            {
                using var scope = _serviceDescriptors!.CreateScope();
                processor = (AbstractPreUpdateProcessor)scope
                    .ServiceProvider
                    .GetRequiredService(_preUpdateProcessorType);
            }
            else
            {
                processor = (AbstractPreUpdateProcessor)Activator
                    .CreateInstance(_preUpdateProcessorType)!;
                processor.SetUpdater(this);
            }

            if (!await processor.PreProcessor(shiningInfo))
            {
                return;
            }
        }

        if (servicesAvailabe)
        {
            await ProcessUpdateFromServices(
                shiningInfo, cancellationToken);
        }
        else
        {
            await ProcessUpdate(shiningInfo, cancellationToken);
        }
    }

    private async Task ProcessUpdateFromServices(
        ShiningInfo<long, Update> shiningInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_serviceDescriptors == null)
                throw new InvalidOperationException(
                    "Can't ProcessUpdateFromServices when" +
                    " there is no ServiceProvider.");

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var scopedHandlers = _scopedHandlerContainers
                .Where(x => x.ShouldHandle(this, shiningInfo.Value));

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
                    if (!await HandleHandler(
                        shiningInfo, handler, cancellationToken))
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(
                exception: e, "Error in ProcessUpdateFromServices.");
        }
    }

    private async Task ProcessUpdate(
        ShiningInfo<long, Update> shiningInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var singletonhandlers = _updateHandlers
                .Where(x => x.ShouldHandle(this, shiningInfo.Value))
                .Select(x => (IUpdateHandler)x);

            var scopedHandlers = _scopedHandlerContainers
                .Where(x => x.ShouldHandle(this, shiningInfo.Value))
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

                if (!await HandleHandler(
                    shiningInfo, handler, cancellationToken))
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
                .Where(x => x.MessageMatched(this, ex.Message));

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
}
