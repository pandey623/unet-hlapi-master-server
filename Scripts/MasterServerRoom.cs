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
    public string hostIp;
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
    public bool passwordProtected;
    public int maxConnections;
}
