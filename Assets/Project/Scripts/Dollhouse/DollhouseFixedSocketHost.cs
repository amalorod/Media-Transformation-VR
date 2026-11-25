using System;
using UnityEngine;

/// <summary>
/// Hosts fixed mini-house hallway sockets without adding room physics or XR interaction.
/// Each hallway socket points to its corresponding real-world doorway transform.
/// </summary>
[DisallowMultipleComponent]
public sealed class DollhouseFixedSocketHost : MonoBehaviour
{
    [Serializable]
    private sealed class SocketWorldMapping
    {
        [SerializeField] private DoorSocket miniSocket;
        [SerializeField] private Transform worldSocket;

        public DoorSocket MiniSocket => miniSocket;
        public Transform WorldSocket => worldSocket;
    }

    [SerializeField] private SocketWorldMapping[] mappings = Array.Empty<SocketWorldMapping>();

    public bool TryGetWorldDoorSocket(DoorSocket socket, out Transform worldSocket)
    {
        if (mappings != null)
        {
            foreach (SocketWorldMapping mapping in mappings)
            {
                if (mapping == null)
                    continue;

                if (mapping.MiniSocket == socket && mapping.WorldSocket != null)
                {
                    worldSocket = mapping.WorldSocket;
                    return true;
                }
            }
        }

        worldSocket = null;
        return false;
    }
}
