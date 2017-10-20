using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MasterServerNetworkManager : NetworkManagerSimple
{
    protected override void RegisterServerMessages()
    {
        base.RegisterServerMessages();
    }

    protected override void RegisterClientMessages(NetworkClient client)
    {
        base.RegisterClientMessages(client);
    }
}
