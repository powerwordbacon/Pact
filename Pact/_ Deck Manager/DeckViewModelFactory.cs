﻿using System;
using System.Collections.Generic;
using Pact.Extensions.Contract;

namespace Pact
{
    public sealed class DeckViewModelFactory
        : IDeckViewModelFactory
    {
        private readonly ICardInfoProvider _cardInfoProvider;
        private readonly IConfigurationSettings _configurationSettings;
        private readonly Valkyrie.IEventDispatcherFactory _eventDispatcherFactory;
        private readonly IEventStreamFactory _eventStreamFactory;
        private readonly IGameResultStorage _gameResultStorage;
        private readonly ILogger _logger;

        public DeckViewModelFactory(
            ICardInfoProvider cardInfoProvider,
            IConfigurationSettings configurationSettings,
            Valkyrie.IEventDispatcherFactory eventDispatcherFactory,
            IEventStreamFactory eventStreamFactory,
            IGameResultStorage gameResultStorage,
            ILogger logger)
        {
            _cardInfoProvider = cardInfoProvider.ThrowIfNull(nameof(cardInfoProvider));
            _configurationSettings = configurationSettings.ThrowIfNull(nameof(configurationSettings));
            _eventDispatcherFactory = eventDispatcherFactory.ThrowIfNull(nameof(eventDispatcherFactory));
            _eventStreamFactory = eventStreamFactory.ThrowIfNull(nameof(eventStreamFactory));
            _gameResultStorage = gameResultStorage.ThrowIfNull(nameof(gameResultStorage));
            _logger = logger.ThrowIfNull(nameof(logger));
        }

        DeckViewModel IDeckViewModelFactory.Create(
            Valkyrie.IEventDispatcher gameEventDispatcher,
            Valkyrie.IEventDispatcher viewEventDispatcher,
            Guid deckID,
            Decklist decklist,
            IEnumerable<GameResult> gameResults)
        {
            return
                new DeckViewModel(
                    _cardInfoProvider,
                    _configurationSettings,
                    _eventDispatcherFactory,
                    _eventStreamFactory,
                    gameEventDispatcher,
                    _gameResultStorage,
                    _logger,
                    viewEventDispatcher,
                    deckID,
                    decklist,
                    gameResults);
        }
    }
}
