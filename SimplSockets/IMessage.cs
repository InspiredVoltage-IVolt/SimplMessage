using System;

namespace SimplSockets
{
    public interface IMessage : IDisposable
    {
        int Length { get; }
        void Sent();
        byte[] Content { get; set; }
    }
}