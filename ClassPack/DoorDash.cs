using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Idea 11, second version: DoorDash is an app on the student's phone (see Phone.cs).
    /// The student orders from the phone, the host waits a little and then drops the food on
    /// the floor of the teacher's office, and whoever eats it gets a big mischief score.
    /// The risk is the walk: you have to leave your seat and go into the teacher's office.
    /// </summary>
    internal static class DoorDash
    {
        // Item ids from the game's unit table (tbunit.json), all normal edible snacks.
        public static readonly int[] MenuIds = { 6002, 6040, 6041, 6039, 6013, 6038 };
        public static readonly string[] MenuNames = { "Hamburger", "Fries", "Fried chicken bucket", "Chicken drumstick", "Cola", "Cake" };

        public enum Stage
        {
            Idle,
            OnTheWay,
            Arrived,
            Failed
        }

        // What this player's phone shows
        public static Stage Current { get; private set; }
        public static int OrderedFood { get; private set; }
        public static float ArrivesAt { get; private set; }
        public static float ReadyAt { get; private set; }

        // Delivered food that is still lying around, known to every modded player
        private static readonly HashSet<ulong> Deliveries = new HashSet<ulong>();

        // Host only
        private sealed class Pending
        {
            public ulong PlayerUID;
            public int ConnectionId;
            public int Food;
            public float At;
        }

        private static readonly List<Pending> HostPending = new List<Pending>();
        private static readonly Dictionary<ulong, float> HostReadyAt = new Dictionary<ulong, float>();

        public static bool IsDelivery(ulong itemUID)
        {
            return Deliveries.Contains(itemUID);
        }

        /// <summary>Why the local player can't order right now, or null when they can.</summary>
        public static string WhyNot()
        {
            Unit me = Game.LocalPlayer;
            if (!Plugin.Settings.doorDash)
            {
                return "DoorDash is switched off in the mod's settings.";
            }
            if (me == null || !me.IsStudent)
            {
                return "Only students can order.";
            }
            if (!Game.InClass)
            {
                return "DoorDash only delivers during class.";
            }
            if (!Net.CanSendToHost)
            {
                return "The room owner doesn't have Class Pack, so DoorDash is closed here.";
            }
            if (me.IsStun || me.BodyActionsBlocked)
            {
                return "You can't order right now.";
            }
            if (Current == Stage.OnTheWay)
            {
                return "Your order is already on the way.";
            }
            if (Time.time < ReadyAt)
            {
                return "Next order in " + Mathf.CeilToInt(ReadyAt - Time.time) + "s.";
            }
            return null;
        }

        /// <summary>Called by the phone when the player taps a dish.</summary>
        public static void Order(int food)
        {
            if (food < 0 || food >= MenuIds.Length || WhyNot() != null)
            {
                return;
            }
            OrderedFood = food;
            Current = Stage.OnTheWay;
            ArrivesAt = Time.time + Mathf.Max(1f, Plugin.Settings.doorDashWaitSeconds);
            Game.PlaySound(AudioEvents.sfx_StudentPhone_sfx);
            NetworkClient.Send(new CpOrder { food = food });
        }

        public static void Tick()
        {
            if (!Game.InClass)
            {
                // The game clears loose items when a class ends, so forget them too.
                Current = Stage.Idle;
                ReadyAt = 0f;
                Deliveries.Clear();
                HostPending.Clear();
                HostReadyAt.Clear();
                return;
            }
            // If the host's answer never comes, don't leave the phone stuck on "on the way".
            if (Current == Stage.OnTheWay && Time.time > ArrivesAt + 8f)
            {
                Current = Stage.Failed;
                ReadyAt = Time.time + 5f;
            }
            if (!NetworkServer.active)
            {
                HostPending.Clear();
                HostReadyAt.Clear();
                return;
            }
            for (int i = HostPending.Count - 1; i >= 0; i--)
            {
                Pending order = HostPending[i];
                if (Time.time >= order.At)
                {
                    HostPending.RemoveAt(i);
                    Deliver(order);
                }
            }
        }

        // ---------------------------------------------------------------- host side

        public static void OnServerOrder(NetworkConnectionToClient connection, CpOrder message)
        {
            RoomManager room = Game.Peek<RoomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            ulong playerUID = room != null ? room.GetPlayerUIDByConnection(connection) : 0uL;
            Unit player = units != null ? units.GetUnitByUID(playerUID) : null;
            float readyAt;
            bool ok = Plugin.Settings.doorDash && Game.InClass && player != null && player.IsStudent
                && message.food >= 0 && message.food < MenuIds.Length
                && !HostPending.Exists(p => p.PlayerUID == playerUID)
                && !(HostReadyAt.TryGetValue(playerUID, out readyAt) && Time.time < readyAt - 1f);
            if (!ok)
            {
                connection.Send(new CpDelivery { ok = false, ordererUID = playerUID, food = message.food });
                return;
            }
            HostPending.Add(new Pending
            {
                PlayerUID = playerUID,
                ConnectionId = connection.connectionId,
                Food = message.food,
                At = Time.time + Mathf.Max(1f, Plugin.Settings.doorDashWaitSeconds)
            });
        }

        private static void Deliver(Pending order)
        {
            ulong itemUID = 0uL;
            try
            {
                Vector3 spot;
                string how;
                if (TeacherOffice.TryFind(out spot, out how))
                {
                    Vector2 scatter = UnityEngine.Random.insideUnitCircle * 0.45f;
                    spot += new Vector3(scatter.x, 0f, scatter.y);
                    Unit item = SpawnLooseItem(MenuIds[order.Food], spot);
                    itemUID = item != null ? item.UID : 0uL;
                    Plugin.Log("DoorDash: " + MenuNames[order.Food] + " dropped at " + spot.ToString("F2") + " (" + how + "), item " + itemUID);
                }
                else
                {
                    Plugin.Log("DoorDash: no teacher's office found (" + how + ")");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log("DoorDash delivery failed: " + ex);
            }

            var result = new CpDelivery { ok = itemUID != 0uL, itemUID = itemUID, ordererUID = order.PlayerUID, food = order.Food };
            if (result.ok)
            {
                HostReadyAt[order.PlayerUID] = Time.time + Mathf.Max(0f, Plugin.Settings.doorDashCooldownSeconds);
                Net.SendToModded(result);
            }
            else
            {
                NetworkConnectionToClient connection;
                if (NetworkServer.connections.TryGetValue(order.ConnectionId, out connection) && connection != null)
                {
                    connection.Send(result);
                }
            }
        }

        /// <summary>
        /// Puts a loose, pick-up-able item into the world for every player. This is the same
        /// recipe the game's vending machine uses when it drops a snack.
        /// </summary>
        private static Unit SpawnLooseItem(int itemId, Vector3 position)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return null;
            }
            UnitDataNet data = units.CreateUnitNetData(itemId, position, new Vector3(0f, UnityEngine.Random.Range(0f, 360f), 0f),
                delegate(ref UnitDataNet netData)
                {
                    netData.IgnoreSync = false;
                    netData.IsInteraction = true;
                    netData.MountingPointNet = new MountingPointDataNet
                    {
                        HaveValue = true,
                        MountingUID = 0uL,
                        MountingPoint = EUnitPartType.None,
                        IsKinematic = false,
                        IsTrigger = false,
                        IsInteraction = true,
                        LayerName = "Interaction",
                        isSync = true
                    };
                }, 0uL, NetworkServer.active || NetworkClient.active);
            return units.CreateCommonUnit(data, true);
        }

        // -------------------------------------------------------------- every player

        public static void OnClientDelivery(CpDelivery message)
        {
            if (message.ok && message.itemUID != 0uL)
            {
                Deliveries.Add(message.itemUID);
            }
            Unit me = Game.LocalPlayer;
            if (me == null || me.UID != message.ordererUID)
            {
                return;
            }
            int food = Mathf.Clamp(message.food, 0, MenuNames.Length - 1);
            if (message.ok)
            {
                Current = Stage.Arrived;
                ReadyAt = Time.time + Mathf.Max(0f, Plugin.Settings.doorDashCooldownSeconds);
                Game.PlaySound(AudioEvents.sfx_GameAddItem_sfx);
                Game.Notify("DoorDash: your " + MenuNames[food] + " is in the teacher's office. Eat it for "
                    + Mathf.RoundToInt(Plugin.Settings.doorDashPoints) + " mischief", 5f);
            }
            else
            {
                Current = Stage.Failed;
                ReadyAt = Time.time + 5f;
                Game.Notify("DoorDash: order cancelled", 3f);
            }
        }
    }

    /// <summary>
    /// The game asks a snack what each bite is worth. For DoorDash food the answer is nothing
    /// until the last bite, which pays the whole DoorDash score. The game then adds the points
    /// itself, the normal way.
    /// </summary>
    [HarmonyPatch(typeof(FoodComponent), "GetBiteScore")]
    internal static class DoorDashScorePatch
    {
        private static void Postfix(FoodComponent __instance, float durability, ref float __result)
        {
            Unit item = __instance != null ? __instance.owner : null;
            if (item != null && DoorDash.IsDelivery(item.UID))
            {
                __result = durability <= 1f ? Mathf.Max(0f, Plugin.Settings.doorDashPoints) : 0f;
            }
        }
    }
}
