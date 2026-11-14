using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// From idea 6: the teacher takes snacks out of student drawers and eats them for stamina.
    /// The teacher stands by a desk and presses the snack key. The host removes one snack
    /// from that drawer for everyone, then the teacher's own game adds the stamina, which is
    /// how the game itself handles stamina (blackboard writing works the same way).
    /// </summary>
    internal static class TeacherSnacks
    {
        private const float Reach = 2.2f;
        private const float HostReach = 4f;
        private const float Cooldown = 6f;

        private static bool _hinted;
        private static float _readyAt;
        private static readonly Dictionary<ulong, float> HostLastEat = new Dictionary<ulong, float>();

        public static void Tick()
        {
            if (!NetworkServer.active)
            {
                HostLastEat.Clear();
            }
            if (!Plugin.Settings.teacherSnacks)
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            if (me == null || !Game.InClass || !me.IsTeacher || !Net.CanSendToHost)
            {
                _hinted = false;
                return;
            }
            KeyCode snackKey = Plugin.Settings.TeacherSnackKeyCode;
            if (!_hinted)
            {
                _hinted = true;
                Game.Notify("Hungry? Stand by a desk and press " + snackKey + " to eat a snack from its drawer (+"
                    + Mathf.RoundToInt(Plugin.Settings.teacherSnackStamina) + " stamina)", 6f);
            }
            if (Game.IsTyping || !Input.GetKeyDown(snackKey))
            {
                return;
            }
            if (Time.time < _readyAt)
            {
                Game.Notify("Still chewing. Next snack in " + Mathf.CeilToInt(_readyAt - Time.time) + "s", 2f);
                return;
            }
            Unit drawer = FindNearestDrawerWithSnack(me.Position, Reach);
            if (drawer == null)
            {
                Game.Notify("No snacks within reach", 2f);
                return;
            }
            _readyAt = Time.time + Cooldown;
            Net.SendToHost(new CpSnackRequest { drawerUID = drawer.UID });
        }

        /// <summary>Runs on the teacher's own game when the host answers.</summary>
        public static void OnClientResult(CpSnackResult result)
        {
            Unit me = Game.LocalPlayer;
            if (!result.ok || me == null || !me.IsTeacher)
            {
                _readyAt = 0f;
                Game.Notify("That drawer is empty", 2f);
                return;
            }
            SkillComponent skill = me.GetComponent<SkillComponent>();
            if (skill != null)
            {
                skill.AddSkillEnergy(Mathf.Max(0f, Plugin.Settings.teacherSnackStamina));
            }
            Game.PlaySound(AudioEvents.sfx_StudentEat_sfx);
            Game.Notify("Yum. +" + Mathf.RoundToInt(Plugin.Settings.teacherSnackStamina) + " stamina", 2.5f);
        }

        /// <summary>Runs on the host: checks the request and removes one snack for everyone.</summary>
        public static void OnServerRequest(NetworkConnectionToClient connection, CpSnackRequest request)
        {
            bool ok = TryEat(connection, request.drawerUID);
            Net.SendTo(connection, new CpSnackResult { ok = ok });
        }

        private static bool TryEat(NetworkConnectionToClient connection, ulong drawerUID)
        {
            RoomManager room = Game.Peek<RoomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            if (room == null || units == null || !Game.InClass || !Plugin.Settings.teacherSnacks)
            {
                return false;
            }
            Unit teacher = Net.PlayerOf(connection);
            float lastEat;
            if (teacher == null || (HostLastEat.TryGetValue(teacher.UID, out lastEat) && Time.time - lastEat < Cooldown - 1f))
            {
                return false;
            }
            Unit drawerUnit = units.GetUnitByUID(drawerUID);
            DrawerComponent drawer = drawerUnit != null ? drawerUnit.GetComponent<DrawerComponent>() : null;
            if (teacher == null || !teacher.IsTeacher || drawer == null
                || (teacher.Position - drawerUnit.Position).sqrMagnitude > HostReach * HostReach)
            {
                return false;
            }
            Unit snack = FindSnackIn(units, drawerUID);
            if (snack == null)
            {
                return false;
            }
            HostLastEat[teacher.UID] = Time.time;
            // Same two steps the game takes when a student finishes a snack: the drawer forgets
            // the item (and may restock), then the item is destroyed for every player. The host
            // calls the game's handler directly, as the game itself does; "no connection" is
            // how the game marks a request that comes from the host.
            drawer.OnItemDetached(snack.UID, true, 0uL);
            room.GetUnitMessageHandler().OnUnitDestroyMessageRequest(null, new UnitDestroyMessageRequest_C2S { unitUID = snack.UID });
            NerfGuns.OnTeacherAte(teacher);
            return true;
        }

        private static bool IsSnackIn(Unit unit, out ulong drawerUID)
        {
            drawerUID = 0uL;
            if (unit == null || !unit.isActived || unit.IsDead || unit.UnitSubType != EUnitSubType.Food)
            {
                return false;
            }
            RoleData data = unit.GetData<RoleData>();
            if (data == null)
            {
                return false;
            }
            MountingPointDataNet mount = data.NetData.MountingPointNet;
            if (!mount.HaveValue || mount.MountingUID == 0uL)
            {
                return false;
            }
            drawerUID = mount.MountingUID;
            return true;
        }

        private static Unit FindSnackIn(UnitManager units, ulong drawerUID)
        {
            foreach (Unit unit in units.unitsDict.Values)
            {
                ulong mountedOn;
                if (IsSnackIn(unit, out mountedOn) && mountedOn == drawerUID)
                {
                    return unit;
                }
            }
            return null;
        }

        private static Unit FindNearestDrawerWithSnack(Vector3 from, float reach)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return null;
            }
            Unit best = null;
            float bestDistance = reach * reach;
            foreach (Unit unit in units.unitsDict.Values)
            {
                ulong mountedOn;
                if (!IsSnackIn(unit, out mountedOn))
                {
                    continue;
                }
                Unit holder = units.GetUnitByUID(mountedOn);
                if (holder == null || holder.UnitSubType != EUnitSubType.Drawer || holder.GetComponent<DrawerComponent>() == null)
                {
                    continue;
                }
                float distance = (holder.Position - from).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = holder;
                }
            }
            return best;
        }
    }
}
