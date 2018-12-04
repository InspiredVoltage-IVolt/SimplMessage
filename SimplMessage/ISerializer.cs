using System;

namespace SimplMessage
{
    public interface ISerializer
    {
        byte[] Serialize  <T>(T message);
        T      Deserialize<T>(byte[] messageBytes);
        //object Deserialize   (byte[] messageBytes, out Type type);
    }
}