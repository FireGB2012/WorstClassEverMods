using System.Collections.Generic;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Idea 29: prank call the teacher from the phone. The teacher's phone rings for a few
    /// seconds. Answering it costs nothing but a moment of attention; ignoring it costs
    /// stamina. The caller gets a little mischief either way.
    /// </summary>
    internal static class PrankCall
    {
        private const float RingSeconds = 8f;
        private const float CallerWait = 60f;
        private const float Mischief = 3f;
        private const float IgnoredCost = 10f;

        // Caller's phone
        public static float ReadyAt { get; private set; }

        // Teacher's game
        private static float _ringUntil;
        private static float _nextRingSound;

        // Host
        private static readonly Dictionary<ulong, float> HostReadyAt = new Dictionary<ulong, float>();

        public static string WhyNot()
        {
            Unit me = Game.LocalPlayer;
            if (!Plugin.Settings.prankCall)
            {
                return "Prank calls are switched off in the mod's settings.";
            }
            if (me == null || !me.IsStudent)
            {
                return "Only students make prank calls.";
            }
            if (!Game.InClass)
            {
                return "Wait until class has started.";
            }
            if (!Net.CanSendToHost)
            {
                return "The room owner doesn't have Class Pack.";
            }
            if (me.IsStun || me.BodyActionsBlocked)
            {
                return "You can't call right now.";
            }
            if (Time.time < ReadyAt)
            {
                return "Next call in " + Mathf.CeilToInt(ReadyAt - Time.time) + "s.";
            }
            return null;
        }

        public static void Call()
        {
            if (WhyNot() != null)
            {
                return;
            }
            ReadyAt = Time.time + CallerWait;
            Game.PlaySound(AudioEvents.sfx_StudentPhone_sfx);
            Net.SendToHost(new CpAsk { kind = CpKind.PrankCall });
        }

        public static void Tick()
        {
            if (!Game.InClass)
            {
                _ringUntil = 0f;
                ReadyAt = 0f;
                HostReadyAt.Clear();
                return;
            }
            if (_ringUntil <= 0f)
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            if (me == null || !me.IsTeacher)
            {
                _ringUntil = 0f;
                return;
            }
            if (Input.GetKeyDown(Plugin.Settings.AnswerKeyCode) && !Game.IsTyping)
            {
                _ringUntil = 0f;
                Game.Notify("\"Is your fridge running?\" It was a prank call.", 3f);
                return;
            }
            if (Time.time >= _ringUntil)
            {
                _ringUntil = 0f;
                SkillComponent skill = me.GetComponent<SkillComponent>();
                if (skill != null)
                {
                    skill.UseSkillEnergy(IgnoredCost);
                }
                Game.Notify("You ignored the call. The principal is not happy: -" + Mathf.RoundToInt(IgnoredCost) + " stamina", 3.5f);
                return;
            }
            if (Time.time >= _nextRingSound)
            {
                _nextRingSound = Time.time + 1.7f;
                Game.PlaySound(AudioEvents.sfx_PhoneBell_sfx);
            }
        }

        /// <summary>Host: a student asks to ring the teacher.</summary>
        public static void OnAsk(Unit caller)
        {
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            float readyAt;
            if (!Plugin.Settings.prankCall || units == null || classroom == null || !Game.InClass || !caller.IsStudent
                || (HostReadyAt.TryGetValue(caller.UID, out readyAt) && Time.time < readyAt - 1f))
            {
                return;
            }
            var teachers = new List<Unit>();
            foreach (Unit player in units.playerList)
            {
                if (player != null && player.isActived && player.isPlayer && player.IsTeacher)
                {
                    teachers.Add(player);
                }
            }
            if (teachers.Count == 0)
            {
                Net.Toast(caller, "Nobody picked up. There is no teacher in this class.", 3f);
                return;
            }
            if (!Phones.HostUse(caller))
            {
                Net.Toast(caller, "You need a phone in your hand for that.", 3f);
                return;
            }
            HostReadyAt[caller.UID] = Time.time + CallerWait;
            Unit teacher = teachers[Random.Range(0, teachers.Count)];
            Net.TellPlayer(teacher, new CpTell { kind = CpKind.Ring, f = RingSeconds, a = caller.UID });
            classroom.AddTrickCount(caller.UID, Mischief);
            Net.Toast(caller, "Ringing the teacher...", 3f);
        }

        /// <summary>Teacher's game: the phone starts ringing.</summary>
        public static void OnTell(CpTell tell)
        {
            Unit me = Game.LocalPlayer;
            if (me == null || !me.IsTeacher)
            {
                return;
            }
            _ringUntil = Time.time + Mathf.Clamp(tell.f, 2f, 20f);
            _nextRingSound = 0f;
            Game.Notify("Your phone is ringing! Press " + Plugin.Settings.AnswerKeyCode + " to answer", 4f);
        }

        public static string DrawHud()
        {
            if (_ringUntil > 0f)
            {
                return "Phone ringing: press " + Plugin.Settings.AnswerKeyCode + " to answer  (" + Mathf.CeilToInt(_ringUntil - Time.time) + "s)";
            }
            return null;
        }
    }
}
