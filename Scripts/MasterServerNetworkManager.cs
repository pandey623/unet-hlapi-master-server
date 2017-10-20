using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerNetworkManager : NetworkManagerSimple
{
    public MasterServerNetworkManager Singleton { get; protected set; }
    public string serverRegisterationKey = "";
    public string spawningBuildPath = "";
    public string spawningBuildPathForEditor = "";
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
        server.RegisterHandler(MasterServerMessages.RequestListOfHostsId, OnServerListHosts);
    }

    protected override void RegisterClientMessages(NetworkClient client)
    {
        base.RegisterClientMessages(client);
    }

    #region Server Handlers
    protected MasterServerRooms EnsureRoomsForGameType(string gameType)
    {
        if (gameTypeRooms.ContainsKey(gameType))
            return gameTypeRooms[gameType];

        MasterServerRooms newRooms = new MasterServerRooms();
        newRooms.gameType = gameType;
        gameTypeRooms[gameType] = newRooms;
        return newRooms;
    }

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
        var rooms = EnsureRoomsForGameType(msg.gameType);
        var roomId = System.Guid.NewGuid().ToString();
        var newRoom = new MasterServerRoom();

        newRoom.title = msg.title;
        newRoom.password = msg.password;
        newRoom.hostIp = netMsg.conn.address;
        newRoom.hostPort = msg.hostPort;
        newRoom.playerLimit = msg.playerLimit;
        newRoom.connectionId = netMsg.conn.connectionId;

        if (!rooms.AddRoom(roomId, newRoom))
        {
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationFailed;
            response.roomId = string.Empty;
        }
        else
        {
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationSucceeded;
            response.roomId = roomId;
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

    protected void OnServerListHosts(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerListHosts");
        var msg = netMsg.ReadMessage<MasterServerMessages.RequestHostListMessage>();
        var response = new MasterServerMessages.ListOfHostsMessage();
        response.hosts = new RegisteredMasterServerRoom[0];
        if (gameTypeRooms.ContainsKey(msg.gameType))
        {
            var rooms = gameTypeRooms[msg.gameType];
            response.hosts = rooms.GetRegisteredRooms();
        }
        netMsg.conn.Send(MasterServerMessages.ListOfHostsId, response);
    }
    #endregion
}
