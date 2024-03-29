﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pact.Extensions.String
{
    public static class StringExtensions
    {
        private static readonly Regex s_tokenPattern =
            new Regex(
                @"((?<Key>\w+)=(?<Value>(?:\[(?>\[(?<DEPTH>)|\](?<-DEPTH>)|[^\[\]]*)*(?(DEPTH)(?!))\]|\S*(?:\s+[^=]+\s)*)))",
                RegexOptions.Compiled);

        public static bool Eq(
            this string left,
            string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public static IDictionary<string, string> ParseKeyValuePairs(
            this string line)
        {
            if (line == null)
                return new Dictionary<string, string>();

            return
                __EnumerateGroups(s_tokenPattern.Matches(line))
                .ToDictionary(__kvp => __kvp.Key, __kvp => __kvp.Value);

            IEnumerable<(string Key, string Value)> __EnumerateGroups(
                MatchCollection matchCollection)
            {
                foreach (Match match in matchCollection)
                    yield return (Key: match.Groups["Key"].Value, match.Groups["Value"].Value);
            }
        }

        public static string Rem(
            this string text,
            params string[] tokens)
        {
            if (text == null)
                return null;

            foreach (string token in tokens)
                text = text.Replace(token, string.Empty);

            return text;
        }
    }
}
