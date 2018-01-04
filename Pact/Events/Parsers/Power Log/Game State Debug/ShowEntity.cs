﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Pact.StringExtensions;

namespace Pact.EventParsers.PowerLog.GameStateDebug
{
    public sealed class ShowEntity
        : IGameStateDebugEventParser
    {
        private static readonly Regex s_showEntityUpdatingPattern =
            new Regex(
                @"^\s*SHOW_ENTITY - Updating (?<Attributes>.*)$",
                RegexOptions.Compiled);

        private static readonly Regex s_tagPattern =
            new Regex(
                @"^\s*tag=(?<tag>\S*) value=(?<value>.*)$",
                RegexOptions.Compiled);

        IEnumerable<string> IGameStateDebugEventParser.TryParseEvents(
            IEnumerator<string> lines,
            IEnumerable<IGameStateDebugEventParser> gameStateDebugEventParsers,
            out IEnumerable<object> parsedEvents)
        {
            parsedEvents = null;

            string currentLine = lines.Current;
            Match match = s_showEntityUpdatingPattern.Match(currentLine);
            if (!match.Success)
                return null;

            var linesConsumed = new List<string> { currentLine };
            lines.MoveNext();

            var events = new List<object>();

            IDictionary<string, string> showEntityUpdatingAttributes = currentLine.ParseKeyValuePairs();

            showEntityUpdatingAttributes.TryGetValue("CardID", out string cardID);

            if (showEntityUpdatingAttributes.TryGetValue("Entity", out string entity))
            {
                if (int.TryParse(entity, out int entityID))
                {
                    // identifying existing entity (already in hand due to mulligan, other object, etc.)
                }
                else
                {
                    IDictionary<string, string> entityAttributes =
                        entity
                        .Replace("[", string.Empty)
                        .Replace("]", string.Empty)
                        .ParseKeyValuePairs();

                    entityAttributes.TryGetValue("player", out string playerID);
                    entityAttributes.TryGetValue("zone", out string oldZone);

                    var tags = new Dictionary<string, string>();

                    Match tagMatch;
                    while ((currentLine = lines.Current) != null && (tagMatch = s_tagPattern.Match(currentLine)).Success)
                    {
                        tags.Add(tagMatch.Groups["tag"].Value, tagMatch.Groups["value"].Value);

                        linesConsumed.Add(currentLine);
                        lines.MoveNext();
                    }

                    if (!tags.TryGetValue("ZONE", out string newZone))
                        return linesConsumed;

                    if (string.Equals(oldZone, "DECK", StringComparison.Ordinal) && string.Equals(newZone, "HAND", StringComparison.Ordinal))
                        events.Add(
                            new Events.CardDrawnFromDeck(
                                playerID,
                                cardID));
                }
            }

            parsedEvents = events;

            return linesConsumed;
        }
    }
}
