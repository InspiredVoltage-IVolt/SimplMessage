using System;
using MsgPack.Serialization;

namespace SimplMessage
{
    public class MsgPackCliSerializer
    {
        public byte[] Serialize<T>(string identifier,T message)
        {
            try
            {
                var serializer = MessagePackSerializer.Get<T>();
                return ProtocolHelper.AppendMessageToIdentifier(identifier, serializer.PackSingleObject(message));
            }
            catch (Exception) { return null; }
        }

        public T Deserialize<T>(byte[] message)
        {
            try
            {
                ProtocolHelper.ExtractIdentifier(message, out string identifier, out byte[] messageContent);
                var serializer = MessagePackSerializer.Get<T>();
                return serializer.UnpackSingleObject(messageContent);
            }
            catch (Exception) { return default(T); }
        }

        public T Deserialize<T>(byte[] message, out string identifier)
        {
            try
            {
                ProtocolHelper.ExtractIdentifier(message, out identifier, out byte[] messageContent);
                var serializer = MessagePackSerializer.Get<T>();
                return serializer.UnpackSingleObject(messageContent);
            }
            catch (Exception) { identifier = null;  return default(T); }
        }

        public static T DeserializeMessage<T>(byte[] message)
        {
            try
            {
                var serializer = MessagePackSerializer.Get<T>();
                return serializer.UnpackSingleObject(message);
            }
            catch (Exception) {  return default(T); }
        }
    }
}