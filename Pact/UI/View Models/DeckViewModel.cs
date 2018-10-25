﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Pact.Extensions.Contract;

namespace Pact
{
    public sealed class DeckViewModel
        : INotifyPropertyChanged
    {
        private readonly ICardInfoProvider _cardInfoProvider;
        private readonly IDeckImportInterface _deckImportInterface;
        private readonly IDeckInfoRepository _deckInfoRepository;
        private readonly IDecklistSerializer _decklistSerializer;
        private readonly AsyncSemaphore _deckPersistenceMutex;
        private readonly IPlayerDeckTrackerInterface _deckTrackerInterface;
        private readonly Valkyrie.IEventDispatcherFactory _eventDispatcherFactory;
        private readonly Valkyrie.IEventDispatcher _gameEventDispatcher;
        private readonly ILogger _logger;
        private readonly IBackgroundWorkInterface _notifyWaiter;
        private readonly Dispatcher _uiThreadDispatcher;
        private readonly IUserConfirmationInterface _userConfirmation;
        private readonly Valkyrie.IEventDispatcher _viewEventDispatcher;

        private bool _canDelete = true;
        private Action _deleteCanExecuteChanged;
        private readonly Func<DeckViewModel, int> _findDeckPosition;
        private readonly IList<GameResult> _gameResults;
        private bool _isTracking = false;

        public DeckViewModel(
            ICardInfoProvider cardInfoProvider,
            IDeckImportInterface deckImportInterface,
            IDeckInfoRepository deckInfoRepository,
            IDecklistSerializer decklistSerializer,
            AsyncSemaphore deckPersistenceMutex,
            IPlayerDeckTrackerInterface deckTrackerInterface,
            Valkyrie.IEventDispatcherFactory eventDispatcherFactory,
            Valkyrie.IEventDispatcher gameEventDispatcher,
            ILogger logger,
            IBackgroundWorkInterface notifyWaiter,
            Dispatcher uiThreadDispatcher,
            IUserConfirmationInterface userConfirmation,
            Valkyrie.IEventDispatcher viewEventDispatcher,
            Func<DeckViewModel, int> findPosition,
            Guid deckID,
            Decklist decklist,
            string title,
            IEnumerable<GameResult> gameResults = null)
        {
            _cardInfoProvider =
                cardInfoProvider.Require(nameof(cardInfoProvider));

            _deckImportInterface =
                deckImportInterface.Require(nameof(deckImportInterface));

            _deckInfoRepository =
                deckInfoRepository.Require(nameof(deckInfoRepository));

            _decklistSerializer =
                decklistSerializer.Require(nameof(decklistSerializer));

            _deckPersistenceMutex =
                deckPersistenceMutex.Require(nameof(deckPersistenceMutex));

            _deckTrackerInterface =
                deckTrackerInterface.Require(nameof(deckTrackerInterface));

            _eventDispatcherFactory =
                eventDispatcherFactory.Require(nameof(eventDispatcherFactory));

            _gameEventDispatcher =
                gameEventDispatcher.Require(nameof(gameEventDispatcher));

            _logger =
                logger.Require(nameof(logger));

            _notifyWaiter =
                notifyWaiter.Require(nameof(notifyWaiter));

            _uiThreadDispatcher =
                uiThreadDispatcher.Require(nameof(uiThreadDispatcher));

            _userConfirmation =
                userConfirmation.Require(nameof(userConfirmation));

            _viewEventDispatcher =
                viewEventDispatcher.Require(nameof(viewEventDispatcher));

            _findDeckPosition =
                findPosition.Require(nameof(findPosition));

            DeckID = deckID;
            Decklist = decklist;
            _gameResults = (gameResults ?? Enumerable.Empty<GameResult>()).ToList();
            Title = title ?? string.Empty;

            // Needs to get unregistered when deck is deleted so the view model can get garbage collected
            _viewEventDispatcher.RegisterHandler(
                new Valkyrie.DelegateEventHandler<DeckTrackingEvent>(
                    __event =>
                    {
                        IsTracking = __event.DeckViewModel == this;

                        _canDelete = !IsTracking;
                        _deleteCanExecuteChanged?.Invoke();

                        _canReplace = !IsTracking;
                        _replaceCanExecuteChanged?.Invoke();
                    }));
        }

        public string Class => _cardInfoProvider.GetCardInfo(Decklist.HeroID)?.Class;

        public ICommand CopyDeck =>
            new DelegateCommand(
                () =>
                {
                    _notifyWaiter.Perform(
                        async __setStatus =>
                        {
                            byte[] bytes = null;

                            using (var stream = new MemoryStream())
                            {
                                await _decklistSerializer.Serialize(stream, Decklist);

                                stream.Position = 0;

                                bytes = new byte[stream.Length];
                                stream.Read(bytes, 0, (int)stream.Length);
                            }

                            _uiThreadDispatcher.Invoke(() => Clipboard.SetText(Encoding.Default.GetString(bytes)));

                            __setStatus?.Invoke("Deck copied to clipboard!");

                            await Task.Delay(500);
                        },
                        750);
                });

        public Guid DeckID { get; private set; }

        public Decklist Decklist { get; private set; }

        public ICommand Delete =>
            new DelegateCommand(
                async () =>
                {
                    if (!await _userConfirmation.Confirm($"Are you sure you want to delete {Title}?", "Delete", "Cancel"))
                        return;
                    
                    _viewEventDispatcher.DispatchEvent(new Commands.DeleteDeck(DeckID));
                },
                canExecute:
                    () => _canDelete,
                canExecuteChangedClient:
                    __canExecuteChanged => _deleteCanExecuteChanged = __canExecuteChanged);

        public IEnumerable<GameResult> GameResults => _gameResults;

        public bool IsTracking
        {
            get => _isTracking;
            private set
            {
                _isTracking = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTracking"));
            }
        }

        public int Losses => _gameResults.Count(__gameResult => !__gameResult.GameWon);

        public int Position => _findDeckPosition(this);

        private bool _canReplace = true;
        private Action _replaceCanExecuteChanged;
        public ICommand Replace =>
            new DelegateCommand(
                async () =>
                {
                    DeckImportDetails? deckImportResult = await _deckImportInterface.GetDecklist();
                    if (!deckImportResult.HasValue)
                        return;

                    Decklist = deckImportResult.Value.Decklist;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Class"));

                    await SaveDeckToRepository();
                },
                canExecute:
                    () => _canReplace,
                canExecuteChangedClient:
                    __canExecuteChanged => _replaceCanExecuteChanged = __canExecuteChanged);

        public ICommand SaveDeckTitle =>
            new DelegateCommand(
                async () =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Title"));

                    await SaveDeckToRepository();
                });

        public string Title { get; set; }

        public ICommand TrackDeck =>
            new DelegateCommand(
                () =>
                {
                    _viewEventDispatcher.DispatchEvent(new DeckTrackingEvent(this));

                    _deckTrackerInterface.TrackDeck(Decklist);

                    var recordGameResult =
                        new Valkyrie.DelegateEventHandler<Events.GameEnded>(
                            async __event =>
                            {
                                var timestamp = DateTime.UtcNow;

                                string opponentClass = _cardInfoProvider.GetCardInfo(__event.OpponentHeroCardID)?.Class;

                                var gameResult = new GameResult(timestamp, __event.GameWon, opponentClass);

                                _gameResults.Add(gameResult);

                                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Wins"));
                                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Losses"));

                                // Make a distinct "game result storage" interface again?
                                await SaveDeckToRepository();
                            });

                    _gameEventDispatcher.RegisterHandler(recordGameResult);

                    Valkyrie.IEventHandler unregisterHandlers = null;
                    unregisterHandlers =
                        new Valkyrie.DelegateEventHandler<DeckTrackingEvent>(
                            __event =>
                            {
                                _gameEventDispatcher.UnregisterHandler(recordGameResult);
                                _viewEventDispatcher.UnregisterHandler(unregisterHandlers);
                            });

                    _viewEventDispatcher.RegisterHandler(unregisterHandlers);

                    IsTracking = true;
                });

        public ICommand UntrackDeck =>
            new DelegateCommand(
                () =>
                {
                    _deckTrackerInterface.Close();

                    _viewEventDispatcher.DispatchEvent(new DeckTrackingEvent(null));
                });

        public Valkyrie.IEventDispatcher ViewEventDispatcher => _viewEventDispatcher;

        public int Wins => _gameResults.Count(__gameResult => __gameResult.GameWon);

        public event PropertyChangedEventHandler PropertyChanged;

        private async Task SaveDeckToRepository()
        {
            using (await _deckPersistenceMutex.WaitAsync().ConfigureAwait(false))
            {
                DeckInfo deckInfo;

                using (var stream = new MemoryStream())
                {
                    await _decklistSerializer.Serialize(stream, Decklist);

                    stream.Position = 0;

                    using (var reader = new StreamReader(stream))
                        deckInfo = new DeckInfo(DeckID, reader.ReadToEnd(), Title, (ushort)_findDeckPosition(this), _gameResults);
                }

                await _deckInfoRepository.Save(deckInfo).ConfigureAwait(false);
            }
        }

        private sealed class DeckTrackingEvent
        {
            private readonly DeckViewModel _deckViewModel;

            public DeckTrackingEvent(
                DeckViewModel deckViewModel)
            {
                _deckViewModel = deckViewModel;
            }

            public DeckViewModel DeckViewModel => _deckViewModel;
        }
    }
}