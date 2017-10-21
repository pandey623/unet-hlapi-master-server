using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MasterServerRooms {
    public string gameType;
    private readonly Dictionary<string, MasterServerRoom> rooms = new Dictionary<string, MasterServerRoom>();

    public bool AddRoom(string roomId, MasterServerRoom room, out RegisteredMasterServerRoom registeredRoom)
    {
        registeredRoom = RegisteredMasterServerRoom.Empty;
        if (rooms.ContainsKey(roomId))
            return false;

        rooms[roomId] = room;
        registeredRoom = new RegisteredMasterServerRoom();
        registeredRoom.roomId = room.roomId;
        registeredRoom.gameType = room.gameType;
        registeredRoom.title = room.title;
        registeredRoom.scene = room.scene;
        registeredRoom.passwordProtected = !string.IsNullOrEmpty(room.password);
        registeredRoom.maxConnections = room.maxConnections;
        return true;
    }

    public bool RemoveRoom(string roomId)
    {
        return rooms.Remove(roomId);
    }

    public bool TryGetRoom(string roomId, out MasterServerRoom room)
    {
        room = MasterServerRoom.Empty;
        if (rooms.ContainsKey(roomId))
        {
            room = rooms[roomId];
            return true;
        }
        return false;
    }

    public MasterServerRoom[] GetRooms()
    {
        return rooms.Values.ToArray();
    }

    public RegisteredMasterServerRoom[] GetRegisteredRooms()
    {
        var roomValues = rooms.Values;
        var result = new RegisteredMasterServerRoom[roomValues.Count];
        var i = 0;
        foreach (var room in roomValues)
        {
            var registeredRoom = new RegisteredMasterServerRoom();
            registeredRoom.roomId = room.roomId;
            registeredRoom.gameType = room.gameType;
            registeredRoom.title = room.title;
            registeredRoom.scene = room.scene;
            registeredRoom.passwordProtected = !string.IsNullOrEmpty(room.password);
            registeredRoom.maxConnections = room.maxConnections;
            result[i] = registeredRoom;
            ++i;
        }
        return result;
    }
}
