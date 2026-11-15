using System;
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

    /// <summary>What a CpAsk or CpTell is about. Newer features share these two messages.</summary>
    public enum CpKind : byte
    {
        None = 0,
        Toast = 1,          // tell: show text s
        Crate = 10,         // tell: gun crate i holds gun j at v (j = 0 means taken)
        GunTake = 11,       // ask: take the gun from crate i
        GunDrop = 12,       // ask: throw my gun away
        GunState = 13,      // tell: player a now holds gun i with j shots
        GunFire = 14,       // ask: I fired from v to w and hit t0..t5
        GunFx = 15,         // tell: player a fired gun i from v to w
        PrankCall = 20,     // ask: ring the teacher
        Ring = 21,          // tell: your phone rings for f seconds
        AlarmPull = 30,     // ask: pull the fire alarm
        Alarm = 31,         // tell: the fire alarm runs for f seconds, box at v
        AlarmBox = 32,      // tell: the fire alarm box is at v (i = 1 while it can be pulled)
        TrickHold = 40,     // ask: take trick i in hand (0 = put it away)
        TrickHeld = 41,     // tell: player a now holds trick i
        TrickUse = 42,      // ask: I used held trick i from v at w, on t0
        Plane = 43,         // tell: a paper airplane flies from v to w (i = how it ended, a = thrower)
        Balloon = 44,       // tell: a water balloon flies from v to w (i = 1 soaks board a for f seconds)
        GlueHand = 45,      // tell: teacher a can't throw for f seconds
        GluePuddle = 46,    // tell: glue puddle i at v (j = 1 there, 0 gone)
        GlueFeet = 47,      // tell: teacher a is stuck for f seconds
        LaserAsk = 48,      // ask: my laser shines from v to w on t0
        Laser = 49,         // tell: a laser line from v to w (a = the teacher it blinds, or 0)
        Cushion = 50,       // tell: whoopee cushion i at v (j = 1 put down, 2 went off, 0 gone)
        NoteAsk = 51,       // ask: pass a note
        Note = 52,          // tell: note i goes from v to w in f seconds (j = 1 start, 2 arrived, 3 grabbed)
        NapAsk = 53,        // ask: i = 1 fall asleep, 0 wake up
        Snore = 54,         // tell: player a snores at v, loudness f (below 0: woke up), i = mischief paid
        LookSet = 60,       // ask: my outfit is i (parts, colours, teacher type) and t0 (hat, face, back)
        Look = 61,          // tell: player a wears outfit i, j
        TeacherThrow = 62,  // ask: as teacher type i, I threw from v to w at t0
        Thrown = 63,        // tell: teacher food of type i flies from v to w, hitting a for f seconds
        Pot = 64,           // tell: the lunch lady's pot stands at v (j = 1 there, 0 gone)
        Mop = 65,           // ask: the janitor mops where he stands
        Wet = 66,           // tell: wet floor i at v (j = 1 there, 0 dried)
        WigPull = 67,       // ask: yank the wig off teacher t0
        Wig = 68,           // tell: teacher a's wig lies at v (j = 1), or is back on (j = 0)
        Pool = 70,          // tell: swim class: the pool hall stands at v (j = 1), or is taken away (j = 0)
        SplashAsk = 71,     // ask: I splash from the water at v, looking along w
        Splash = 72,        // tell: water flies from v to w (i = 0 someone got in, 1 a dive, 2 a splash that hit teacher a for f seconds)
        PeeAsk = 73,        // ask: I pee in the pool where I stand (v)
        Pee = 74,           // tell: student a peed in the pool at v: a yellow cloud spreads there
        DanceHall = 75,     // tell: dance class: the hall stands at v with song i (j = 1), or is taken away (j = 0)
        DanceAsk = 76,      // ask: I start (i = 1) or stop (i = 0) dancing
        Dancing = 77,       // tell: character a dances (i = 1) or stops (i = 0)
        Faint = 78          // tell: character a was hit by a nerf dart and faints for f seconds, knocked along v
    }

    /// <summary>Any player to the host: a request for one of the newer features.</summary>
    public struct CpAsk : NetworkMessage
    {
        public CpKind kind;
        public int i;
        public Vector3 v;
        public Vector3 w;
        public ulong t0, t1, t2, t3, t4, t5;
    }

    /// <summary>Host to modded players: news about one of the newer features.</summary>
    public struct CpTell : NetworkMessage
    {
        public CpKind kind;
        public int i;
        public int j;
        public ulong a;
        public float f;
        public Vector3 v;
        public Vector3 w;
        public string s;
    }

    /// <summary>
    /// Sends the mod's messages between players.
    ///
    /// The host never sends a message to itself over the network. The game doesn't either:
    /// everywhere in its code the host calls its own handler directly and only real clients
    /// use the wire, and a message the host posts to itself is never delivered. So every send
    /// here has two paths: a direct call when the other end is this same game, and a network
    /// message when it is another player's.
    /// </summary>
    internal static class Net
    {
        /// <summary>The mod's handlers for one message type, callable without the network.</summary>
        private static class Route<T> where T : struct, NetworkMessage
        {
            public static Action<NetworkConnectionToClient, T> OnHost;
            public static Action<T> OnClient;
        }

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

            Writer<CpAsk>.write = (writer, value) =>
            {
                writer.WriteByte((byte)value.kind);
                writer.WriteInt(value.i);
                writer.WriteVector3(value.v);
                writer.WriteVector3(value.w);
                writer.WriteULong(value.t0);
                writer.WriteULong(value.t1);
                writer.WriteULong(value.t2);
                writer.WriteULong(value.t3);
                writer.WriteULong(value.t4);
                writer.WriteULong(value.t5);
            };
            Reader<CpAsk>.read = reader => new CpAsk
            {
                kind = (CpKind)reader.ReadByte(),
                i = reader.ReadInt(),
                v = reader.ReadVector3(),
                w = reader.ReadVector3(),
                t0 = reader.ReadULong(),
                t1 = reader.ReadULong(),
                t2 = reader.ReadULong(),
                t3 = reader.ReadULong(),
                t4 = reader.ReadULong(),
                t5 = reader.ReadULong()
            };

            Writer<CpTell>.write = (writer, value) =>
            {
                writer.WriteByte((byte)value.kind);
                writer.WriteInt(value.i);
                writer.WriteInt(value.j);
                writer.WriteULong(value.a);
                writer.WriteFloat(value.f);
                writer.WriteVector3(value.v);
                writer.WriteVector3(value.w);
                writer.WriteString(value.s ?? string.Empty);
            };
            Reader<CpTell>.read = reader => new CpTell
            {
                kind = (CpKind)reader.ReadByte(),
                i = reader.ReadInt(),
                j = reader.ReadInt(),
                a = reader.ReadULong(),
                f = reader.ReadFloat(),
                v = reader.ReadVector3(),
                w = reader.ReadVector3(),
                s = reader.ReadString()
            };

            Route<CpAsk>.OnHost = OnAsk;
            Route<CpTell>.OnClient = OnTell;
            Route<CpHello>.OnHost = OnHello;
            Route<CpSnackRequest>.OnHost = TeacherSnacks.OnServerRequest;
            Route<CpOrder>.OnHost = DoorDash.OnServerOrder;
            Route<CpSnackResult>.OnClient = TeacherSnacks.OnClientResult;
            Route<CpDelivery>.OnClient = DoorDash.OnClientDelivery;
            Route<CpPowerCut>.OnClient = PowerCut.OnClientState;
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
                NetworkServer.ReplaceHandler<CpHello>(Route<CpHello>.OnHost);
                NetworkServer.ReplaceHandler<CpSnackRequest>(Route<CpSnackRequest>.OnHost);
                NetworkServer.ReplaceHandler<CpOrder>(Route<CpOrder>.OnHost);
                NetworkServer.ReplaceHandler<CpAsk>(Route<CpAsk>.OnHost);
                _serverRegistered = true;
            }
            if (clientActive && !serverActive && (refresh || !_clientRegistered))
            {
                NetworkClient.ReplaceHandler<CpTell>(Route<CpTell>.OnClient);
                NetworkClient.ReplaceHandler<CpSnackResult>(Route<CpSnackResult>.OnClient);
                NetworkClient.ReplaceHandler<CpDelivery>(Route<CpDelivery>.OnClient);
                NetworkClient.ReplaceHandler<CpPowerCut>(Route<CpPowerCut>.OnClient);
                _clientRegistered = true;
            }

            // A real client says hello once per connection, and only to a host that has the mod.
            NetworkConnection connection = NetworkClient.connection;
            if (!serverActive && CanSendToHost && connection != _helloSentTo)
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
                if (NetworkServer.active)
                {
                    return true;
                }
                NetworkConnection connection = NetworkClient.connection;
                return NetworkClient.active && NetworkClient.isConnected && connection != null
                    && connection.isAuthenticated && Game.RoomHasMod;
            }
        }

        private static void OnHello(NetworkConnectionToClient connection, CpHello message)
        {
            if (!IsLocal(connection))
            {
                ModdedConnections.Add(connection.connectionId);
                Looks.OnPeerJoined(connection);
                Swim.OnPeerJoined(connection);
                Dance.OnPeerJoined(connection);
            }
        }

        /// <summary>Host: a request from a player for one of the newer features.</summary>
        private static void OnAsk(NetworkConnectionToClient connection, CpAsk ask)
        {
            Unit player = PlayerOf(connection);
            if (player == null)
            {
                return;
            }
            switch (ask.kind)
            {
                case CpKind.GunTake:
                case CpKind.GunDrop:
                case CpKind.GunFire:
                    NerfGuns.OnAsk(player, ask);
                    break;
                case CpKind.PrankCall:
                    PrankCall.OnAsk(player);
                    break;
                case CpKind.AlarmPull:
                    FireAlarm.OnAsk(player);
                    break;
                case CpKind.TrickHold:
                case CpKind.TrickUse:
                case CpKind.LaserAsk:
                case CpKind.NoteAsk:
                case CpKind.NapAsk:
                    Tricks.OnAsk(player, ask);
                    break;
                case CpKind.LookSet:
                    Looks.OnAsk(player, ask);
                    break;
                case CpKind.TeacherThrow:
                case CpKind.Mop:
                case CpKind.WigPull:
                    Teachers.OnAsk(player, ask);
                    break;
                case CpKind.SplashAsk:
                case CpKind.PeeAsk:
                    Swim.OnAsk(player, ask);
                    break;
                case CpKind.DanceAsk:
                    Dance.OnAsk(player, ask);
                    break;
            }
        }

        /// <summary>Every modded player: news from the host about one of the newer features.</summary>
        private static void OnTell(CpTell tell)
        {
            switch (tell.kind)
            {
                case CpKind.Toast:
                    Game.Notify(tell.s, tell.f > 0f ? tell.f : 3f);
                    break;
                case CpKind.Crate:
                case CpKind.GunState:
                case CpKind.GunFx:
                    NerfGuns.OnTell(tell);
                    break;
                case CpKind.Ring:
                    PrankCall.OnTell(tell);
                    break;
                case CpKind.Alarm:
                case CpKind.AlarmBox:
                    FireAlarm.OnTell(tell);
                    break;
                case CpKind.TrickHeld:
                case CpKind.Plane:
                case CpKind.Balloon:
                case CpKind.GlueHand:
                case CpKind.GluePuddle:
                case CpKind.GlueFeet:
                case CpKind.Laser:
                case CpKind.Cushion:
                case CpKind.Note:
                case CpKind.Snore:
                    Tricks.OnTell(tell);
                    break;
                case CpKind.Look:
                    Looks.OnTell(tell);
                    break;
                case CpKind.Thrown:
                case CpKind.Pot:
                case CpKind.Wet:
                case CpKind.Wig:
                    Teachers.OnTell(tell);
                    break;
                case CpKind.DanceHall:
                case CpKind.Dancing:
                    Dance.OnTell(tell);
                    break;
                case CpKind.Faint:
                    Faint.OnTell(tell);
                    break;
                case CpKind.Pool:
                case CpKind.Splash:
                case CpKind.Pee:
                    Swim.OnTell(tell);
                    break;
            }
        }

        /// <summary>Host: show a pop-up notice to one player.</summary>
        public static void Toast(Unit player, string text, float seconds = 3f)
        {
            TellPlayer(player, new CpTell { kind = CpKind.Toast, s = text, f = seconds });
        }

        /// <summary>Host: the connection of a player, or null for the host's own player.</summary>
        public static NetworkConnectionToClient ConnectionOf(Unit player)
        {
            if (player == null || player == Game.LocalPlayer)
            {
                return null;
            }
            RoomManager room = Game.Peek<RoomManager>();
            return room != null ? room.GetConnectByPlayerUID(player.UID) : null;
        }

        /// <summary>Host: whether a player's game runs the mod (the host's own always does).</summary>
        public static bool IsModded(Unit player)
        {
            if (player == null)
            {
                return false;
            }
            if (player == Game.LocalPlayer)
            {
                return true;
            }
            NetworkConnectionToClient connection = ConnectionOf(player);
            return connection != null && ModdedConnections.Contains(connection.connectionId);
        }

        /// <summary>Host: tell one player, but only if their game runs the mod.</summary>
        public static void TellPlayer(Unit player, CpTell tell)
        {
            NetworkConnectionToClient connection = ConnectionOf(player);
            if (player == Game.LocalPlayer || (connection != null && ModdedConnections.Contains(connection.connectionId)))
            {
                SendTo(connection, tell);
            }
        }

        /// <summary>Any player: send a request to the host.</summary>
        public static void SendToHost<T>(T message) where T : struct, NetworkMessage
        {
            if (NetworkServer.active)
            {
                Action<NetworkConnectionToClient, T> handler = Route<T>.OnHost;
                if (handler != null)
                {
                    handler(NetworkServer.localConnection, message);
                }
            }
            else if (CanSendToHost)
            {
                NetworkClient.Send(message);
            }
        }

        /// <summary>Host: answer one player.</summary>
        public static void SendTo<T>(NetworkConnectionToClient connection, T message) where T : struct, NetworkMessage
        {
            if (IsLocal(connection))
            {
                Action<T> handler = Route<T>.OnClient;
                if (handler != null)
                {
                    handler(message);
                }
            }
            else
            {
                connection.Send(message);
            }
        }

        /// <summary>Host: tell every player whose game runs the mod, the host's own included.</summary>
        public static void SendToModded<T>(T message) where T : struct, NetworkMessage
        {
            if (!NetworkServer.active)
            {
                return;
            }
            Action<T> local = Route<T>.OnClient;
            if (local != null)
            {
                local(message);
            }
            foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            {
                if (pair.Value != null && !IsLocal(pair.Value) && ModdedConnections.Contains(pair.Key))
                {
                    pair.Value.Send(message);
                }
            }
        }

        /// <summary>Host: the player unit behind a connection.</summary>
        public static Unit PlayerOf(NetworkConnectionToClient connection)
        {
            if (IsLocal(connection))
            {
                return Game.LocalPlayer;
            }
            RoomManager room = Game.Peek<RoomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            if (room == null || units == null)
            {
                return null;
            }
            return units.GetUnitByUID(room.GetPlayerUIDByConnection(connection));
        }

        /// <summary>The game treats "no connection" as the host itself, and so does the mod.</summary>
        public static bool IsLocal(NetworkConnectionToClient connection)
        {
            return connection == null || connection == NetworkServer.localConnection;
        }
    }
}
