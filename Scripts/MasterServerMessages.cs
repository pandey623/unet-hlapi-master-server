using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerMessages
{
    public enum ResultCodes : short
    {
        RegistrationFailedInvalidKey,           // Registration failed because register key is invalid.
        RegistrationFailedCannotCreateRoom,     // Registration failed because an empty game name was given.
        RegistrationSucceeded,                  // Registration to master server succeeded, received confirmation.
        UnregistrationFailedNoRegisteredRoom,   // Unregistration to master server failed, no registered room.
        UnregistrationFailedConnectionMismatch, // Unregistration to master server failed, connection mismatch.
        UnregistrationSucceeded,                // Unregistration to master server succeeded, received confirmation.
    }

    // -------------- client to masterserver Ids --------------
    public const short RegisterGameServerId = MsgType.Highest + 1;
    public const short UnregisterGameServerId = MsgType.Highest + 2;
    public const short RequestGameServerListId = MsgType.Highest + 3;

    // -------------- masterserver to client Ids --------------
    public const short RegisteredGameServerId = MsgType.Highest + 4;
    public const short UnregisteredGameServerId = MsgType.Highest + 5;
    public const short ResponseGameServerListId = MsgType.Highest + 6;


    // -------------- client to server messages --------------
    public class RegisterGameServerMessage : MessageBase
    {
        public string registerKey;
        public string gameType;
        public string title;
        public string password;
        public int networkPort;
        public int maxConnections;
    }

    public class UnregisterGameServerMessage : MessageBase
    {
        public string roomId;
        public string gameType;
    }

    public class RequestGameServerListMessage : MessageBase
    {
        public string gameType;
    }

    // -------------- server to client messages --------------
    public class RegisteredGameServerMessage : MessageBase
    {
        public short resultCode;
        public RegisteredMasterServerRoom registeredRoom;
    }

    public class UnregisteredGameServerMessage : MessageBase
    {
        public short resultCode;
    }

    public class ResponseGameServerListMessage : MessageBase
    {
        public RegisteredMasterServerRoom[] hosts;
    }
}
