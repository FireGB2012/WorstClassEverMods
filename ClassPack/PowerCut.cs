using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassPack
{
    /// <summary>
    /// From idea 38: once per class the power goes out for a while. The host picks the moment
    /// and tells every modded player; each player's own game then darkens the picture. The host
    /// also switches off the room's real light groups on floors that have them.
    /// </summary>
    internal static class PowerCut
    {
        private const float FadeSpeed = 2.5f;
        private const float Darkness = -2.6f;

        // Host state
        private static bool _wasInClass;
        private static bool _scheduled;
        private static float _startAt;
        private static bool _hostActive;
        private static float _hostEndAt;
        private static readonly List<LightGroupComponent> SwitchedOff = new List<LightGroupComponent>();

        // Local picture state
        private static float _target;
        private static float _weight;
        private static float _failsafeAt;
        private static Volume _volume;
        private static UniversalAdditionalCameraData _cameraData;
        private static bool _cameraHadPostProcessing;

        public static void Tick()
        {
            TickHost();
            TickPicture();
        }

        private static void TickHost()
        {
            bool inClass = NetworkServer.active && Game.InClass && Plugin.Settings.powerCut;
            if (inClass && !_wasInClass)
            {
                ClassroomManager classroom = Game.Peek<ClassroomManager>();
                // The night classroom and the dormitory are dark already.
                bool darkFloor = classroom.currentFloor == ClassroomManager.NightClassroomFloor
                    || classroom.currentFloor == ClassroomManager.DormitoryFloor;
                float length = Mathf.Max(60f, classroom.ClassDuration);
                _scheduled = !darkFloor;
                _startAt = Time.time + Random.Range(0.25f, 0.65f) * length;
            }
            if (!inClass && _wasInClass)
            {
                _scheduled = false;
                if (_hostActive)
                {
                    EndOnHost();
                }
            }
            _wasInClass = inClass;

            if (_scheduled && inClass && Time.time >= _startAt)
            {
                _scheduled = false;
                BeginOnHost(Mathf.Clamp(Plugin.Settings.powerCutSeconds, 5f, 120f));
            }
            if (_hostActive && Time.time >= _hostEndAt)
            {
                EndOnHost();
            }
        }

        private static void BeginOnHost(float seconds)
        {
            _hostActive = true;
            _hostEndAt = Time.time + seconds;
            SwitchedOff.Clear();
            UnitManager units = Game.Peek<UnitManager>();
            if (units != null)
            {
                foreach (Unit unit in units.unitsDict.Values)
                {
                    LightGroupComponent group = unit != null && unit.isActived ? unit.GetComponent<LightGroupComponent>() : null;
                    if (group != null && group.Ready && group.IsOn && group.TrySetState(group.Version, false))
                    {
                        SwitchedOff.Add(group);
                    }
                }
            }
            Net.SendToModded(new CpPowerCut { on = true, seconds = seconds });
            Plugin.Log("power cut started for " + seconds + "s");
        }

        private static void EndOnHost()
        {
            _hostActive = false;
            foreach (LightGroupComponent group in SwitchedOff)
            {
                if (group != null && group.Ready && !group.IsOn)
                {
                    group.TrySetState(group.Version, true);
                }
            }
            SwitchedOff.Clear();
            Net.SendToModded(new CpPowerCut { on = false, seconds = 0f });
        }

        /// <summary>Runs on every modded player's game when the host starts or ends the cut.</summary>
        public static void OnClientState(CpPowerCut state)
        {
            if (state.on)
            {
                _target = 1f;
                _failsafeAt = Time.time + Mathf.Clamp(state.seconds, 1f, 120f) + 3f;
                Game.PlaySound(AudioEvents.sfx_lightSwitch_sfx);
                Game.Notify("Power cut!", 3f);
            }
            else if (_target > 0f)
            {
                _target = 0f;
                Game.PlaySound(AudioEvents.sfx_lightSwitch_sfx);
                Game.Notify("The power is back", 2.5f);
            }
        }

        private static void TickPicture()
        {
            // Never leave a player in the dark: outside class, or if the "end" message got lost.
            if (_target > 0f && (!Game.InClass || Time.time >= _failsafeAt))
            {
                _target = 0f;
            }
            if (_weight == _target)
            {
                return;
            }
            _weight = Mathf.MoveTowards(_weight, _target, FadeSpeed * Time.unscaledDeltaTime);
            ApplyPicture();
        }

        private static void ApplyPicture()
        {
            if (_volume == null)
            {
                var holder = new GameObject("[ClassPack PowerCut]");
                Object.DontDestroyOnLoad(holder);
                _volume = holder.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 9999f;
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(Darkness);
                color.colorFilter.Override(new Color(0.6f, 0.68f, 0.95f));
                color.saturation.Override(-35f);
                _volume.profile = profile;
            }
            _volume.weight = _weight;
            _volume.enabled = _weight > 0f;

            // The darkening is a post effect, so make sure the camera draws post effects
            // while the cut lasts, and hand the setting back afterwards.
            if (_weight > 0f && _cameraData == null)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    _cameraData = camera.GetUniversalAdditionalCameraData();
                    _cameraHadPostProcessing = _cameraData.renderPostProcessing;
                    _cameraData.renderPostProcessing = true;
                }
            }
            else if (_weight <= 0f && _cameraData != null)
            {
                _cameraData.renderPostProcessing = _cameraHadPostProcessing;
                _cameraData = null;
            }
        }
    }
}
