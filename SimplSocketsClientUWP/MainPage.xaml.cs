using Windows.UI.Xaml.Controls;
using SimplSocketsClient;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SimplSocketsClientUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            UIDispatcher.Initialize();

            //var socketClient = new SocketClient(this);
            //socketClient.Start();

            var messageClient = new MessageClient(this);
            messageClient.Start();
        }

        public void Log(string output)
        {
            OutputListbox?.Items?.Add(output);
        }
    }
}
