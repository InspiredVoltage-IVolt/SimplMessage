using System;
using System.Text;
using SimplSockets;

namespace SimplMessage
{
    internal sealed class ProtocolHelper
    {
        /// <summary>
        /// The control bytes placeholder - the first 4 bytes are little endian message length, the last 4 are thread id
        /// </summary>
        

        public static byte[] AppendMessageToIdentifier(string identifier, byte[] message)
        {
            // Encode string
            var identifierUtf8   = Encoding.UTF8.GetBytes(identifier);
            var identifierLength = (byte)identifierUtf8.Length;

            // Create room for the control bytes
            var messageWithIdentifier = new byte[1+identifierUtf8.Length+ message.Length];
            messageWithIdentifier[0]  = identifierLength;
            Buffer.BlockCopy(identifierUtf8,0, messageWithIdentifier,1, identifierLength);
            Buffer.BlockCopy(message, 0, messageWithIdentifier, 1+ identifierLength, message.Length);
            
            return messageWithIdentifier;
        }

        
        public static void ExtractIdentifier(byte[] messageWithIdentifier, out string identifier, out byte[] message)
        {
            var identifierLength = messageWithIdentifier[0];
            var identifierUtf8   = new byte[identifierLength];            
            message              = new byte[messageWithIdentifier.Length - 1 - identifierLength];
            Buffer.BlockCopy(messageWithIdentifier, 1, identifierUtf8, 0, identifierLength);           
            Buffer.BlockCopy(messageWithIdentifier, 1 + identifierLength, message, 0, message.Length);
            identifier           = Encoding.UTF8.GetString(identifierUtf8);
        }
    }
}
