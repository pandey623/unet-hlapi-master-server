using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerNetworkManager : NetworkManagerSimple
{
    public const string DefaultGameType = "Default";
    public MasterServerNetworkManager Singleton { get; protected set; }
    public string registerKey = "";
    public bool registerServerOnConnect;
    public bool startGameServerAsHost;
    public string gameServerGameType = DefaultGameType;
    public string gameServerTitle;
    public string gameServerPassword;
    public string gameServerScene;
    public int gameServerNetworkPort;
    public int gameServerMaxConnections;
    public bool spawningAsBatch;
    public string spawningBuildPath = "";
    public string spawningBuildPathForEditor = "";
    public System.Action<MasterServerMessages.RegisteredGameServerMessage> onRegisteredHost;
    public System.Action<MasterServerMessages.UnregisteredGameServerMessage> onUnregisteredHost;
    public System.Action<MasterServerMessages.ResponseGameServerListMessage> onResponseGameServerList;
    public System.Action<MasterServerMessages.SpawnedGameServerMessage> onSpawnedGameServer;
    public System.Action<MasterServerMessages.ResponseConnectionInfoMessage> onResponseConnectionInfo;
    protected string spawnToken;
    /// <summary>
    /// Registered room, use at client as reference to do something.
    /// </summary>
    protected RegisteredMasterServerRoom registeredRoom = RegisteredMasterServerRoom.Empty;
    protected readonly Dictionary<string, MasterServerRooms> gameTypeRooms = new Dictionary<string, MasterServerRooms>();
    protected readonly Dictionary<string, NetworkConnection> spawners = new Dictionary<string, NetworkConnection>();

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
        spawnToken = "";
        gameServerGameType = DefaultGameType;
        gameServerTitle = "";
        gameServerPassword = "";
        gameServerScene = gameNetworkManager.onlineScene;
        gameServerNetworkPort = gameNetworkManager.networkPort;
        gameServerMaxConnections = gameNetworkManager.maxConnections;
        gameServerScene = gameNetworkManager.onlineScene;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "-startGameServer")
                startGameServer = true;
            if (arg == "-registerKey " && i + 1 < args.Length)
                registerKey = args[i + 1];
            if (arg == "-spawnToken " && i + 1 < args.Length)
                spawnToken = args[i + 1];
            if (arg == "-gameServerGameType" && i + 1 < args.Length)
                gameServerGameType = args[i + 1];
            if (arg == "-gameServerTitle" && i + 1 < args.Length)
                gameServerTitle = args[i + 1];
            if (arg == "-gameServerPassword" && i + 1 < args.Length)
                gameServerPassword = args[i + 1];
            if (arg == "-gameServerScene" && i + 1 < args.Length)
                gameServerScene = args[i + 1];
            if (arg == "-gameServerNetworkPort" && i + 1 < args.Length)
                gameServerNetworkPort = int.Parse(args[i + 1]);
            if (arg == "-gameServerMaxConnections" && i + 1 < args.Length)
                gameServerMaxConnections = int.Parse(args[i + 1]);
        }
        if (startGameServer)
        {
            registerServerOnConnect = true;
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
        server.RegisterHandler(MasterServerMessages.SpawnGameServerId, OnServerSpawnGameServer);
        server.RegisterHandler(MasterServerMessages.RequestConnectionInfoId, OnServerRequestConnectionInfo);
    }

    protected override void RegisterClientMessages(NetworkClient client)
    {
        base.RegisterClientMessages(client);
        client.RegisterHandler(MasterServerMessages.RegisteredGameServerId, OnClientRegisteredGameServer);
        client.RegisterHandler(MasterServerMessages.UnregisteredGameServerId, OnClientUnregisteredGameServer);
        client.RegisterHandler(MasterServerMessages.ResponseGameServerListId, OnClientResponseGameServerList);
        server.RegisterHandler(MasterServerMessages.SpawnedGameServerId, OnClientSpawnedGameServer);
        server.RegisterHandler(MasterServerMessages.ResponseConnectionInfoId, OnClientResponseConnectionInfo);
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
        if (writeLog) Debug.Log("[" + name + "] OnServerRegisterGameServer");
        var msg = netMsg.ReadMessage<MasterServerMessages.RegisterGameServerMessage>();
        var response = new MasterServerMessages.RegisteredGameServerMessage();
        var gameType = string.IsNullOrEmpty(msg.gameType) ? DefaultGameType : msg.gameType;
        var rooms = EnsureRoomsForGameType(gameType);
        var roomId = System.Guid.NewGuid().ToString();
        var newRoom = new MasterServerRoom();

        newRoom.roomId = roomId;
        newRoom.gameType = gameType;
        newRoom.title = msg.title;
        newRoom.password = msg.password;
        newRoom.scene = msg.scene;
        newRoom.networkAddress = netMsg.conn.address;
        newRoom.networkPort = msg.networkPort;
        newRoom.maxConnections = msg.maxConnections;
        newRoom.connectionId = netMsg.conn.connectionId;

        var registeredRoom = RegisteredMasterServerRoom.Empty;
        if (!msg.registerKey.Equals(registerKey))
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationFailedInvalidKey;
        else if (!rooms.AddRoom(roomId, newRoom, out registeredRoom))
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationFailedCannotCreateRoom;
        else
        {
            response.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationSucceeded;

            NetworkConnection spawnerConnection;
            if (!string.IsNullOrEmpty(msg.spawnToken) && spawners.TryGetValue(msg.spawnToken, out spawnerConnection))
            {
                var spawnResponse = new MasterServerMessages.SpawnedGameServerMessage();
                spawnResponse.resultCode = (short)MasterServerMessages.ResultCodes.SpawnSucceeded;
                spawnResponse.room = newRoom;
                spawnerConnection.Send(MasterServerMessages.SpawnedGameServerId, spawnResponse);
            }
        }

        response.registeredRoom = registeredRoom;

        netMsg.conn.Send(MasterServerMessages.RegisteredGameServerId, response);
    }

    protected void OnServerUnregisterGameServer(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerUnregisterGameServer");
        var msg = netMsg.ReadMessage<MasterServerMessages.UnregisterGameServerMessage>();
        var response = new MasterServerMessages.UnregisteredGameServerMessage();

        // find the room
        var rooms = EnsureRoomsForGameType(msg.gameType);
        var room = MasterServerRoom.Empty;

        if (!rooms.TryGetRoom(msg.roomId, out room))
        {
            if (writeLog) Debug.Log("[" + name + "] OnServerUnregisterGameServer room not found: " + msg.roomId);
            response.resultCode = (short)MasterServerMessages.ResultCodes.UnregistrationFailedNoRegisteredRoom;
        }
        else if (room.connectionId != netMsg.conn.connectionId)
        {
            if (writeLog) Debug.Log("[" + name + "] OnServerUnregisterGameServer connection mismatch: " + room.connectionId + " != " + netMsg.conn.connectionId);
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
        if (writeLog) Debug.Log("[" + name + "] OnServerRequestGameServerList");
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

    protected void OnServerSpawnGameServer(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerSpawnGameServer");
        var msg = netMsg.ReadMessage<MasterServerMessages.SpawnGameServerMessage>();

        if (!msg.registerKey.Equals(registerKey))
        {
            var spawnResponse = new MasterServerMessages.SpawnedGameServerMessage();
            spawnResponse.resultCode = (short)MasterServerMessages.ResultCodes.RegistrationFailedInvalidKey;
            spawnResponse.room = MasterServerRoom.Empty;
            netMsg.conn.Send(MasterServerMessages.SpawnedGameServerId, spawnResponse);
            return;
        }
        
        var spawnToken = System.Guid.NewGuid().ToString();
        var arguments = "";
        arguments += " -startGameServer";
        arguments += " -registerKey " + registerKey;
        arguments += " -spawnToken " + spawnToken;
        arguments += " -gameServerGameType " + msg.gameType;
        arguments += " -gameServerTitle " + msg.title;
        arguments += " -gameServerPassword " + msg.title;
        arguments += " -gameServerScene " + msg.scene;
        arguments += " -gameServerNetworkPort " + msg.networkPort;
        arguments += " -gameServerMaxConnections " + msg.maxConnections;

        var spawnPath = Application.isEditor ? spawningBuildPathForEditor : spawningBuildPath;
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = spawnPath;
        process.StartInfo.Arguments = arguments;
        process.Start();
    }

    protected void OnServerRequestConnectionInfo(NetworkMessage netMsg)
    {
        if (writeLog) Debug.Log("[" + name + "] OnServerRequestConnectionInfo");
        var msg = netMsg.ReadMessage<MasterServerMessages.RequestConnectionInfoMessage>();
        var response = new MasterServerMessages.ResponseConnectionInfoMessage();

        // find the room
        var rooms = EnsureRoomsForGameType(msg.gameType);
        var room = MasterServerRoom.Empty;

        if (!rooms.TryGetRoom(msg.roomId, out room))
        {
            if (writeLog) Debug.Log("[" + name + "] OnServerRequestConnectionInfo room not found: " + msg.roomId);
            response.resultCode = (short)MasterServerMessages.ResultCodes.RequestConnectionInfoFailedNoRegisteredRoom;
        }
        else if (!room.password.Equals(msg.password))
        {
            if (writeLog) Debug.Log("[" + name + "] OnServerRequestConnectionInfo invalid password");
            response.resultCode = (short)MasterServerMessages.ResultCodes.RequestConnectionInfoFailedInvalidPassword;
        }
        else
        {
            response.resultCode = (short)MasterServerMessages.ResultCodes.RequestConnectionInfoSucceed;
            response.networkAddress = room.networkAddress;
            response.networkPort = room.networkPort;
        }

        netMsg.conn.Send(MasterServerMessages.ResponseConnectionInfoId, response);
    }
    #endregion

    #region Gameserver Functions
    public void RegisterGameServer(string gameType, string title, string password, string scene, int networkPort, int maxConnections)
    {
        if (!IsClientActive())
        {
            Debug.LogError("["+name+ "] Cannot RegisterGameServer, client not connected.");
            return;
        }

        var gameNetworkManager = NetworkManager.singleton;
        gameNetworkManager.networkPort = networkPort;
        gameNetworkManager.maxConnections = maxConnections;
        gameNetworkManager.onlineScene = scene;

        var msg = new MasterServerMessages.RegisterGameServerMessage();
        msg.spawnToken = spawnToken;
        msg.registerKey = registerKey;
        msg.gameType = gameType;
        msg.title = title;
        msg.password = password;
        msg.scene = scene;
        msg.networkPort = networkPort;
        msg.maxConnections = maxConnections;
        client.Send(MasterServerMessages.RegisterGameServerId, msg);
    }

    public void RegisterGameServer(string gameType, string title, string password = "")
    {
        var gameNetworkManager = NetworkManager.singleton;
        RegisterGameServer(gameType, title, password, gameNetworkManager.onlineScene, gameNetworkManager.networkPort, gameNetworkManager.maxConnections);
    }

    public void RegisterGameServer()
    {
        RegisterGameServer(gameServerGameType, gameServerTitle, gameServerPassword, gameServerScene, gameServerNetworkPort, gameServerMaxConnections);
    }

    public void UnregisterGameServer()
    {
        if (registeredRoom.Equals(RegisteredMasterServerRoom.Empty))
        {
            Debug.LogError("[" + name + "] Cannot UnregisterGameServer, registered room is empty.");
            return;
        }
        var msg = new MasterServerMessages.UnregisterGameServerMessage();
        msg.roomId = registeredRoom.roomId;
        msg.gameType = registeredRoom.gameType;
        client.Send(MasterServerMessages.UnregisterGameServerId, msg);
    }
    #endregion

    #region Gameserver Handlers
    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);
        if (registerServerOnConnect)
            RegisterGameServer();
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

    protected void OnClientSpawnedGameServer(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.SpawnedGameServerMessage>();
        if (onSpawnedGameServer != null)
            onSpawnedGameServer(msg);
    }

    protected void OnClientResponseConnectionInfo(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<MasterServerMessages.ResponseConnectionInfoMessage>();
        if (onResponseConnectionInfo != null)
            onResponseConnectionInfo(msg);
    }
    #endregion
}
