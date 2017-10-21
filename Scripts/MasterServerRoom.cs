using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct MasterServerRoom
{
    public static readonly MasterServerRoom Empty = new MasterServerRoom();
    public string roomId;
    public string gameType;
    public string title;
    public string password;
    public string scene;
    public string networkAddress;
    public int networkPort;
    public int maxConnections;
    public int connectionId;
}

[System.Serializable]
public struct RegisteredMasterServerRoom
{
    public static readonly RegisteredMasterServerRoom Empty = new RegisteredMasterServerRoom();
    public string roomId;
    public string gameType;
    public string title;
    public string scene;
    public bool passwordProtected;
    public int maxConnections;
}
