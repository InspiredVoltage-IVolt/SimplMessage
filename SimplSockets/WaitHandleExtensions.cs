using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimplSockets
{
    public static class WaitHandleExtensions
    {
        // <summary>
        //  Awaits until the current WaitHandle receives a signal.
        // </summary>
        public static async Task<bool> WaitOneAsync(this WaitHandle handle, int millisecondsTimeout,
            CancellationToken cancellationToken)
        {
            RegisteredWaitHandle          registeredHandle  = null;
            CancellationTokenRegistration tokenRegistration = default(CancellationTokenRegistration);
            try
            {
                var tcs = new TaskCompletionSource<bool>();
                registeredHandle = ThreadPool.RegisterWaitForSingleObject(
                    handle,
                    (state, timedOut) => ((TaskCompletionSource<bool>) state).TrySetResult(!timedOut),
                    tcs,
                    millisecondsTimeout,
                    true);
                tokenRegistration = cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>) state).TrySetCanceled(),
                    tcs);
                return await tcs.Task;
            }
            finally
            {
                if (registeredHandle != null)
                    registeredHandle.Unregister(null);
                tokenRegistration.Dispose();
            }
        }

        // <summary>
        //  Awaits until the current WaitHandle receives a signal.
        // </summary>
        public static Task<bool> WaitOneAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return handle.WaitOneAsync((int)timeout.TotalMilliseconds, cancellationToken);
        }

        // <summary>
        //  Awaits until the current WaitHandle receives a signal.
        // </summary>
        public static Task<bool> WaitOneAsync(this WaitHandle handle, CancellationToken cancellationToken)
        {
            return handle.WaitOneAsync(Timeout.Infinite, cancellationToken);
        }

        // <summary>
        //  Awaits until the current WaitHandle receives a signal.
        // </summary>
        public static Task<bool> WaitOneAsync(this WaitHandle handle, TimeSpan timeout)
        {
            return handle.WaitOneAsync((int)timeout.TotalMilliseconds, new CancellationToken());
        }

        // <summary>
        //  Awaits until the current WaitHandle receives a signal.
        // </summary>
        public static Task<bool> WaitOneAsync(this WaitHandle handle)
        {
            return handle.WaitOneAsync(Timeout.Infinite, new CancellationToken());
        }
    }
}