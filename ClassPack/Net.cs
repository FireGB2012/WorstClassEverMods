using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    // The mod's own network messages. They are only ever sent to a peer that is known to
    // run the mod, because the game drops a connection that sends it a message it doesn't know.

    /// <summary>Client to host: "I run Class Pack too", so the host may send me its messages.</summary>
    public struct CpHello : NetworkMessage
    {
    }

    /// <summary>Teacher's client to host: eat a snack out of this drawer.</summary>
    public struct CpSnackRequest : NetworkMessage
    {
        public ulong drawerUID;
    }

    /// <summary>Host to the teacher's client: whether a snack was eaten.</summary>
    public struct CpSnackResult : NetworkMessage
    {
        public bool ok;
    }

    /// <summary>Student's client to host: a DoorDash order for this dish on the menu.</summary>
    public struct CpOrder : NetworkMessage
    {
        public int food;
    }

    /// <summary>Host to modded clients: a DoorDash order was dropped off, or cancelled.</summary>
    public struct CpDelivery : NetworkMessage
    {
        public bool ok;
        public ulong itemUID;
        public ulong ordererUID;
        public int food;
    }

    /// <summary>Host to every modded client: the power cut starts or ends.</summary>
    public struct CpPowerCut : NetworkMessage
    {
        public bool on;
        public float seconds;
    }

    internal static class Net
    {
        private static readonly HashSet<int> ModdedConnections = new HashSet<int>();
        private static float _nextRegisterAt;
        private static bool _serverRegistered;
        private static bool _clientRegistered;
        private static NetworkConnection _helloSentTo;

        static Net()
        {
            // Mirror normally generates these at build time; a mod has to supply them by hand.
            Writer<CpHello>.write = (writer, value) => { };
            Reader<CpHello>.read = reader => default(CpHello);

            Writer<CpSnackRequest>.write = (writer, value) => writer.WriteULong(value.drawerUID);
            Reader<CpSnackRequest>.read = reader => new CpSnackRequest { drawerUID = reader.ReadULong() };

            Writer<CpSnackResult>.write = (writer, value) => writer.WriteBool(value.ok);
            Reader<CpSnackResult>.read = reader => new CpSnackResult { ok = reader.ReadBool() };

            Writer<CpOrder>.write = (writer, value) => writer.WriteInt(value.food);
            Reader<CpOrder>.read = reader => new CpOrder { food = reader.ReadInt() };

            Writer<CpDelivery>.write = (writer, value) =>
            {
                writer.WriteBool(value.ok);
                writer.WriteULong(value.itemUID);
                writer.WriteULong(value.ordererUID);
                writer.WriteInt(value.food);
            };
            Reader<CpDelivery>.read = reader => new CpDelivery
            {
                ok = reader.ReadBool(),
                itemUID = reader.ReadULong(),
                ordererUID = reader.ReadULong(),
                food = reader.ReadInt()
            };

            Writer<CpPowerCut>.write = (writer, value) =>
            {
                writer.WriteBool(value.on);
                writer.WriteFloat(value.seconds);
            };
            Reader<CpPowerCut>.read = reader => new CpPowerCut { on = reader.ReadBool(), seconds = reader.ReadFloat() };
        }

        public static void Tick()
        {
            bool serverActive = NetworkServer.active;
            bool clientActive = NetworkClient.active;
            if (!serverActive)
            {
                ModdedConnections.Clear();
                _serverRegistered = false;
            }
            if (!clientActive)
            {
                _helloSentTo = null;
                _clientRegistered = false;
            }

            // Register as soon as a session starts, then refresh every two seconds because the
            // game clears and rebuilds its handlers as rooms come and go.
            bool refresh = Time.unscaledTime >= _nextRegisterAt;
            if (refresh)
            {
                _nextRegisterAt = Time.unscaledTime + 2f;
            }
            if (serverActive && (refresh || !_serverRegistered))
            {
                NetworkServer.ReplaceHandler<CpHello>(OnHello);
                NetworkServer.ReplaceHandler<CpSnackRequest>(TeacherSnacks.OnServerRequest);
                NetworkServer.ReplaceHandler<CpOrder>(DoorDash.OnServerOrder);
                _serverRegistered = true;
            }
            if (clientActive && (refresh || !_clientRegistered))
            {
                NetworkClient.ReplaceHandler<CpSnackResult>(TeacherSnacks.OnClientResult);
                NetworkClient.ReplaceHandler<CpDelivery>(DoorDash.OnClientDelivery);
                NetworkClient.ReplaceHandler<CpPowerCut>(PowerCut.OnClientState);
                _clientRegistered = true;
            }

            // Say hello once per connection, and only to a host that has the mod. When we are
            // the host ourselves, our own handlers must be in place first.
            NetworkConnection connection = NetworkClient.connection;
            if (CanSendToHost && connection != _helloSentTo && (!serverActive || _serverRegistered))
            {
                _helloSentTo = connection;
                NetworkClient.Send(default(CpHello));
            }
        }

        /// <summary>True when the mod may send its own messages to the host.</summary>
        public static bool CanSendToHost
        {
            get
            {
                NetworkConnection connection = NetworkClient.connection;
                return NetworkClient.active && NetworkClient.isConnected && connection != null
                    && connection.isAuthenticated && Game.RoomHasMod;
            }
        }

        private static void OnHello(NetworkConnectionToClient connection, CpHello message)
        {
            ModdedConnections.Add(connection.connectionId);
        }

        /// <summary>Host: send to every connected player whose game runs the mod.</summary>
        public static void SendToModded<T>(T message) where T : struct, NetworkMessage
        {
            if (!NetworkServer.active)
            {
                return;
            }
            foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            {
                if (pair.Value != null && ModdedConnections.Contains(pair.Key))
                {
                    pair.Value.Send(message);
                }
            }
        }
    }
}
