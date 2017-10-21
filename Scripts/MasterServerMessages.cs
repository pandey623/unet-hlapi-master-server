using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerMessages
{
    public enum ResultCodes : short
    {
        RegistrationFailed,                     // Registration failed because an empty game name was given.
        RegistrationFailedNoServer,             // Registration failed because no server is running.
        RegistrationSucceeded,                  // Registration to master server succeeded, received confirmation.
        UnregistrationFailedNoRegisteredRoom,   // Unregistration to master server failed, no registered room.
        UnregistrationFailedConnectionMismatch, // Unregistration to master server failed, no registered room.
        UnregistrationSucceeded,                // Unregistration to master server succeeded, received confirmation.
    }

    // -------------- client to masterserver Ids --------------
    public const short RegisterHostId = MsgType.Highest + 1;
    public const short UnregisterHostId = MsgType.Highest + 2;
    public const short RequestListOfHostsId = MsgType.Highest + 3;

    // -------------- masterserver to client Ids --------------
    public const short RegisteredHostId = MsgType.Highest + 4;
    public const short UnregisteredHostId = MsgType.Highest + 5;
    public const short ResponseListOfHostsId = MsgType.Highest + 6;


    // -------------- client to server messages --------------
    public class RegisterHostMessage : MessageBase
    {
        public string gameType;
        public string title;
        public string password;
        public int hostPort;
        public int playerLimit;
    }

    public class UnregisterHostMessage : MessageBase
    {
        public string roomId;
        public string gameType;
    }

    public class RequestHostListMessage : MessageBase
    {
        public string gameType;
    }

    // -------------- server to client messages --------------
    public class RegisteredHostMessage : MessageBase
    {
        public short resultCode;
        public RegisteredMasterServerRoom registeredRoom;
    }

    public class UnregisteredHostMessage : MessageBase
    {
        public short resultCode;
    }

    public class ListOfHostsMessage : MessageBase
    {
        public RegisteredMasterServerRoom[] hosts;
    }
}
