﻿namespace Pact.Commands
{
    public sealed class MoveDeck
    {
        public MoveDeck(
            int sourcePosition,
            int targetPosition)
        {
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
        }

        public int SourcePosition { get; }

        public int TargetPosition { get; }
    }
}
