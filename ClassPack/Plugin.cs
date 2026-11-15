using System;
using HarmonyLib;
using UnityEngine;
using WorstClassEver.ModAPI;

namespace ClassPack
{
    /// <summary>Entry point the game calls once at startup (see mod.json entryType).</summary>
    public sealed class Plugin : IMod
    {
        public const string ModId = "classpack";

        public static ModContext Context { get; private set; }
        public static Settings Settings { get; private set; }

        public void OnLoad(ModContext context)
        {
            Context = context;
            Settings = Settings.Load(context.ConfigDirectory);

            // The game unpatches by mod id on shutdown, so the Harmony id must be the mod id.
            new Harmony(context.Id).PatchAll(typeof(Plugin).Assembly);

            var host = new GameObject("[ClassPack]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
            host.AddComponent<LateRunner>();
            host.AddComponent<HostPanel>();
            host.AddComponent<Phone>();
            host.AddComponent<Wardrobe>();
            host.AddComponent<Hud>();
            AutoTest.Arm(context.ConfigDirectory);

            Log("loaded. DoorDash=" + Settings.doorDash + " TeacherSnacks=" + Settings.teacherSnacks + " PowerCut=" + Settings.powerCut
                + " SoloClass=" + Settings.soloClass + " NerfGuns=" + Settings.nerfGuns + " WindowPoles=" + Settings.windowPoles
                + " PrankCall=" + Settings.prankCall + " FireAlarm=" + Settings.fireAlarm + " Tricks=" + Settings.tricks + " Looks=" + Settings.looks + " Teachers=" + Settings.teachers + " Swim=" + Settings.swimClass);
        }

        public static void Log(string message)
        {
            Context?.Log?.Invoke(message);
        }
    }

    /// <summary>Runs every frame for the whole session and drives each feature.</summary>
    internal sealed class Runner : MonoBehaviour
    {
        private static readonly Action[] Features =
        {
            Game.Tick, Net.Tick, Poles.Tick, DoorDash.Tick, TeacherSnacks.Tick, PowerCut.Tick,
            NerfGuns.Tick, WindowPoles.Tick, PrankCall.Tick, FireAlarm.Tick, Tricks.Tick, Swim.Tick, Dance.Tick, Faint.Tick, SelfView.Tick, Looks.Tick, Teachers.Tick,
            Tips.Tick, AutoTest.Tick
        };

        private readonly float[] _lastErrorLog = new float[Features.Length];

        private void Update()
        {
            // Each feature gets its own safety net, so a bug in one can't stop the others,
            // spam the log or break the game's own frame.
            for (int i = 0; i < Features.Length; i++)
            {
                try
                {
                    Features[i]();
                }
                catch (Exception ex)
                {
                    if (_lastErrorLog[i] == 0f || Time.unscaledTime - _lastErrorLog[i] > 10f)
                    {
                        _lastErrorLog[i] = Time.unscaledTime;
                        Plugin.Log("error in " + Features[i].Method.DeclaringType.Name + ": " + ex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Runs at the end of every frame, as late as Unity allows, after the game has posed its
    /// characters: the swimming move goes on top of that pose, and hats follow the moved heads.
    /// </summary>
    [DefaultExecutionOrder(30000)]
    internal sealed class LateRunner : MonoBehaviour
    {
        // In this order: the swim and dance moves pose the bodies, a rag doll goes over that,
        // then the faces, then what everybody wears and holds follows the posed bodies.
        private static readonly Action[] Steps =
        {
            SwimPose.LateTick, Faint.LateTick, Looks.FaceTick, Looks.LateTick, NerfGuns.LateTick, Tricks.LateTick
        };

        private readonly float[] _lastErrorLog = new float[Steps.Length];

        private void LateUpdate()
        {
            // Each step gets its own safety net, like the features in Update.
            for (int i = 0; i < Steps.Length; i++)
            {
                try
                {
                    Steps[i]();
                }
                catch (Exception ex)
                {
                    if (_lastErrorLog[i] == 0f || Time.unscaledTime - _lastErrorLog[i] > 10f)
                    {
                        _lastErrorLog[i] = Time.unscaledTime;
                        Plugin.Log("error late in the frame in " + Steps[i].Method.DeclaringType.Name + "." + Steps[i].Method.Name + ": " + ex);
                    }
                }
            }
        }
    }
}
