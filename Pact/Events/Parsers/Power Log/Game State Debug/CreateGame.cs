﻿//using System.Collections.Generic;
//using System.Text.RegularExpressions;

//namespace Pact.EventParsers.PowerLog.GameStateDebug
//{
//    public sealed class CreateGame
//        : IGameStateDebugEventParser
//    {
//        private static readonly Regex s_startPattern =
//            new Regex(
//                @"^.*GameState.DebugPrintPower\(\) - CREATE_GAME.*$",
//                RegexOptions.Compiled);

//        bool IGameStateDebugEventParser.TryParseEvents(
//            IEnumerator<string> lines,
//            out IEnumerable<object> parsedEvents,
//            out string unusedText)
//        {
//            parsedEvents = null;
//            unusedText = string.Empty;

//            if (!s_startPattern.IsMatch(lines.Current))
//                return false;

//            lines.MoveNext();

//            parsedEvents = new List<object>();
//            unusedText = null;

//            return true;
//        }
//    }
//}
