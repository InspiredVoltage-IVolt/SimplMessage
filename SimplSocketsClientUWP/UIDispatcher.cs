using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace SimplSocketsClientUWP
{
    public static class UIDispatcher
    {
        private static CoreDispatcher _dispatcher;

        public static void Initialize()
        {
            _dispatcher = Window.Current.Dispatcher;
        }

        public static void BeginExecute(Action action)
        {
            if (_dispatcher.HasThreadAccess)
                action();

            else
            {
                var task = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
            }
        }

        public static void Execute(Action action)
        {
            InnerExecute(action).Wait();
        }

        private static async Task InnerExecute(Action action)
        {
            if (_dispatcher.HasThreadAccess)
                action();

            else await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }
    }
}