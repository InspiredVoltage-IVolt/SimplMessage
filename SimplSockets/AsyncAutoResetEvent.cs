using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimplSockets
{
    public class AsyncAutoResetEvent
    {
        private readonly static Task s_completed = Task.FromResult(true);
        private readonly Queue<TaskCompletionSource<bool>> m_waits = new Queue<TaskCompletionSource<bool>>();
        private bool m_signaled;

        public Task WaitAsync()
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

        //public Task WaitAsync(int timeout)
        //{
        //    lock (m_waits)
        //    {
        //        if (m_signaled)
        //        {
        //            m_signaled = false;
        //            return s_completed;
        //        }
        //        else
        //        {
        //            var tcs = new TaskCompletionSource<bool>();
        //            var ct  = new CancellationTokenSource(timeout);
        //            ct.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
        //            m_waits.Enqueue(tcs);
        //            return tcs.Task;
        //        }
        //    }
        //}

        public Task WaitAsync(int timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);
            return WaitAsync(cancellationTokenSource.Token);
        }

        public void Wait(int timeout)
        {
            WaitAsync(timeout).RunSynchronously();
            return;
        }

        public Task WaitAsync(CancellationToken cancellationToken)
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

        public void Wait(CancellationToken cancellationToken)
        {
            WaitAsync(cancellationToken).RunSynchronously();
            return;
        }
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
    }
}
