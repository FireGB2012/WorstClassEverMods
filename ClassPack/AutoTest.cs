using System;
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// A hands-free test of the swim class, for checking the mod without a player at the keys.
    ///
    /// It only runs when a file named "autotest.txt" lies in the mod's settings folder at start.
    /// The file is deleted as it is read, so one file gives one run and normal play is never
    /// affected. The run hosts an offline room (nobody can join it), starts a swim class alone,
    /// walks through a fixed list of steps, saves pictures and a report into the "autotest"
    /// folder next to the settings, and closes the game.
    ///
    /// Lines in autotest.txt:  role=student | role=teacher   and   kind=0..4 (teacher type).
    /// </summary>
    internal static class AutoTest
    {
        private sealed class Step
        {
            public float At;
            public string Name;
            public Action Do;
        }

        private static bool _armed;
        private static string _folder;
        private static bool _teacher;
        private static int _kind;
        /// <summary>True when the test runs a dance class instead of a swim class ("class=dance").</summary>
        private static bool _dance;

        /// <summary>Where the hall of the class under test stands: picture spots are given in its own frame.</summary>
        private static Vector3 Hall => _dance ? Dance.Origin : Swim.Origin;

        private static bool HallUp => _dance ? Dance.Active : Swim.Active;
        private static int _phase;
        private static float _phaseAt;
        private static float _startedAt;
        private static bool _hosting;
        private static bool _rang;
        private static float _steady;
        private static float _stepsFrom;
        private static int _nextStep;
        private static readonly List<Step> Steps = new List<Step>();
        private static readonly List<string> Report = new List<string>();

        private static Camera _camera;
        private static RenderTexture _target;
        private static string _shotName;
        private static int _shotFrames;
        private static Unit _follow;

        public static bool Running => _armed;

        /// <summary>Called once when the mod loads.</summary>
        public static void Arm(string settingsFolder)
        {
            try
            {
                string flag = Path.Combine(settingsFolder, "autotest.txt");
                if (!File.Exists(flag))
                {
                    return;
                }
                foreach (string line in File.ReadAllLines(flag))
                {
                    string[] parts = line.Trim().Split('=');
                    if (parts.Length != 2)
                    {
                        continue;
                    }
                    if (parts[0] == "role")
                    {
                        _teacher = parts[1] == "teacher";
                    }
                    else if (parts[0] == "kind")
                    {
                        int.TryParse(parts[1], out _kind);
                    }
                    else if (parts[0] == "class")
                    {
                        _dance = parts[1] == "dance";
                    }
                }
                File.Delete(flag);
                _folder = Path.Combine(settingsFolder, "autotest");
                Directory.CreateDirectory(_folder);
                foreach (string old in Directory.GetFiles(_folder))
                {
                    File.Delete(old);
                }
                _armed = true;
                _startedAt = Time.realtimeSinceStartup;
                _phaseAt = _startedAt;
                Application.runInBackground = true;
                Note("armed: " + (_teacher ? "teacher, type " + _kind : "student") + ", " + (_dance ? "dance" : "swim") + " class");
            }
            catch (Exception ex)
            {
                Plugin.Log("autotest: could not start: " + ex.Message);
            }
        }

        private static void Note(string text)
        {
            float t = Time.realtimeSinceStartup - _startedAt;
            Report.Add(t.ToString("F1") + "s  " + text);
            Plugin.Log("autotest: " + text);
        }

        private static void Finish(string why)
        {
            Note("finished: " + why);
            try
            {
                File.WriteAllLines(Path.Combine(_folder, "report.txt"), Report.ToArray());
            }
            catch (Exception)
            {
            }
            _armed = false;
            Application.Quit();
        }

        private static void NextPhase()
        {
            _phase++;
            _phaseAt = Time.realtimeSinceStartup;
        }

        public static void Tick()
        {
            if (!_armed)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            float inPhase = now - _phaseAt;
            if (now - _startedAt > 420f)
            {
                Finish("took too long, stopped in phase " + _phase);
                return;
            }
            TickShot();
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            Unit me = Game.LocalPlayer;
            switch (_phase)
            {
                case 0:
                {
                    // The main menu.
                    bool menu = false;
                    try
                    {
                        menu = GameRoot.Inst != null && Game.Peek<NetManager>() != null && SingletonT<UIManager>.Instance.IsUIOpen(EUIWindowType.Login);
                    }
                    catch (Exception)
                    {
                    }
                    _steady = menu ? _steady + Time.unscaledDeltaTime : 0f;
                    // Host once the menu has been up for a few seconds, or, if the menu can't
                    // be told apart, once the game has clearly finished starting.
                    bool started = GameRoot.Inst != null && Game.Peek<NetManager>() != null;
                    if (_steady > 4f || (started && inPhase > 70f))
                    {
                        Note("main menu " + (menu ? "is up" : "not recognised, hosting anyway"));
                        _steady = 0f;
                        NextPhase();
                        Host();
                    }
                    else if (inPhase > 150f)
                    {
                        Finish("the game never finished starting");
                    }
                    break;
                }
                case 1:
                {
                    // In the room, before class.
                    bool ready = NetworkServer.active && me != null && me.isActived && classroom != null && classroom.curState == EClassroomState.BeforeClass;
                    _steady = ready ? _steady + Time.unscaledDeltaTime : 0f;
                    if (_steady > 7f)
                    {
                        Note("in the room as " + (me.IsTeacher ? "teacher" : "student") + " at " + me.Position.ToString("F1"));
                        if (_teacher && !me.IsTeacher)
                        {
                            MonoSingletonT<RoomManager>.Instance.ChangeIdentify(true);
                        }
                        _steady = 0f;
                        NextPhase();
                    }
                    else if (inPhase > 120f)
                    {
                        Finish("never got into the room (hosting started: " + _hosting + ")");
                    }
                    break;
                }
                case 2:
                    // Role settled: pick the teacher type, then ring the bell.
                    if (inPhase > 3f && me != null && classroom != null && !_rang)
                    {
                        if (_teacher)
                        {
                            Look look = Looks.Mine;
                            look.Teacher = Mathf.Clamp(_kind, 0, 4);
                            Looks.Mine = look;
                            Note("now " + (me.IsTeacher ? "teacher" : "STILL A STUDENT") + ", type " + look.Teacher);
                        }
                        Plugin.Settings.nextClass = _dance ? Dance.DanceClass : Swim.SwimClass;
                        classroom.ClassPrepare(0uL);
                        _rang = true;
                    }
                    if (_rang && classroom != null && classroom.curState != EClassroomState.BeforeClass)
                    {
                        Note("class is starting, state " + classroom.curState);
                        NextPhase();
                    }
                    else if (_rang && inPhase > 8f && inPhase < 9f)
                    {
                        // No bell near enough: ring the first one there is.
                        UnitManager units = Game.Peek<UnitManager>();
                        if (units != null && classroom != null)
                        {
                            foreach (Unit unit in units.unitsDict.Values)
                            {
                                if (unit != null && unit.isActived && unit.GetComponent<StartClassRingComponent>() != null)
                                {
                                    classroom.ClassPrepare(unit.UID);
                                    break;
                                }
                            }
                        }
                    }
                    else if (inPhase > 25f)
                    {
                        Finish("the class did not start");
                    }
                    break;
                case 3:
                    // Wait until the pool hall stands and this player is in it.
                    _steady = HallUp && me != null && (me.Position - Hall).magnitude < 40f ? _steady + Time.unscaledDeltaTime : 0f;
                    if (_steady > 2f)
                    {
                        Note("in the " + (_dance ? "dance" : "pool") + " hall at " + (me.Position - Hall).ToString("F1") + " (hall frame)");
                        if (_dance)
                        {
                            PlanDance(me);
                        }
                        else
                        {
                            Plan(me);
                        }
                        _stepsFrom = now;
                        _nextStep = 0;
                        NextPhase();
                    }
                    else if (inPhase > 40f)
                    {
                        Finish("never arrived in the hall (hall up: " + HallUp + ")");
                    }
                    break;
                case 4:
                    if (_shotName != null)
                    {
                        break;
                    }
                    if (_nextStep >= Steps.Count)
                    {
                        Finish("all steps done");
                        break;
                    }
                    Step step = Steps[_nextStep];
                    if (now - _stepsFrom >= step.At)
                    {
                        _nextStep++;
                        try
                        {
                            step.Do();
                        }
                        catch (Exception ex)
                        {
                            Note("step '" + step.Name + "' failed: " + ex.Message);
                        }
                    }
                    break;
            }
        }

        private static async void Host()
        {
            try
            {
                _hosting = true;
                NetManager net = Game.Peek<NetManager>();
                net.SetRoomName("Class Pack test");
                net.SetPrivateRoom(true);
                // Offline: the room lives on this computer only and is not listed anywhere.
                GameRoot.Inst.NetworkType = ENetworkType.Local;
                if (GameRoot.Inst.LoadingUI != null)
                {
                    GameRoot.Inst.LoadingUI.SafeSetActive(true);
                }
                SingletonT<UIManager>.Instance.CloseUI(EUIWindowType.Login);
                bool ok = await net.StartHost();
                Note("offline room hosted: " + ok);
                if (ok)
                {
                    MonoSingletonT<global::SceneManager>.Instance.ChangeScene(2001);
                }
                else
                {
                    Finish("the room could not be hosted");
                }
            }
            catch (Exception ex)
            {
                Finish("hosting failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------------- the steps

        private static void Add(float at, string name, Action act)
        {
            Steps.Add(new Step { At = at, Name = name, Do = act });
        }

        private static void Plan(Unit me)
        {
            Steps.Clear();
            float t = 1f;
            // The hall itself, from fixed camera spots (hall frame: x along the pool, z across).
            Add(t += 0.5f, "skeleton", () => DumpSkeleton(Game.LocalPlayer, me.IsTeacher ? "skeleton_teacher.txt" : "skeleton_student.txt"));
            Add(t += 1f, "eyes", () => Screen("01_eyes_arrival"));
            Add(t += 1f, "overview", () => Shot("02_overview_shallow", new Vector3(-13f, 5f, -8f), new Vector3(3f, 0f, 0.5f)));
            Add(t += 1f, "overview", () => Shot("03_overview_deep", new Vector3(13f, 5f, 8f), new Vector3(-3f, 0f, -0.5f)));
            Add(t += 1f, "board", () => Shot("04_board", new Vector3(4.2f, 2.3f, -4.2f), new Vector3(8.6f, 0.7f, -0.2f)));
            Add(t += 1f, "chair", () => Shot("05_chair_benches", new Vector3(-4f, 2.2f, 1.5f), new Vector3(0.5f, 0.9f, 7.5f)));
            Add(t += 1f, "poles", () => Shot("06_poles", new Vector3(0f, 1.8f, -2.5f), new Vector3(0f, 1f, -7f)));
            Add(t += 1f, "door", () => Shot("07_lounge_door", new Vector3(8.5f, 1.9f, -7.6f), new Vector3(14f, 1.1f, -6f)));
            Add(t += 1f, "lounge", () => Shot("08_lounge_inside", new Vector3(15.1f, 1.9f, -8.4f), new Vector3(19.4f, 0.4f, -5.6f)));
            Add(t += 1f, "ramp", () => Shot("09_ramp_water", new Vector3(-9.5f, 1.6f, 3f), new Vector3(-4f, -0.5f, 0f)));
            Add(t += 1f, "count", CountThings);

            // Bots: wait for them to arrive and spread out, then look at them.
            Add(t += 14f, "bots", () => Shot("10_bots_overview", new Vector3(-13f, 5.2f, 8f), new Vector3(2f, 0f, 0f)));
            Add(t += 0.6f, "walkable", () => Note(Swim.WalkableReport()));
            Add(t += 0.2f, "bot skeleton", () =>
            {
                UnitManager all = Game.Peek<UnitManager>();
                foreach (Unit unit in all.unitsDict.Values)
                {
                    if (unit != null && unit.isActived && unit.model != null && unit.GetComponent<StudentNPCComponent>() != null)
                    {
                        DumpSkeleton(unit, "skeleton_bot.txt");
                        break;
                    }
                }
            });
            Add(t += 1f, "count", CountThings);
            // Bots walk into the pool over the ramp's link by themselves: give them time to get there.
            Add(t += 10f, "bots in water", () => Note("bots in the water: " + Swim.BotsInWater() + " (" + Swim.TestState() + ")"));
            Add(t += 0.3f, "bots", () => Shot("10b_bots_in_pool", new Vector3(-10.5f, 3.2f, -6.5f), new Vector3(0f, -0.6f, 0f)));
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Add(t += 1.3f, "bot swimming", () => ShotOfBot("11_bot_swim_" + index, true, index));
            }
            Add(t += 1f, "bots in water", () => Note("bots in the water: " + Swim.BotsInWater()));
            Add(t += 1.3f, "bot on deck", () => ShotOfBot("12_bot_deck", false, 0));

            // This player in the water: seen through their own eyes (from behind) and from the side.
            Add(t += 1f, "into the pool", () => Put(me, new Vector3(1f, -0.8f, 0.8f), 90f));
            Add(t += 3f, "eyes", () => Screen("13_eyes_treading_water"));
            Add(t += 1f, "side", () => ShotOfUnit("14_me_treading_side", Game.LocalPlayer, 2.4f));
            Add(t += 0.5f, "stroke", () => SwimPose.ForceMoving = true);
            Add(t += 2f, "eyes", () => Screen("15_eyes_swimming"));
            Add(t += 1f, "side", () => ShotOfUnit("16_me_swimming_side", Game.LocalPlayer, 2.4f));
            Add(t += 0.7f, "side", () => ShotOfUnit("17_me_swimming_side2", Game.LocalPlayer, 2.4f));
            Add(t += 0.7f, "front", () => ShotOfUnit("18_me_swimming_front", Game.LocalPlayer, -2.4f));
            Add(t += 0.5f, "still", () => SwimPose.ForceMoving = false);
            Add(t += 0.5f, "state", () => DescribeMe("in the pool"));

            if (!me.IsTeacher)
            {
                // Things only a student does. They need the class to be running.
                Add(t += 1f, "wait for class", WaitForClass);
                // Peeing in the pool, the swim class's mischief, first: the DoorDash food pays
                // mischief too, and in a solo test the class is won (and over) soon after.
                Add(t += 0.5f, "state", () => DescribeMe("before peeing"));
                Add(t += 0.3f, "pee", Swim.TestPee);
                Add(t += 0.6f, "pee starts", () => Shot("26_pee_cloud_start", new Vector3(-2f, 2.4f, 4.2f), new Vector3(1f, -0.15f, 0.8f)));
                Add(t += 2.4f, "pee cloud", () => Shot("27_pee_cloud", new Vector3(-2f, 2.4f, 4.2f), new Vector3(1f, -0.15f, 0.8f)));
                Add(t += 0.4f, "eyes", () => Screen("28_eyes_pee"));
                Add(t += 0.3f, "state", () => DescribeMe("after peeing"));
                Add(t += 0.3f, "pee again", Swim.TestPee);
                Add(t += 0.5f, "state", () => DescribeMe("peeing again right away (should not pay)"));
                Add(t += 0.5f, "owner window", () =>
                {
                    if (HostPanel.Instance != null)
                    {
                        HostPanel.Instance.TestShow(true);
                    }
                });
                Add(t += 0.8f, "owner window", () => Screen("29_owner_window"));
                Add(t += 0.3f, "owner window", () =>
                {
                    if (HostPanel.Instance != null)
                    {
                        HostPanel.Instance.TestShow(false);
                    }
                });
                Add(t += 1f, "order", () =>
                {
                    DoorDash.Order(0);
                    Note("DoorDash ordered, state " + DoorDash.Current + (DoorDash.WhyNot() != null ? " (" + DoorDash.WhyNot() + ")" : ""));
                });
                // The food comes before anything that earns mischief: in a solo test the class
                // is won (and over) within seconds of the dive.
                Add(t += Plugin.Settings.doorDashWaitSeconds + 2f, "lounge", () => Shot("24_lounge_food", new Vector3(17.2f, 1.5f, -7.9f), new Vector3(18.75f, 0.45f, -6f)));
                Add(t += 1f, "delivery", () => Note("DoorDash state " + DoorDash.Current));
                Add(t += 1f, "into the lounge", () => Put(Game.LocalPlayer, new Vector3(16.2f, 0.08f, -6f), 90f));
                Add(t += 1.5f, "eyes", () => Screen("25_eyes_in_lounge"));
                Add(t += 0.5f, "back to the pool", () => Put(Game.LocalPlayer, new Vector3(1f, -0.8f, 0.8f), 90f));
                Add(t += 1.5f, "dive under", () =>
                {
                    StudentCrawlComponent crawl = Game.LocalPlayer.GetComponent<StudentCrawlComponent>();
                    if (crawl != null)
                    {
                        crawl.SetCrawlingFromInput(true);
                    }
                });
                Add(t += 2.5f, "eyes", () => Screen("19_eyes_under_water"));
                Add(t += 0.5f, "side", () => ShotOfUnit("20_me_under_water_side", Game.LocalPlayer, 2.4f));
                Add(t += 0.5f, "state", () => DescribeMe("under water"));
                Add(t += 0.5f, "come up", () =>
                {
                    StudentCrawlComponent crawl = Game.LocalPlayer.GetComponent<StudentCrawlComponent>();
                    if (crawl != null)
                    {
                        crawl.SetCrawlingFromInput(false);
                    }
                });
                // The diving board: stand on it, then step off its tip.
                Add(t += 1.5f, "onto the board", () => Put(Game.LocalPlayer, new Vector3(7.2f, 1.08f, 0f), 270f));
                Add(t += 1.2f, "eyes", () => Screen("21_eyes_on_board"));
                Add(t += 0.3f, "state", () => DescribeMe("on the board"));
                Add(t += 0.3f, "side", () => ShotOfUnit("22_me_on_board", Game.LocalPlayer, 3.2f));
                Add(t += 0.5f, "off the tip", () => Put(Game.LocalPlayer, new Vector3(5.2f, 1.2f, 0f), 270f));
                Add(t += 0.75f, "dive", () => Shot("23_dive_splash", new Vector3(3.5f, 1.6f, -3.6f), new Vector3(5.4f, -0.1f, 0f)));
                Add(t += 1f, "state", () => DescribeMe("after the dive"));
            }
            else
            {
                Add(t += 1f, "to the chair", () => Put(Game.LocalPlayer, new Vector3(0f, 0.08f, 5.2f), 0f));
                Add(t += 1.5f, "eyes", () => Screen("19_eyes_teacher_at_chair"));
                Add(t += 0.5f, "side", () => ShotOfUnit("20_teacher_on_deck", Game.LocalPlayer, 3f));
                Add(t += 0.5f, "state", () => DescribeMe("at the chair"));
            }
            Add(t += 1.5f, "overview", () => Shot("30_overview_end", new Vector3(-13f, 6f, -8f), new Vector3(2f, 0f, 0f)));
            Add(t += 1f, "count", CountThings);
            Add(t += 1.5f, "end", () => { });
        }

        /// <summary>The dance class's test: the hall, dancing bots, this player's dance, a partner and the snack table.</summary>
        private static void PlanDance(Unit me)
        {
            Steps.Clear();
            float t = 1f;
            // The hall from fixed camera spots (hall frame: x along the hall, z across).
            Add(t, "eyes", () => Screen("01_eyes_arrival"));
            Add(t += 1f, "overview", () => Shot("02_overview_from_far_end", new Vector3(11f, 4.6f, 7.5f), new Vector3(-3f, 0.5f, -1f)));
            Add(t += 1f, "overview", () => Shot("03_overview_from_stage", new Vector3(-10.5f, 4.2f, -7.5f), new Vector3(4f, 0.3f, 1.5f)));
            Add(t += 1f, "stage", () => Shot("04_stage_dj_booth", new Vector3(-5.5f, 2.2f, 2.5f), new Vector3(-10.8f, 1f, 0f)));
            Add(t += 1f, "snacks", () => Shot("05_snack_table", new Vector3(7.5f, 1.8f, 5.3f), new Vector3(7.2f, 0.5f, 8.1f)));
            Add(t += 1f, "ball", () => Shot("06_disco_ball_floor", new Vector3(-2f, 2.4f, -4f), new Vector3(2f, 3.6f, 0f)));

            // Bots: time to arrive, stop on the dance floor and dance.
            Add(t += 16f, "bots", () => Shot("10_bots_overview", new Vector3(11f, 4.6f, -7.5f), new Vector3(1f, 0.3f, 0.5f)));
            Add(t += 0.3f, "state", () => Note(Dance.TestState()));
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Add(t += 1.2f, "bot dancing", () => ShotOfDancingBot("11_bot_dance_" + index, index));
            }

            // This player on the floor: a student is made to dance by itself (the class dance);
            // the teacher dances with the class dance key. From the side, the front and the eyes.
            Add(t += 1f, "to the floor", () => Put(Game.LocalPlayer, new Vector3(4.5f, 0.05f, 2.5f), 90f));
            if (me.IsTeacher)
            {
                Add(t += 0.5f, "dance", () => Dance.TestDance(Dance.ClassDance));
            }
            Add(t += 1.5f, "side", () => ShotOfUnit("13_me_dancing_side", Game.LocalPlayer, 2.4f));
            Add(t += 0.35f, "side", () => ShotOfUnit("14_me_dancing_side2", Game.LocalPlayer, 2.4f));
            Add(t += 0.35f, "front", () => ShotOfUnit("15_me_dancing_front", Game.LocalPlayer, -2.4f));
            Add(t += 0.4f, "eyes", () => Screen("16_eyes_dancing"));
            Add(t += 0.3f, "state", () => DescribeMe("class dance (a student must dance)"));

            if (!me.IsTeacher)
            {
                // The worm: the naughty dance. Mischief needs the class running.
                Add(t += 0.5f, "worm", () => Dance.TestDance(Dance.Worm));
                Add(t += 1.5f, "side", () => ShotOfUnit("17_me_worm_side", Game.LocalPlayer, 2.4f));
                Add(t += 0.4f, "side", () => ShotOfUnit("18_me_worm_side2", Game.LocalPlayer, 2.4f));
                Add(t += 0.4f, "front", () => ShotOfUnit("19_me_worm_front", Game.LocalPlayer, -2.4f));
                Add(t += 0.4f, "eyes", () => Screen("20_eyes_worm"));
                Add(t += 0.3f, "state", () => DescribeMe("the worm, before class"));
                Add(t += 0.5f, "wait for class", WaitForClass);
                Add(t += 0.5f, "state", () => DescribeMe("the worm, class started"));
                Add(t += Plugin.Settings.danceSeconds * 2.5f, "worm", () => Note(Dance.TestState()));
                Add(t += 0.3f, "state", () => DescribeMe("after the worm (mischief should be up)"));
                Add(t += 0.5f, "class dance", () => Dance.TestDance(Dance.ClassDance));
                Add(t += 1f, "state", () => DescribeMe("back to the class dance"));
                Add(t += 0.5f, "order", () =>
                {
                    DoorDash.Order(0);
                    Note("DoorDash ordered, state " + DoorDash.Current + (DoorDash.WhyNot() != null ? " (" + DoorDash.WhyNot() + ")" : ""));
                });
                Add(t += Plugin.Settings.doorDashWaitSeconds + 2f, "snacks", () => Shot("24_snack_food", new Vector3(7.3f, 1.4f, 6.6f), new Vector3(7.7f, 0.55f, 8.05f)));
                Add(t += 1f, "delivery", () => Note("DoorDash state " + DoorDash.Current));
                Add(t += 0.5f, "owner window", () =>
                {
                    if (HostPanel.Instance != null)
                    {
                        HostPanel.Instance.TestShow(true);
                    }
                });
                Add(t += 0.8f, "owner window", () => Screen("29_owner_window"));
                Add(t += 0.3f, "owner window", () =>
                {
                    if (HostPanel.Instance != null)
                    {
                        HostPanel.Instance.TestShow(false);
                    }
                });
            }
            else
            {
                Add(t += 0.5f, "stop", () => Dance.TestDance(Dance.ClassDance));
                Add(t += 1f, "to the booth", () => Put(Game.LocalPlayer, new Vector3(-10.1f, 0.67f, 0.6f), 270f));
                Add(t += 1.5f, "eyes", () => Screen("19_eyes_teacher_at_booth"));
                Add(t += 0.5f, "state", () => DescribeMe("at the DJ booth"));
            }
            Add(t += 1.5f, "overview", () => Shot("30_overview_end", new Vector3(11f, 5f, 7.5f), new Vector3(-2f, 0f, -1f)));
            Add(t += 0.3f, "state", () => Note(Dance.TestState()));
            Add(t += 1.5f, "end", () => { });
        }

        private static void ShotOfDancingBot(string name, int skip)
        {
            Unit bot = Dance.DancingBot(skip);
            if (bot == null)
            {
                Note("picture " + name + " skipped: no bot dances right now");
                return;
            }
            _follow = bot;
            ShotOfUnit(name, bot, 2.2f);
            Note("picture " + name + ": bot " + bot.UID + " at " + (bot.Position - Hall).ToString("F1"));
        }

        /// <summary>
        /// Writes down every part of a character's body: its name, what it hangs under, and
        /// where it is and how it is turned, measured from the character's own place and facing.
        /// With this the swimming move can be worked out away from the game.
        /// </summary>
        private static void DumpSkeleton(Unit unit, string file)
        {
            if (unit == null || unit.model == null)
            {
                Note("skeleton: nobody to write down");
                return;
            }
            Transform root = unit.model.transform;
            Quaternion toBody = Quaternion.Inverse(Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f));
            Vector3 at = unit.Position;
            var lines = new List<string>
            {
                "# name | parent | depth | place from the character (x right, y up, z forward) | turn (x y z w) | local place | local turn | size",
                "# character at " + at.ToString("F3") + ", model root " + root.name + ", unit yaw " + unit.transform.eulerAngles.y.ToString("F1")
            };
            var skinned = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                lines.Add("# skin " + skin.name + " active " + skin.gameObject.activeInHierarchy + " bones " + skin.bones.Length
                    + " root bone " + (skin.rootBone != null ? skin.rootBone.name : "none"));
                foreach (Transform bone in skin.bones)
                {
                    if (bone != null)
                    {
                        skinned.Add(bone);
                    }
                }
            }
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            {
                int depth = 0;
                for (Transform up = part; up != null && up != root; up = up.parent)
                {
                    depth++;
                }
                Vector3 place = toBody * (part.position - at);
                Quaternion turn = toBody * part.rotation;
                lines.Add(string.Join("|", new[]
                {
                    part.name, part.parent != null && part != root ? part.parent.name : "-", depth.ToString(),
                    place.x.ToString("F4") + " " + place.y.ToString("F4") + " " + place.z.ToString("F4"),
                    turn.x.ToString("F5") + " " + turn.y.ToString("F5") + " " + turn.z.ToString("F5") + " " + turn.w.ToString("F5"),
                    part.localPosition.x.ToString("F4") + " " + part.localPosition.y.ToString("F4") + " " + part.localPosition.z.ToString("F4"),
                    part.localRotation.x.ToString("F5") + " " + part.localRotation.y.ToString("F5") + " " + part.localRotation.z.ToString("F5") + " " + part.localRotation.w.ToString("F5"),
                    part.lossyScale.x.ToString("F3") + (skinned.Contains(part) ? " bone" : "")
                }));
            }
            File.WriteAllLines(Path.Combine(_folder, file), lines.ToArray());
            Note("skeleton written: " + file + ", " + (lines.Count - 2) + " lines");
        }

        private static void WaitForClass()
        {
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            if (classroom != null && classroom.curState != EClassroomState.Classing && Time.realtimeSinceStartup - _stepsFrom < 200f)
            {
                // Not yet: come back to this same step, and push the later ones along with it.
                _nextStep--;
                float wait = 1f;
                for (int i = _nextStep; i < Steps.Count; i++)
                {
                    Steps[i].At += wait;
                }
                return;
            }
            Note("class state " + (classroom != null ? classroom.curState.ToString() : "none"));
        }

        private static void Put(Unit unit, Vector3 inHall, float yaw)
        {
            RoomManager room = Game.Peek<RoomManager>();
            if (unit == null || room == null)
            {
                return;
            }
            var message = new UnitTeleportPositionMessageResponse_S2C
            {
                unitUID = unit.UID,
                position = Hall + inHall,
                rotation = new Vector3(0f, yaw, 0f),
                floor = 0
            };
            room.GetUnitMessageHandler().OnUnitTeleportPositionMessageResponse(message);
            room.BroadcastToAllExceptHost(message);
        }

        private static void DescribeMe(string when)
        {
            Unit me = Game.LocalPlayer;
            if (me == null)
            {
                Note(when + ": no player");
                return;
            }
            InputComponent input = me.GetComponent<InputComponent>();
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            Note(when + ": at " + (me.Position - Hall).ToString("F2") + ", swimming " + Swim.Swimming(me) + ", dancing " + Dance.IsDancing(me)
                + ", seen from behind " + SelfView.Showing + ", speed factor " + (input != null ? input.speedFac.ToString("F2") : "?")
                + ", state " + me.CurState() + ", mischief " + (classroom != null ? classroom.currentTrickCount.ToString("F0") : "?"));
        }

        private static void CountThings()
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            int bots = 0, inHall = 0, swimming = 0, poles = 0, food = 0;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.transform == null)
                {
                    continue;
                }
                bool near = (unit.Position - Hall).magnitude < 45f;
                if (unit.GetComponent<StudentNPCComponent>() != null)
                {
                    bots++;
                    if (near)
                    {
                        inHall++;
                    }
                    if (Swim.Swimming(unit))
                    {
                        swimming++;
                    }
                }
                else if (near && unit.GetComponent<PunishPillarComponent>() != null)
                {
                    poles++;
                }
                else if (near && DoorDash.IsDelivery(unit.UID))
                {
                    food++;
                    Note("DoorDash item at " + (unit.Position - Hall).ToString("F2") + " (hall frame)");
                }
            }
            Note("bots " + bots + ", in the hall " + inHall + ", swimming " + swimming + ", poles " + poles + ", food " + food + "; " + Swim.TestState());
        }

        // ------------------------------------------------------------------------- pictures

        /// <summary>The picture the player would see on their screen, with the mod's text on it.</summary>
        private static void Screen(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            Note("picture " + name + " (the player's own view)");
        }

        private static void Shot(string name, Vector3 fromInHall, Vector3 atInHall)
        {
            ShotWorld(name, Hall + fromInHall, Hall + atInHall);
        }

        private static void ShotWorld(string name, Vector3 from, Vector3 at)
        {
            if (_camera == null)
            {
                var holder = new GameObject("[ClassPack test camera]");
                UnityEngine.Object.DontDestroyOnLoad(holder);
                _camera = holder.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.fieldOfView = 62f;
                _camera.nearClipPlane = 0.08f;
                _camera.farClipPlane = 150f;
                _target = new RenderTexture(1152, 648, 24, RenderTextureFormat.ARGB32);
                _camera.targetTexture = _target;
            }
            Camera main = Camera.main;
            if (main != null)
            {
                _camera.clearFlags = main.clearFlags;
                _camera.backgroundColor = main.backgroundColor;
                int mask = main.cullingMask;
                foreach (string layer in new[] { "PlayerSelf", "Player", "Default", "Ground" })
                {
                    int index = LayerMask.NameToLayer(layer);
                    if (index >= 0)
                    {
                        mask |= 1 << index;
                    }
                }
                _camera.cullingMask = mask;
            }
            _camera.transform.position = from;
            _camera.transform.rotation = Quaternion.LookRotation((at - from).normalized, Vector3.up);
            _camera.enabled = true;
            _shotName = name;
            _shotFrames = 0;
        }

        private static void ShotOfUnit(string name, Unit unit, float sideways)
        {
            if (unit == null || unit.transform == null)
            {
                Note("picture " + name + " skipped: nobody to look at");
                return;
            }
            Vector3 at = unit.Position;
            at.y = _dance ? at.y + 0.5f : Mathf.Max(at.y + 0.5f, Swim.WaterWorldY + 0.1f);
            Vector3 right = unit.transform.right;
            Vector3 from = sideways >= 0f
                ? at + right * sideways + Vector3.up * 0.7f
                : at + unit.transform.forward * -sideways + Vector3.up * 0.5f;
            ShotWorld(name, from, at);
        }

        private static void ShotOfBot(string name, bool swimming, int skip)
        {
            UnitManager units = Game.Peek<UnitManager>();
            Unit found = null;
            if (units != null)
            {
                foreach (Unit unit in units.unitsDict.Values)
                {
                    if (unit == null || !unit.isActived || unit.transform == null || unit.GetComponent<StudentNPCComponent>() == null
                        || (unit.Position - Hall).magnitude > 45f || Swim.Swimming(unit) != swimming)
                    {
                        continue;
                    }
                    // The camera stands beside the bot: a bot right by a wall would put it inside the wall.
                    Vector3 inHall = unit.Position - Hall;
                    if (!swimming && (Mathf.Abs(inHall.x) > 11f || Mathf.Abs(inHall.z) > 6.5f))
                    {
                        continue;
                    }
                    found = unit;
                    if (skip-- <= 0)
                    {
                        break;
                    }
                }
            }
            if (found == null)
            {
                Note("picture " + name + " skipped: no bot " + (swimming ? "in the water" : "on the deck"));
                return;
            }
            _follow = found;
            ShotOfUnit(name, found, 2.2f);
            Note("picture " + name + ": bot " + found.UID + " at " + (found.Position - Hall).ToString("F1"));
        }

        private static void TickShot()
        {
            if (_shotName == null || _camera == null)
            {
                return;
            }
            _shotFrames++;
            if (_shotFrames < 4)
            {
                return;
            }
            try
            {
                RenderTexture before = RenderTexture.active;
                RenderTexture.active = _target;
                var picture = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, _target.width, _target.height), 0, 0);
                picture.Apply();
                RenderTexture.active = before;
                File.WriteAllBytes(Path.Combine(_folder, _shotName + ".jpg"), picture.EncodeToJPG(88));
                UnityEngine.Object.Destroy(picture);
            }
            catch (Exception ex)
            {
                Note("picture " + _shotName + " failed: " + ex.Message);
            }
            _camera.enabled = false;
            _shotName = null;
            _follow = null;
        }
    }
}
