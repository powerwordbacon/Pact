﻿using System;
using System.Threading.Tasks;

namespace Pact
{
    public interface IBackgroundWorkInterface
    {
        Task Perform(
            Func<Action<string>, Task> backgroundWorker,
            int fadeDuration = 0);
    }
}
