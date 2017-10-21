using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerNetworkManager : NetworkManagerSimple
{
    public const string DefaultGameType = "Default";
    public MasterServerNetworkManager Singleton { get; protected set; }
    public string serverRegisterationKey = "";
    public string spawningBuildPath = "";
    public string spawningBuildPathForEditor = "";
    public System.Action<MasterServerMessages.RegisteredHostMessage> onRegisteredHost;
    public System.Action<MasterServerMessages.UnregisteredHostMessage> onUnregisteredHost;
    public System.Action<MasterServerMessages.ResponseListOfHostsMessage> onResponseListOfHosts;
    /// <summary>
    /// Registered room, use at client as reference to do something.
    /// </summary>
    protected RegisteredMasterServerRoom registeredRoom = RegisteredMasterServerRoom.Empty;
    protected readonly Dictionary<string, MasterServerRooms> gameTypeRooms = new Dictionary<string, MasterServerRooms>();

    protected virtual void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        Singleton = this;
    }

    protected override void RegisterServerMessages()
    {
        base.RegisterServerMessages();
        server.RegisterHandler(MasterServerMessages.RegisterHostId, OnServerRegisterHost);
        server.RegisterHandler(MasterServerMessages.UnregisterHostId, OnServerUnregisterHost);
        server.RegisterHandler(MasterServerMessages.RequestListOfHostsId, OnServerRequestListOfHosts);
    }

    protected override void RegisterClientMessages(NetworkClient client)
    {
        base.RegisterClientMessages(client);
        client.RegisterHandler(MasterServerMessages.RegisteredHostId, OnClientRegisteredHost);
        client.RegisterHandler(MasterServerMessages.UnregisteredHostId, OnClientUnregisteredHost);
        client.RegisterHandler(MasterServerMessages.ResponseListOfHostsId, OnClientResponseListOfHosts);
    }

    #region Server Helpers
    protected MasterServerRooms EnsureRoomsForGameType(string gameType)
    {
        if (gameTypeRooms.ContainsKey(gameType))
            return gameTypeRooms[gameType];

        MasterServerRooms newRooms = new MasterServerRooms();
        newRooms.gameType = gameType;
        gameTypeRooms[gameType] = newRooms;
        return newRooms;
    }
    #endregion

    #region Server Handlers
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);

        // remove the associated host
        foreach (var gameTypeRooms in gameTypeRooms.Values)
        {
            var rooms = gameTypeRooms.GetRooms();
            foreach (var room in rooms)
            {
                if (room.connectionId == conn.connectionId)
                {
                    // remove room
                    gameTypeRooms.RemoveRoom(room.roomId);

                    if (writeLog) Debug.Log("[" + name + "] Room [" + room.roomId + "] closed because host left");
                    break;
                }
            }
        }
    }

    protected void OnServerRegisterHost(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerRegisterHost");
        var msg = netMsg.ReadMessage<MasterServerMessages.RegisterHostMessage>();
        var response = new MasterServerMessages.RegisteredHostMessage();
        var gameType = string.IsNullOrEmpty(msg.gameType) ? DefaultGameType : msg.gameType;
        var rooms = EnsureRoomsForGameType(gameType);
        var roomId = System.Guid.NewGuid().ToString();
        var newRoom = new MasterServerRoom();

        newRoom.title = msg.title;
        newRoom.password = msg.password;
        newRoom.hostIp = netMsg.conn.address;
        newRoom.hostPort = msg.hostPort;
        newRoom.playerLimit = msg.playerLimit;
        newRoom.connectionId = netMsg.conn.connectionId;

        var registeredRoom = RegisteredMasterServerRoom.Empty;
        if (!rooms.AddRoom(roomId, newRoom, out registeredRoom))
        {
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationFailed;
            response.registeredRoom = registeredRoom;
        }
        else
        {
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationSucceeded;
            response.registeredRoom = registeredRoom;
        }

        netMsg.conn.Send(MasterServerMessages.RegisteredHostId, response);
    }

    protected void OnServerUnregisterHost(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerUnregisterHost");
        var msg = netMsg.ReadMessage<MasterServerMessages.UnregisterHostMessage>();
        var response = new MasterServerMessages.UnregisteredHostMessage();

        // find the room
        var rooms = EnsureRoomsForGameType(msg.gameType);
        var room = MasterServerRoom.Empty;

        if (!rooms.TryGetRoom(msg.roomId, out room))
        {
            if (writeLog) Debug.Log("[" + name + "] OnServerUnregisterHost game not found: " + msg.roomId);
            response.resultCode = (short)MasterServerMessages.ResultCodes.UnregistrationFailedNoRegisteredRoom;
        }
        else if (room.connectionId != netMsg.conn.connectionId)
        {
            if (writeLog) Debug.Log("[" + name + "] OnServerUnregisterHost connection mismatch: " + room.connectionId + " != " + netMsg.conn.connectionId);
            response.resultCode = (short)MasterServerMessages.ResultCodes.UnregistrationFailedNoRegisteredRoom;
        }
        else
        {
            rooms.RemoveRoom(msg.roomId);
            response.resultCode = (short)MasterServerMessages.ResultCodes.UnregistrationSucceeded;
        }

        netMsg.conn.Send(MasterServerMessages.UnregisteredHostId, response);
    }

    protected void OnServerRequestListOfHosts(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerListHosts");
        var msg = netMsg.ReadMessage<MasterServerMessages.RequestHostListMessage>();
        var response = new MasterServerMessages.ResponseListOfHostsMessage();
        response.hosts = new RegisteredMasterServerRoom[0];
        if (gameTypeRooms.ContainsKey(msg.gameType))
        {
            var rooms = gameTypeRooms[msg.gameType];
            response.hosts = rooms.GetRegisteredRooms();
        }
        netMsg.conn.Send(MasterServerMessages.ResponseListOfHostsId, response);
    }
    #endregion

    #region Client Functions
    public void RegisterHost(string gameType, string title, string password, int hostPort, int playerLimit)
    {
        if (!IsClientActive())
        {
            Debug.LogError("["+name+"] Cannot RegisterHost, client not connected.");
            return;
        }

        var msg = new MasterServerMessages.RegisterHostMessage();
        msg.gameType = gameType;
        msg.title = title;
        msg.password = password;
        msg.hostPort = hostPort;
        msg.playerLimit = playerLimit;
        client.Send(MasterServerMessages.RegisterHostId, msg);
    }

    public void RegisterHost(string gameType, string title, string password = "")
    {
        var gameNetworkManager = NetworkManager.singleton;
        RegisterHost(gameType, title, password, gameNetworkManager.networkPort, gameNetworkManager.maxConnections);
    }

    public void UnregisterHost()
    {
        if (registeredRoom.Equals(RegisteredMasterServerRoom.Empty))
        {
            Debug.LogError("[" + name + "] Cannot UnregisterHost, registered room is empty.");
            return;
        }
        var msg = new MasterServerMessages.UnregisterHostMessage();
        msg.roomId = registeredRoom.roomId;
        msg.gameType = registeredRoom.gameType;
        client.Send(MasterServerMessages.UnregisterHostId, msg);
    }
    #endregion

    #region Client Handlers
    protected void OnClientRegisteredHost(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.RegisteredHostMessage>();
        if (msg.resultCode == (short)MasterServerMessages.ResultCodes.RegistrationSucceeded)
            registeredRoom = msg.registeredRoom;
        if (onRegisteredHost != null)
            onRegisteredHost(msg);
    }

    protected void OnClientUnregisteredHost(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.UnregisteredHostMessage>();
        if (msg.resultCode == (short)MasterServerMessages.ResultCodes.UnregistrationSucceeded)
            registeredRoom = RegisteredMasterServerRoom.Empty;
        if (onUnregisteredHost != null)
            onUnregisteredHost(msg);
    }

    protected void OnClientResponseListOfHosts(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.ResponseListOfHostsMessage>();
        if (onResponseListOfHosts != null)
            onResponseListOfHosts(msg);
    }
    #endregion
}
