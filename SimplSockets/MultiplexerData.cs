﻿using System.Threading;

namespace SimplSockets
{
    /// <summary>
    /// Contains multiplexer data.
    /// </summary>
    internal class MultiplexerData
    {
        public byte[] Message { get; set; }
        public ManualResetEventSlim ManualResetEventSlim { get; set; }
    }
}
