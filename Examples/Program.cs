using System;

namespace SimplSocketsClient
{
    /// <summary>
    /// This show-cases most of the functionality of both the SimplSockets and SimplMessage library
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Choose an example:");
            Console.WriteLine();
            Console.WriteLine(" 1  - Client sending raw bytes"             );
            Console.WriteLine(" 2  - Server sending raw bytes"             );
            Console.WriteLine(" 3  - Client waiting for reply of raw bytes");
            Console.WriteLine(" 4  - Client sending object"                );
            Console.WriteLine(" 5  - Server sending object"                );
            Console.WriteLine(" 6  - Client waiting for reply of object"   );
            Console.WriteLine(" 7  - Client & Server connecting"           );
            Console.WriteLine(" 8  - Unity3D integration"                  );
            Console.WriteLine(" 9  - Asynchronous functions"               );
            var input  = Console.ReadLine();
            var choice = int.Parse(input.ToString());

            switch (choice)
            {
                case 1: { var example = new ClientSendBytes()          ; example.Start();  break; }
                case 2: { var example = new ServerSendBytes()          ; example.Start();  break; }
                case 3: { var example = new ClientWaitForReplyBytes()  ; example.Start();  break; }
                case 4: { var example = new ClientSendMessage()        ; example.Start();  break; }
                case 5: { var example = new ServerSendMessage()        ; example.Start();  break; }
                case 6: { var example = new ClientWaitForReplyMessage(); example.Start();  break; }
                case 7: { var example = new ConnectionClientServer()   ; example.Start();  break; }
                case 8: { var example = new Unity3D()                  ; example.Start();  break; }
                case 9: { var example = new AsyncMessage()             ; example.Start();  break; }
            }

            Console.WriteLine("Done! Press any key to exit");
            Console.ReadLine();
        }
    }
}
