﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Valkyrie;

using Pact.Extensions.Contract;
using Pact.Extensions.Enumerable;
using Pact.Extensions.String;

namespace Pact
{
    public sealed class PowerLogEventStream
        : IEventStream
    {
        private readonly IConfigurationSource _configurationSource;
        private readonly IPowerLogEventParser _powerLogEventParser;
        private readonly IEventDispatcher _viewEventDispatcher;

        private bool _disposed;
        private readonly IList<IEventHandler> _eventHandlers = new List<IEventHandler>();
        private string _filePath;
        private readonly object _lock = new object();
        private readonly Queue<object> _parsedEvents = new Queue<object>();
        private string _remainingText;
        private bool _seekEndWhenFileChanges;
        private long _streamPosition;

        public PowerLogEventStream(
            IConfigurationSource configurationSource,
            IPowerLogEventParser powerLogEventParser,
            IEventDispatcher viewEventDispatcher,
            bool seekEndWhenFileChanges)
        {
            _configurationSource = configurationSource.Require(nameof(configurationSource));
            _powerLogEventParser = powerLogEventParser.Require(nameof(powerLogEventParser));
            _viewEventDispatcher = viewEventDispatcher.Require(nameof(viewEventDispatcher));

            _seekEndWhenFileChanges = seekEndWhenFileChanges;

            _filePath = _configurationSource.GetSettings().PowerLogFilePath;
            
            _eventHandlers.Add(
                new DelegateEventHandler<ViewEvents.ConfigurationSettingsSaved>(
                    __event =>
                    {
                        string newFilePath = _configurationSource.GetSettings().PowerLogFilePath;
                        if (!newFilePath.Eq(_filePath))
                        {
                            Task.Run(
                                () =>
                                {
                                    lock (_lock)
                                    {
                                        _remainingText = null;
                                        _streamPosition = 0L;

                                        _filePath = newFilePath;
                                        
                                        if (_seekEndWhenFileChanges)
                                            SeekEnd_();
                                    }
                                });
                        }
                    }));

            _eventHandlers.ForEach(__eventHandler => _viewEventDispatcher.RegisterHandler(__eventHandler));
        }

        async Task<object> IEventStream.ReadNext(
            CancellationToken? cancellationToken)
        {
            if (_parsedEvents.Count > 0)
                return _parsedEvents.Dequeue();

            while (true)
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                    return null;

                if (!File.Exists(_filePath))
                {
                    await Task.Delay(1000);

                    continue;
                }

                lock (_lock)
                {
                    ParsePowerLogEvents();
                    if (_parsedEvents.Count > 0)
                        return _parsedEvents.Dequeue();
                }

                await Task.Delay(1000);
            }
        }

        void IEventStream.SeekEnd()
        {
            SeekEnd_();
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        private void Dispose(
            bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    _eventHandlers.ForEach(__eventHandler => _viewEventDispatcher.UnregisterHandler(__eventHandler));

                _disposed = true;
            }
        }

        private void ParsePowerLogEvents()
        {
            using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < _streamPosition)
                {
                    _streamPosition = 0;

                    _remainingText = null;
                }

                stream.Seek(_streamPosition, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream))
                {
                    if (_remainingText != null)
                        _remainingText += Environment.NewLine;

                    _remainingText += reader.ReadToEnd();

                    _streamPosition = stream.Position;
                }
            }

            foreach (object parsedEvent in _powerLogEventParser.ParseEvents(ref _remainingText))
                _parsedEvents.Enqueue(parsedEvent);
        }

        private void SeekEnd_()
        {
            if (!File.Exists(_filePath))
                return;

            ParsePowerLogEvents();
            _parsedEvents.Clear();
        }
    }
}
