using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerNetworkManager : NetworkManagerSimple
{
    public const string DefaultGameType = "Default";
    public MasterServerNetworkManager Singleton { get; protected set; }
    public string registerKey = "";
    public bool autoRegisterToMasterServer;
    public bool startGameServerAsHost;
    public string gameServerGameType = DefaultGameType;
    public string gameServerTitle;
    public string gameServerPassword;
    public int gameServerNetworkPort;
    public int gameServerMaxConnections;
    public bool spawningAsBatch;
    public string spawningBuildPath = "";
    public string spawningBuildPathForEditor = "";
    public System.Action<MasterServerMessages.RegisteredGameServerMessage> onRegisteredHost;
    public System.Action<MasterServerMessages.UnregisteredGameServerMessage> onUnregisteredHost;
    public System.Action<MasterServerMessages.ResponseGameServerListMessage> onResponseGameServerList;
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

    protected virtual void Start()
    {
        var args = System.Environment.GetCommandLineArgs();
        var gameNetworkManager = NetworkManager.singleton;
        var startGameServer = false;
        gameServerGameType = DefaultGameType;
        gameServerTitle = "";
        gameServerPassword = "";
        gameServerNetworkPort = gameNetworkManager.networkPort;
        gameServerMaxConnections = gameNetworkManager.maxConnections;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "-startGameServer")
                startGameServer = true;
            if (arg == "-registerKey " && i + 1 < args.Length)
                registerKey = args[i + 1];
            if (arg == "-hostGameType" && i + 1 < args.Length)
                gameServerGameType = args[i + 1];
            if (arg == "-hostTitle" && i + 1 < args.Length)
                gameServerTitle = args[i + 1];
            if (arg == "-hostPassword" && i + 1 < args.Length)
                gameServerPassword = args[i + 1];
            if (arg == "-hostNetworkPort" && i + 1 < args.Length)
                gameServerNetworkPort = int.Parse(args[i + 1]);
            if (arg == "-hostMaxConnections" && i + 1 < args.Length)
                gameServerMaxConnections = int.Parse(args[i + 1]);
        }
        if (startGameServer)
        {
            autoRegisterToMasterServer = true;
            startGameServerAsHost = false;
            StartClient();
        }
    }

    protected override void RegisterServerMessages()
    {
        base.RegisterServerMessages();
        server.RegisterHandler(MasterServerMessages.RegisterGameServerId, OnServerRegisterGameServer);
        server.RegisterHandler(MasterServerMessages.UnregisterGameServerId, OnServerUnregisterGameServer);
        server.RegisterHandler(MasterServerMessages.RequestGameServerListId, OnServerRequestGameServerList);
    }

    protected override void RegisterClientMessages(NetworkClient client)
    {
        base.RegisterClientMessages(client);
        client.RegisterHandler(MasterServerMessages.RegisteredGameServerId, OnClientRegisteredGameServer);
        client.RegisterHandler(MasterServerMessages.UnregisteredGameServerId, OnClientUnregisteredGameServer);
        client.RegisterHandler(MasterServerMessages.ResponseGameServerListId, OnClientResponseGameServerList);
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

    protected void OnServerRegisterGameServer(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerRegisterHost");
        var msg = netMsg.ReadMessage<MasterServerMessages.RegisterGameServerMessage>();
        var response = new MasterServerMessages.RegisteredGameServerMessage();
        var gameType = string.IsNullOrEmpty(msg.gameType) ? DefaultGameType : msg.gameType;
        var rooms = EnsureRoomsForGameType(gameType);
        var roomId = System.Guid.NewGuid().ToString();
        var newRoom = new MasterServerRoom();

        newRoom.title = msg.title;
        newRoom.password = msg.password;
        newRoom.hostIp = netMsg.conn.address;
        newRoom.networkPort = msg.networkPort;
        newRoom.maxConnections = msg.maxConnections;
        newRoom.connectionId = netMsg.conn.connectionId;

        var registeredRoom = RegisteredMasterServerRoom.Empty;
        if (!msg.registerKey.Equals(registerKey))
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationFailedInvalidKey;
        else if (!rooms.AddRoom(roomId, newRoom, out registeredRoom))
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationFailedCannotCreateRoom;
        else
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationSucceeded;

        response.registeredRoom = registeredRoom;

        netMsg.conn.Send(MasterServerMessages.RegisteredGameServerId, response);
    }

    protected void OnServerUnregisterGameServer(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerUnregisterHost");
        var msg = netMsg.ReadMessage<MasterServerMessages.UnregisterGameServerMessage>();
        var response = new MasterServerMessages.UnregisteredGameServerMessage();

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

        netMsg.conn.Send(MasterServerMessages.UnregisteredGameServerId, response);
    }

    protected void OnServerRequestGameServerList(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerListHosts");
        var msg = netMsg.ReadMessage<MasterServerMessages.RequestGameServerListMessage>();
        var response = new MasterServerMessages.ResponseGameServerListMessage();
        response.hosts = new RegisteredMasterServerRoom[0];
        if (gameTypeRooms.ContainsKey(msg.gameType))
        {
            var rooms = gameTypeRooms[msg.gameType];
            response.hosts = rooms.GetRegisteredRooms();
        }
        netMsg.conn.Send(MasterServerMessages.ResponseGameServerListId, response);
    }
    #endregion

    #region Client Functions
    public void RegisterHost(string gameType, string title, string password, int networkPort, int maxConnections)
    {
        if (!IsClientActive())
        {
            Debug.LogError("["+name+"] Cannot RegisterHost, client not connected.");
            return;
        }

        var gameNetworkManager = NetworkManager.singleton;
        gameNetworkManager.networkPort = networkPort;
        gameNetworkManager.maxConnections = maxConnections;

        var msg = new MasterServerMessages.RegisterGameServerMessage();
        msg.registerKey = registerKey;
        msg.gameType = gameType;
        msg.title = title;
        msg.password = password;
        msg.networkPort = networkPort;
        msg.maxConnections = maxConnections;
        client.Send(MasterServerMessages.RegisterGameServerId, msg);
    }

    public void RegisterHost(string gameType, string title, string password = "")
    {
        var gameNetworkManager = NetworkManager.singleton;
        RegisterHost(gameType, title, password, gameNetworkManager.networkPort, gameNetworkManager.maxConnections);
    }

    public void RegisterHost()
    {
        RegisterHost(gameServerGameType, gameServerTitle, gameServerPassword, gameServerNetworkPort, gameServerMaxConnections);
    }

    public void UnregisterHost()
    {
        if (registeredRoom.Equals(RegisteredMasterServerRoom.Empty))
        {
            Debug.LogError("[" + name + "] Cannot UnregisterHost, registered room is empty.");
            return;
        }
        var msg = new MasterServerMessages.UnregisterGameServerMessage();
        msg.roomId = registeredRoom.roomId;
        msg.gameType = registeredRoom.gameType;
        client.Send(MasterServerMessages.UnregisterGameServerId, msg);
    }
    #endregion

    #region Client Handlers
    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);
        if (autoRegisterToMasterServer)
            RegisterHost();
    }

    protected void OnClientRegisteredGameServer(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.RegisteredGameServerMessage>();
        if (msg.resultCode == (short)MasterServerMessages.ResultCodes.RegistrationSucceeded)
        {
            registeredRoom = msg.registeredRoom;
            var gameNetworkManager = NetworkManager.singleton;
            if (startGameServerAsHost)
                gameNetworkManager.StartHost();
            else
                gameNetworkManager.StartServer();
        }
        if (onRegisteredHost != null)
            onRegisteredHost(msg);
    }

    protected void OnClientUnregisteredGameServer(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.UnregisteredGameServerMessage>();
        if (msg.resultCode == (short)MasterServerMessages.ResultCodes.UnregistrationSucceeded)
            registeredRoom = RegisteredMasterServerRoom.Empty;
        if (onUnregisteredHost != null)
            onUnregisteredHost(msg);
    }

    protected void OnClientResponseGameServerList(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.ResponseGameServerListMessage>();
        if (onResponseGameServerList != null)
            onResponseGameServerList(msg);
    }
    #endregion
}
