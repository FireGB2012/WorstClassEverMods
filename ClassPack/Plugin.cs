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
            host.AddComponent<HostPanel>();
            host.AddComponent<Phone>();

            Log("loaded. DoorDash=" + Settings.doorDash + " TeacherSnacks=" + Settings.teacherSnacks + " PowerCut=" + Settings.powerCut
                + " SoloClass=" + Settings.soloClass + " PanelKey=" + Settings.PanelKeyCode);
        }

        public static void Log(string message)
        {
            Context?.Log?.Invoke(message);
        }
    }

    /// <summary>Runs every frame for the whole session and drives each feature.</summary>
    internal sealed class Runner : MonoBehaviour
    {
        private float _lastErrorLog = -100f;

        private void Update()
        {
            try
            {
                Game.Tick();
                Net.Tick();
                DoorDash.Tick();
                TeacherSnacks.Tick();
                PowerCut.Tick();
            }
            catch (Exception ex)
            {
                // Never let a mod bug spam the log or break the game loop.
                if (Time.unscaledTime - _lastErrorLog > 10f)
                {
                    _lastErrorLog = Time.unscaledTime;
                    Plugin.Log("error: " + ex);
                }
            }
        }
    }
}
