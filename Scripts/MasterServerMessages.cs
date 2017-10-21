using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerMessages
{
    public enum ResultCodes : short
    {
        RegistrationFailedInvalidKey,                   // Registration failed because register key is invalid.
        RegistrationFailedCannotCreateRoom,             // Registration failed because an empty game name was given.
        RegistrationSucceeded,                          // Registration to master server succeeded, received confirmation.
        UnregistrationFailedNoRegisteredRoom,           // Unregistration to master server failed, no registered room.
        UnregistrationFailedConnectionMismatch,         // Unregistration to master server failed, connection mismatch.
        UnregistrationSucceeded,                        // Unregistration to master server succeeded, received confirmation.
        SpawnFailedInvalidKey,                          // Spawn failed because register key is invalid.
        SpawnSucceeded,                                 // Spawn succeeded, received confirmation.
        RequestConnectionInfoFailedNoRegisteredRoom,    // Request connection info failed, no registered room
        RequestConnectionInfoFailedInvalidPassword,     // Request connection info failed, invalid password
        RequestConnectionInfoSucceed,                   // Request connection info succeeded, received confirmation.
    }

    // -------------- gameserver to masterserver Ids --------------
    public const short RegisterGameServerId = MsgType.Highest + 1;
    public const short UnregisterGameServerId = MsgType.Highest + 2;

    // -------------- masterserver to gameserver Ids --------------
    public const short RegisteredGameServerId = MsgType.Highest + 3;
    public const short UnregisteredGameServerId = MsgType.Highest + 4;

    // -------------- client to masterserver Ids --------------
    public const short RequestGameServerListId = MsgType.Highest + 5;
    public const short SpawnGameServerId = MsgType.Highest + 6;
    public const short RequestConnectionInfoId = MsgType.Highest + 7;

    // -------------- masterserver to client Ids --------------
    public const short ResponseGameServerListId = MsgType.Highest + 8;
    public const short SpawnedGameServerId = MsgType.Highest + 9;
    public const short ResponseConnectionInfoId = MsgType.Highest + 10;

    // -------------- gameserver to masterserver messages --------------
    public class RegisterGameServerMessage : MessageBase
    {
        public string spawnToken;
        public string registerKey;
        public string gameType;
        public string title;
        public string password;
        public string scene;
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

    // -------------- masterserver to gameserver messages --------------
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

    // -------------- client to masterserver messages --------------
    public class SpawnGameServerMessage : MessageBase
    {
        public string registerKey;
        public string gameType;
        public string title;
        public string password;
        public string scene;
        public int networkPort;
        public int maxConnections;
    }

    public class RequestConnectionInfoMessage : MessageBase
    {
        public string roomId;
        public string gameType;
        public string password;
    }

    // -------------- masterserver to client messages --------------
    public class SpawnedGameServerMessage : MessageBase
    {
        public short resultCode;
        public MasterServerRoom room;
    }

    public class ResponseConnectionInfoMessage : MessageBase
    {
        public short resultCode;
        public string networkAddress;
        public int networkPort;
    }
}
