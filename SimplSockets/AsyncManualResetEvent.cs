
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimplSockets
{
    public class AsyncManualResetEvent
    {
        private readonly static Task<bool> s_completed = Task.FromResult(true);
        private readonly Queue<TaskCompletionSource<bool>> m_waits = new Queue<TaskCompletionSource<bool>>();
        private bool m_signaled;

        public Task WaitOneAsync()
        {
            lock (m_waits)
            {
                if (m_signaled)
                {
                    m_signaled = false;
                    return s_completed;
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    m_waits.Enqueue(tcs);
                    return tcs.Task;
                }
            }
        }

        //public void WaitOne()
        //{
        //    WaitOneAsync().RunSynchronously();
        //    return;
        //}

        public Task WaitOneAsync(int timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);
            return WaitOneAsync(cancellationTokenSource.Token);
        }

        //public bool WaitOne(int timeout)
        //{
        //    WaitOneAsync(timeout).RunSynchronously();
        //    // todo: get whether the wait as been cancelled through a timeout
        //    throw new NotImplementedException();
        //    return true;
        //}

        public Task<bool> WaitOneAsync(CancellationToken cancellationToken)
        {
            lock (m_waits)
            {
                if (m_signaled)
                {
                    m_signaled = false;
                    return s_completed;
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
                    m_waits.Enqueue(tcs);                    
                    return tcs.Task;
                }
            }
        }

        //public bool WaitOne(CancellationToken cancellationToken)
        //{
        //    var result = WaitOneAsync(cancellationToken).RunSynchronously();
        //    // todo: get whether the wait as been cancelled 
        //    throw new NotImplementedException();
        //    return true;
        //}

        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;
            lock (m_waits)
            {
                if (m_waits.Count > 0)
                    toRelease = m_waits.Dequeue();
                else if (!m_signaled)
                    m_signaled = true;
            }
            if (toRelease != null)
                toRelease.SetResult(true);
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}
