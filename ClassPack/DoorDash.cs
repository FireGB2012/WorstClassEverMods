using System;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Idea 11: order food from a computer. While you sit at a computer during class, press
    /// the DoorDash key; after a short wait a random fast-food item lands in your desk drawer.
    /// Uses the same route the computer mini-games use to drop their rewards.
    /// </summary>
    internal static class DoorDash
    {
        // Item ids from the game's unit table (tbunit.json), all normal edible snacks.
        private static readonly int[] MenuIds = { 6002, 6040, 6041, 6039, 6013, 6038 };
        private static readonly string[] MenuNames = { "Hamburger", "Fries", "Fried chicken bucket", "Chicken drumstick", "Cola", "Cake" };

        private static bool _waiting;
        private static float _arrivesAt;
        private static float _readyAt;
        private static float _nextHintAt;
        private static bool _wasAtComputer;
        private static int _order;
        private static DrawerComponent _drawer;

        public static void Tick()
        {
            Settings settings = Plugin.Settings;
            if (!settings.doorDash)
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            if (me == null || !Game.InClass || !Game.RoomHasMod || !me.IsStudent)
            {
                _waiting = false;
                _wasAtComputer = false;
                _drawer = null;
                return;
            }

            if (_waiting)
            {
                if (Time.time >= _arrivesAt)
                {
                    _waiting = false;
                    Deliver(_drawer, _order);
                }
                return;
            }

            DrawerComponent drawer = FindComputerDrawer(me);
            bool atComputer = drawer != null;
            if (atComputer && !_wasAtComputer && Time.time >= _nextHintAt)
            {
                _nextHintAt = Time.time + 60f;
                Game.Notify("DoorDash: press " + settings.DoorDashKeyCode + " to order food to this desk");
            }
            _wasAtComputer = atComputer;

            if (!atComputer || me.IsStun || Game.IsTyping || !Input.GetKeyDown(settings.DoorDashKeyCode))
            {
                return;
            }
            if (Time.time < _readyAt)
            {
                Game.Notify("DoorDash: next order in " + Mathf.CeilToInt(_readyAt - Time.time) + "s", 2f);
                return;
            }

            _order = UnityEngine.Random.Range(0, MenuIds.Length);
            _drawer = drawer;
            _waiting = true;
            _arrivesAt = Time.time + Mathf.Max(1f, settings.doorDashWaitSeconds);
            Game.PlaySound(AudioEvents.sfx_StudentPhone_sfx);
            Game.Notify("DoorDash: " + MenuNames[_order] + " ordered. It lands in your drawer in " + Mathf.RoundToInt(settings.doorDashWaitSeconds) + "s");
        }

        /// <summary>The drawer of the computer desk the player sits at, or null.</summary>
        private static DrawerComponent FindComputerDrawer(Unit me)
        {
            if (!me.IsSitting || me.currentSitUnit == null)
            {
                return null;
            }
            ChairComponent chair = me.currentSitUnit.GetComponent<ChairComponent>();
            ClassroomComputerManager computers = Game.Peek<ClassroomComputerManager>();
            if (chair == null || computers == null || computers.FindByChair(chair) == null || chair.config == null)
            {
                return null;
            }
            Unit drawerUnit = chair.config.GetDrawerUnit();
            return drawerUnit != null ? drawerUnit.GetComponent<DrawerComponent>() : null;
        }

        private static async void Deliver(DrawerComponent drawer, int order)
        {
            try
            {
                Unit item = null;
                if (drawer != null && drawer.IsActive && Game.InClass)
                {
                    item = await drawer.AddItemAsync(MenuIds[order]);
                }
                if (item != null)
                {
                    _readyAt = Time.time + Mathf.Max(0f, Plugin.Settings.doorDashCooldownSeconds);
                    Game.PlaySound(AudioEvents.sfx_GameAddItem_sfx);
                    Game.Notify("DoorDash: your " + MenuNames[order] + " is in your drawer");
                }
                else if (Game.InClass)
                {
                    _readyAt = Time.time + 5f;
                    Game.Notify("DoorDash: no room in your drawer, order cancelled");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log("DoorDash delivery failed: " + ex);
            }
        }
    }
}
