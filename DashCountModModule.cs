﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModModule : EverestModule {

        public static DashCountModModule Instance;

        private static FieldInfo speedBerryPBInChapterPanel;

        public override Type SettingsType => typeof(DashCountModSettings);
        public override Type SaveDataType => typeof(DashCountModSaveData);

        public DashCountModModule() {
            Instance = this;
            Logger.SetLogLevel("DashCountMod", LogLevel.Info);
        }

        public override void Load() {
            // mod methods here
            Everest.Events.Journal.OnEnter += OnJournalEnter;
            On.Celeste.OuiChapterPanel.ctor += ModOuiChapterPanelConstructor;
            IL.Celeste.Player.CallDashEvents += CountDashes;
            On.Celeste.Session.ctor_AreaKey_string_AreaStats += SaveOldDashCount;
        }

        public override void Unload() {
            // unmod methods here
            Everest.Events.Journal.OnEnter -= OnJournalEnter;
            On.Celeste.OuiChapterPanel.ctor -= ModOuiChapterPanelConstructor;
            IL.Celeste.Player.CallDashEvents -= CountDashes;
            On.Celeste.Session.ctor_AreaKey_string_AreaStats -= SaveOldDashCount;
        }

        public override void Initialize() {
            base.Initialize();

            // is SpeedBerryPBInChapterPanel a thing?
            EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");
            if (collabUtils != null) {
                speedBerryPBInChapterPanel = collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.SpeedBerryPBInChapterPanel")
                    .GetField("speedBerryPBDisplay", BindingFlags.NonPublic | BindingFlags.Static);

                Logger.Log("DashCountMod", $"I found the speed berry PB component: {speedBerryPBInChapterPanel.Name} " +
                    $"(type {speedBerryPBInChapterPanel.DeclaringType} in {speedBerryPBInChapterPanel.DeclaringType.Assembly})");

                // be sure other mods are hooked (they might not have been loaded when the dash count mod was loaded).

                if ((_Settings as DashCountModSettings).DashCountInChapterPanel != DashCountOptions.None) {
                    hookCollabUtils();
                }
                if ((_Settings as DashCountModSettings).DashCountOnProgressPage != DashCountOptions.None) {
                    hookCollabUtilsJournalPage();
                }
                if ((_Settings as DashCountModSettings).CountDreamDashRedirectsAsDashes) {
                    hookPandorasBox();
                }
            }
        }

        // ================ Custom Dash Counting ================

        private void CountDashes(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            if (cursor.TryGotoNext(instr => instr.MatchLdfld<SaveData>("TotalDashes"))) {
                // this is the place where vanilla increments the TotalDashes count in the save file: increment our own dash count as well.
                Logger.Log("DashCountMod", $"Adding code to count dashes at {cursor.Index} in IL for Player.CallDashEvents()");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<Player>>(AddDash);
            }
        }

        private static void AddDash(Entity entityInScene) {
            if (entityInScene.Scene != null) {
                AreaKey area = entityInScene.SceneAs<Level>().Session.Area;

                if ((Instance._SaveData as DashCountModSaveData).DashCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> dashCounts)) {
                    if (dashCounts.TryGetValue(area.Mode, out int currentDashCount)) {
                        // area and mode stats exist, we should increment it
                        dashCounts[area.Mode]++;
                    } else {
                        // area stats exist, mode stats don't
                        dashCounts[area.Mode] = 1;
                    }
                } else {
                    // area stats don't exist, create them
                    Dictionary<AreaMode, int> areaStats = new Dictionary<AreaMode, int>();
                    areaStats[area.Mode] = 1;
                    (Instance._SaveData as DashCountModSaveData).DashCountPerLevel[area.GetSID()] = areaStats;
                }
            }
        }

        private void SaveOldDashCount(On.Celeste.Session.orig_ctor_AreaKey_string_AreaStats orig, Session self, AreaKey area, string checkpoint, AreaStats oldStats) {
            orig(self, area, checkpoint, oldStats);

            if (oldStats == null) {
                int oldDashCount = 0;

                if ((_SaveData as DashCountModSaveData).DashCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                    if (areaModes.TryGetValue(area.Mode, out int totalDashes)) {
                        oldDashCount = totalDashes;
                    }
                }

                (_SaveData as DashCountModSaveData).OldDashCount = oldDashCount;
            }
        }

        // ================ Count Dream Dash Redirects As Dashes ================

        private Hook pandorasBoxHook = null;

        public void SetCountDreamDashRedirectsAsDashes(bool enabled) {
            if (enabled && pandorasBoxHook == null) {
                Logger.Log("DashCountMod", "Hooking Pandora's Box dream dash redirects");
                hookPandorasBox();

            } else if (!enabled && pandorasBoxHook != null) {
                Logger.Log("DashCountMod", "Unhooking Pandora's Box dream dash redirects");

                pandorasBoxHook?.Dispose();
                pandorasBoxHook = null;
            }
        }

        private void hookPandorasBox() {
            if (pandorasBoxHook == null) {
                // is Pandora's Box a thing?
                EverestModule pandorasBox = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "PandorasBox");

                if (pandorasBox != null) {
                    pandorasBoxHook = new Hook(
                        pandorasBox.GetType().Assembly.GetType("Celeste.Mod.PandorasBox.DreamDashController").GetMethod("dreamDashRedirect", BindingFlags.NonPublic | BindingFlags.Instance),
                        typeof(DashCountModModule).GetMethod("countPandorasBoxDashes", BindingFlags.NonPublic | BindingFlags.Static));

                    Logger.Log("DashCountMod", "Pandora's Box Dream Dash Controller hooked");
                }
            }
        }

        private static bool countPandorasBoxDashes(Func<Entity, Player, bool> orig, Entity self, Player player) {
            bool didRedirect = orig(self, player);

            if (didRedirect) {
                SaveData.Instance.TotalDashes++;
                self.SceneAs<Level>().Session.Dashes++;
                Stats.Increment(Stat.DASHES);
                AddDash(self);
            }

            return didRedirect;
        }

        // ================ Do Not Reset Dash Count On Death ================

        private bool dashCountOnDeathHooked = false;

        public void SetDoNotResetDashCountOnDeath(bool doNotResetDashCountOnDeath) {
            if (doNotResetDashCountOnDeath && !dashCountOnDeathHooked) {
                On.Celeste.Player.CallDashEvents += onCallDashEvents;
                dashCountOnDeathHooked = true;
            } else if (!doNotResetDashCountOnDeath && dashCountOnDeathHooked) {
                On.Celeste.Player.CallDashEvents -= onCallDashEvents;
                dashCountOnDeathHooked = false;
            }
        }

        private void onCallDashEvents(On.Celeste.Player.orig_CallDashEvents orig, Player self) {
            orig(self);
            (Engine.Scene as Level)?.Session.UpdateLevelStartDashes();
        }


        // ================ Display Dash Count In Level ================

        ShowDashCountInGameOptions showDashCountInGame = ShowDashCountInGameOptions.None;

        public void SetShowDashCountInGame(ShowDashCountInGameOptions value) {
            bool wasEnabled = (showDashCountInGame != ShowDashCountInGameOptions.None);
            bool isEnabled = (value != ShowDashCountInGameOptions.None);

            // (un)hook methods
            if (isEnabled && !wasEnabled) {
                Logger.Log("DashCountMod", "Hooking level enter to add in-game dash counter");
                On.Celeste.Level.Begin += OnLevelBegin;

            } else if (!isEnabled && wasEnabled) {
                Logger.Log("DashCountMod", "Unhooking level enter to stop adding in-game dash counter");
                On.Celeste.Level.Begin -= OnLevelBegin;
            }

            // add/remove/update the dash count accordingly if we already are in a level.
            if (Engine.Scene is Level level) {
                DashCountDisplayInLevel currentDisplay = level.Entities.FindFirst<DashCountDisplayInLevel>();

                if (value == ShowDashCountInGameOptions.None) {
                    currentDisplay?.RemoveSelf();
                } else if (currentDisplay != null) {
                    currentDisplay.SetFormat(value);
                } else {
                    level.Add(new DashCountDisplayInLevel(level.Session, value));
                }
            }

            showDashCountInGame = value;
        }

        private void OnLevelBegin(On.Celeste.Level.orig_Begin orig, Level self) {
            orig(self);
            self.Add(new DashCountDisplayInLevel(self.Session, showDashCountInGame));
        }

        // ================ Journal Page ================

        private void OnJournalEnter(OuiJournal journal, Oui from) {
            // add the "dashes" page just after the "deaths" one
            for (int i = 0; i < journal.Pages.Count; i++) {
                if (journal.Pages[i].GetType() == typeof(OuiJournalDeaths)) {
                    journal.Pages.Insert(i + 1, new OuiJournalDashes(journal));
                }
            }
        }

        // ================ Dash Count in Progress Page ================

        static DashCountOptions fewestDashesInProgressPage = DashCountOptions.None;

        private static Hook collabUtilsJournalsHook = null;
        private static Hook collabUtilsJournalsTotalDashesHook = null;
        private static Hook collabUtilsJournalsLevelDashesHook = null;
        private static ILHook collabUtilsOverworldJournalSizeHook = null;
        private static ILHook collabUtilsLobbyJournalSizeHook = null;

        public void SetFewestDashesInProgressPage(DashCountOptions newValue) {
            bool wasEnabled = (fewestDashesInProgressPage != DashCountOptions.None);
            bool isEnabled = (newValue != DashCountOptions.None);

            // (un)hook methods
            if (isEnabled && !wasEnabled) {
                Logger.Log("DashCountMod", "Hooking journal progress page rendering methods");

                IL.Celeste.OuiJournalProgress.ctor += ModOuiJournalProgressColumnSizes;
                IL.Celeste.OuiJournalProgress.ctor += ModOuiJournalProgressConstructor;
                hookCollabUtilsJournalPage();
            } else if (!isEnabled && wasEnabled) {
                Logger.Log("DashCountMod", "Unhooking journal progress page rendering methods");

                IL.Celeste.OuiJournalProgress.ctor -= ModOuiJournalProgressColumnSizes;
                IL.Celeste.OuiJournalProgress.ctor -= ModOuiJournalProgressConstructor;

                collabUtilsJournalsHook?.Dispose();
                collabUtilsJournalsHook = null;

                collabUtilsJournalsTotalDashesHook?.Dispose();
                collabUtilsJournalsTotalDashesHook = null;

                collabUtilsJournalsLevelDashesHook?.Dispose();
                collabUtilsJournalsLevelDashesHook = null;

                collabUtilsOverworldJournalSizeHook?.Dispose();
                collabUtilsOverworldJournalSizeHook = null;

                collabUtilsLobbyJournalSizeHook?.Dispose();
                collabUtilsLobbyJournalSizeHook = null;
            }

            fewestDashesInProgressPage = newValue;
        }

        private static void hookCollabUtilsJournalPage() {
            if (collabUtilsJournalsHook == null) {
                // is OuiJournalCollabProgressDashCountMod a thing?
                EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");

                if (collabUtils != null) {
                    collabUtilsJournalsHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressDashCountMod").GetMethod("IsDashCountEnabled", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountModModule).GetMethod("enableDashCountModInCollabUtils", BindingFlags.NonPublic | BindingFlags.Static));

                    collabUtilsJournalsTotalDashesHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressDashCountMod").GetMethod("DisplaysTotalDashes", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountModModule).GetMethod("displaysTotalDashesInCollabUtils", BindingFlags.NonPublic | BindingFlags.Static));

                    collabUtilsJournalsLevelDashesHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressDashCountMod").GetMethod("GetLevelDashesForJournalProgress", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountModModule).GetMethod("getLevelDashesInJournalProgressInCollabUtils", BindingFlags.NonPublic | BindingFlags.Static));

                    collabUtilsOverworldJournalSizeHook = new ILHook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressInOverworld").GetConstructor(new Type[] { typeof(OuiJournal) }),
                        ModOuiJournalProgressColumnSizes);

                    collabUtilsLobbyJournalSizeHook = new ILHook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressInLobby").GetConstructor(new Type[] { typeof(OuiJournal), typeof(string), typeof(bool) }),
                        ModOuiJournalProgressColumnSizes);

                    Logger.Log("DashCountMod", "Collab utils dash count column in journals were enabled");
                }
            }
        }

        private static bool enableDashCountModInCollabUtils(Func<bool> orig) {
            return true;
        }

        private static bool displaysTotalDashesInCollabUtils(Func<bool> orig) {
            return (fewestDashesInProgressPage == DashCountOptions.Total);
        }

        private static int getLevelDashesInJournalProgressInCollabUtils(Func<AreaStats, int> orig, AreaStats stats) {
            return GetTotalDashesForJournalProgress(stats);
        }

        private static void ModOuiJournalProgressColumnSizes(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // patch columns to be narrower (100 => 80, 150 => 120, 20 => 0)
            while (cursor.TryGotoNext(instr => instr.MatchLdcR4(100f) || instr.MatchLdcR4(150f) || instr.MatchLdcR4(20f))) {
                float currentValue = (float) cursor.Next.Operand;
                float newValue = (currentValue == 100f ? 80f : (currentValue == 150f ? 120f : 0f));
                Logger.Log("DashCountMod", $"Modding column size from {currentValue} to {newValue} at {cursor.Index} in CIL code for {il.Method.FullName}");
                cursor.Next.Operand = newValue;
            }
        }

        private void ModOuiJournalProgressConstructor(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // add a column header for dash counts, just before the "time" column
            if (cursor.TryGotoNext(instr => instr.MatchLdstr("time"))) {
                Logger.Log("DashCountMod", $"Adding column header for fewest dashes at {cursor.Index} in CIL code for OuiJournalProgress constructor");

                // At this point, we loaded this.table into the stack. Just use it.
                cursor.EmitDelegate<Action<OuiJournalPage.Table>>(AddColumnHeaderForFewestDashes);

                // Then inject this.table back, so that the game can add the time column.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiJournalProgress).GetField("table", BindingFlags.NonPublic | BindingFlags.Instance));
            }

            // and actually add cells on each line, once again just before the "total time" column
            if (cursor.TryGotoNext(instr => instr.MatchCallvirt<AreaStats>("get_TotalTimePlayed"))) {
                // at this point, we loaded AreaStats into the stack. (the instruction is ldloc.1 on XNA, ldloc.3 on FNA)
                OpCode loadOpCodeForAreaStats = cursor.Prev.OpCode;

                // let's load the row too. we just happen to know it's local variable 8 on XNA and 17 on FNA.
                if (cursor.TryFindNext(out ILCursor[] nextLocalVariableCursor, instr => instr.OpCode == OpCodes.Ldloc_S
                     && (((VariableDefinition) instr.Operand).Index == 8 || ((VariableDefinition) instr.Operand).Index == 17))) {

                    Logger.Log("DashCountMod", $"Adding column value for fewest dashes at {cursor.Index} in CIL code for OuiJournalProgress constructor: " +
                        $"loading area stats with {loadOpCodeForAreaStats} and current row with ldloc.s {nextLocalVariableCursor[0].Next.Operand}");

                    // load row and this into the stack, then call our delegate which will build and add the cell.
                    cursor.Emit(OpCodes.Ldloc_S, nextLocalVariableCursor[0].Next.Operand);
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate<Action<AreaStats, OuiJournalPage.Row, OuiJournalProgress>>(AddColumnValueForFewestDashes);

                    // then, put back AreaStats in the stack for the Time cell to use it.
                    cursor.Emit(loadOpCodeForAreaStats);
                }
            }

            // finally, inject ourselves in the Totals line, just before Time
            if (cursor.TryGotoNext(instr => instr.MatchLdfld<SaveData>("Time"))) {
                // step back before loading SaveData.Instance. The instruction before that loads the row in the stack
                cursor.Index--;
                object varIndexForRow = cursor.Prev.Operand;

                Logger.Log("DashCountMod", $"Adding column total for fewest dashes at {cursor.Index} in CIL code for OuiJournalProgress constructor: loading row with {varIndexForRow}");

                // at this point, we have row in the stack. Add this, then call our delegate which will build and add the cell.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<OuiJournalPage.Row, OuiJournalProgress>>(AddColumnTotalForFewestDashes);

                // Then inject row back, so that the game can add the time column.
                cursor.Emit(OpCodes.Ldloc_S, varIndexForRow);
            }
        }

        private void AddColumnHeaderForFewestDashes(OuiJournalPage.Table table) {
            table.AddColumn(new OuiJournalPage.IconCell("max480/DashCountMod/dashes", 80f));
        }

        private static int GetTotalDashesForJournalProgress(AreaStats stats) {
            if (fewestDashesInProgressPage == DashCountOptions.Fewest) {
                return stats.BestTotalDashes;
            }

            int count = 0;
            if ((Instance._SaveData as DashCountModSaveData).DashCountPerLevel.TryGetValue(stats.GetSID(), out Dictionary<AreaMode, int> result)) {
                foreach (int value in result.Values) {
                    count += value;
                }
            }
            return count;
        }

        private void AddColumnValueForFewestDashes(AreaStats areaStats, OuiJournalPage.Row row, OuiJournalProgress self) {
            if (fewestDashesInProgressPage == DashCountOptions.Total && areaStats.TotalTimePlayed > 0) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(GetTotalDashesForJournalProgress(areaStats)), self.TextJustify, 0.5f, self.TextColor));
                return;
            }

            // we only show values for SingleRunCompleted sides. so, the total only appears for chapters with only SingleRunCompleted sides
            bool allSingleRunCompleted = true;
            int mode = 0;
            foreach (AreaModeStats modeStats in areaStats.Modes ?? new AreaModeStats[0]) {
                if (AreaData.Areas[areaStats.ID].HasMode((AreaMode) mode++) && !modeStats.SingleRunCompleted) {
                    allSingleRunCompleted = false;
                }
            }

            if (allSingleRunCompleted) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(GetTotalDashesForJournalProgress(areaStats)), self.TextJustify, 0.5f, self.TextColor));
            } else {
                row.Add(new OuiJournalPage.IconCell("dot"));
            }
        }

        private void AddColumnTotalForFewestDashes(OuiJournalPage.Row row, OuiJournalProgress self) {
            if (fewestDashesInProgressPage == DashCountOptions.Total) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(SaveData.Instance.TotalDashes), self.TextJustify, 0.6f, self.TextColor));
                return;
            }

            // go across all areas that are not interludes, and check if they are all completed in a single run
            bool allSingleRunCompleted = true;
            int totalDashes = 0;
            foreach (AreaStats areaStats in SaveData.Instance.Areas) {
                if (!AreaData.Areas[areaStats.ID].Interlude) {
                    int mode = 0;
                    foreach (AreaModeStats modeStats in areaStats.Modes ?? new AreaModeStats[0]) {
                        if (AreaData.Areas[areaStats.ID].HasMode((AreaMode) mode++) && !modeStats.SingleRunCompleted) {
                            allSingleRunCompleted = false;
                        }
                    }
                    if (allSingleRunCompleted) totalDashes += GetTotalDashesForJournalProgress(areaStats);
                }
            }

            if (allSingleRunCompleted) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(totalDashes), self.TextJustify, 0.6f, self.TextColor));
            } else {
                row.Add(new OuiJournalPage.IconCell("dot"));
            }
        }

        // ================ Dash Count in Chapter Panel ================

        DashCountOptions dashCounterInChapterPanel = DashCountOptions.None;
        private static Hook collabUtilsHook = null;

        public void SetDashCounterInChapterPanel(DashCountOptions newValue) {
            bool wasEnabled = (dashCounterInChapterPanel != DashCountOptions.None);
            bool isEnabled = (newValue != DashCountOptions.None);

            // (un)hook methods
            if (isEnabled && !wasEnabled) {
                Logger.Log("DashCountMod", "Hooking chapter panel rendering methods");

                using (new DetourContext() { After = { "*" } }) { // be sure to apply _after_ the collab utils.
                    IL.Celeste.OuiChapterPanel.Render += ModOuiChapterPanelRender;
                    On.Celeste.OuiChapterPanel.UpdateStats += ModOuiChapterPanelUpdateStats;
                    IL.Celeste.OuiChapterPanel.SetStatsPosition += ModOuiChapterPanelSetStatsPosition;
                    On.Celeste.OuiChapterPanel.IncrementStatsDisplay += ModOuiChapterPanelIncrementStatsDisplay;
                    On.Celeste.OuiChapterPanel.GetModeHeight += ModOuiChapterPanelGetModeHeight;
                }

                hookCollabUtils();
            } else if (!isEnabled && wasEnabled) {
                Logger.Log("DashCountMod", "Unhooking chapter panel rendering methods");

                IL.Celeste.OuiChapterPanel.Render -= ModOuiChapterPanelRender;
                On.Celeste.OuiChapterPanel.UpdateStats -= ModOuiChapterPanelUpdateStats;
                IL.Celeste.OuiChapterPanel.SetStatsPosition -= ModOuiChapterPanelSetStatsPosition;
                On.Celeste.OuiChapterPanel.IncrementStatsDisplay -= ModOuiChapterPanelIncrementStatsDisplay;
                On.Celeste.OuiChapterPanel.GetModeHeight -= ModOuiChapterPanelGetModeHeight;

                collabUtilsHook?.Dispose();
                collabUtilsHook = null;

                // hide the dash counter if currently shown: as we unhooked everything updating it, it will stay invisible.
                dashesCounter.Visible = false;
            }

            dashCounterInChapterPanel = newValue;
        }

        private static void hookCollabUtils() {
            if (collabUtilsHook == null) {
                // is SpeedBerryPBInChapterPanel a thing?
                EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");

                if (collabUtils != null) {
                    collabUtilsHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.SpeedBerryPBInChapterPanel").GetMethod("approach", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountModModule).GetMethod("modSpeedBerryPBApproach", BindingFlags.NonPublic | BindingFlags.Static));

                    Logger.Log("DashCountMod", "Collab utils speed berry PB counter was hooked");
                }
            }
        }

        private delegate Vector2 orig_approach(Vector2 from, Vector2 to, bool snap);
        private static Vector2 modSpeedBerryPBApproach(orig_approach orig, Vector2 from, Vector2 to, bool snap) {
            if ((Instance.dashesCounter?.Visible ?? false) && Instance.dashesOffset.Y != 160f) {
                to.Y -= 40f;
            }

            return orig(from, to, snap);
        }

        private DashesCounterInChapterPanel dashesCounter;
        private Vector2 dashesOffset;

        private void ModOuiChapterPanelConstructor(On.Celeste.OuiChapterPanel.orig_ctor orig, OuiChapterPanel self) {
            orig(self);

            // add the dashes counter as well, but have it hidden by default
            self.Add(dashesCounter = new DashesCounterInChapterPanel(true, 0));
            dashesCounter.CanWiggle = false;
            dashesCounter.Visible = false;
        }

        private void ModOuiChapterPanelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // move after the deaths counter positioning, and place ourselves after that to update dashes counter position as well
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(DeathsCounter), "Position"))) {
                Logger.Log("DashCountMod", $"Injecting dashes counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("contentOffset", BindingFlags.NonPublic | BindingFlags.Instance));
                cursor.EmitDelegate<Action<Vector2>>(UpdateDashesCounterRenderedPosition);
            }
        }

        private void UpdateDashesCounterRenderedPosition(Vector2 contentOffset) {
            dashesCounter.Position = contentOffset + new Vector2(0f, 170f) + dashesOffset;
        }

        private int GetDashCountForChapterPanel(AreaModeStats areaModeStats, AreaKey areaKey) {
            if (dashCounterInChapterPanel == DashCountOptions.Fewest) {
                return areaModeStats.BestDashes;
            }

            if ((_SaveData as DashCountModSaveData).DashCountPerLevel.TryGetValue(areaKey.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                if (areaModes.TryGetValue(areaKey.Mode, out int totalDashes)) {
                    return totalDashes;
                }
            }

            return 0;
        }

        private void ModOuiChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {
            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            dashesCounter.Visible = self.DisplayedStats.Modes[(int) self.Area.Mode].SingleRunCompleted && !AreaData.Get(self.Area).Interlude;
            dashesCounter.Amount = GetDashCountForChapterPanel(self.DisplayedStats.Modes[(int) self.Area.Mode], self.Area);

            if (dashCounterInChapterPanel == DashCountOptions.Total && self.DisplayedStats != self.RealStats) {
                // this is a sign that we are returning from a level, and we should display the old dash count so that it can animate to the new dash count.
                dashesCounter.Amount = (_SaveData as DashCountModSaveData).OldDashCount;
            }

            if (wiggle && dashesCounter.Visible && (overrideDeathWiggle ?? true)) {
                dashesCounter.Wiggle();
            }
        }

        private void ModOuiChapterPanelSetStatsPosition(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // this is a tricky one... in lines like this:
            // this.strawberriesOffset = this.Approach(this.strawberriesOffset, new Vector2(120f, (float)(this.deaths.Visible ? -40 : 0)), !approach);
            // we want to catch the result of (float)(this.deaths.Visible ? -40 : 0) and transform it to shift the things up if the dashes counter is there.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchConvR4())) {
                Logger.Log("DashCountMod", $"Modifying strawberry/death counter positioning at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<float, float>>(ShiftCountersPosition);
            }

            cursor.Index = 0;

            // we will cross 2 occurrences when deathsOffset will be set: first time with the heart, second time without.
            // the only difference is the X offset, so put the code in common.
            bool hasHeart = true;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(OuiChapterPanel), "deathsOffset"))) {
                Logger.Log("DashCountMod", $"Injecting dashes counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition (has heart = {hasHeart})");

                // bool approach
                cursor.Emit(OpCodes.Ldarg_1);
                // StrawberriesCounter strawberries
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("strawberries", BindingFlags.NonPublic | BindingFlags.Instance));
                // DeathsCounter deaths
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("deaths", BindingFlags.NonPublic | BindingFlags.Instance));
                // bool hasHeart
                cursor.Emit(hasHeart ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                // function call
                cursor.EmitDelegate<Action<bool, StrawberriesCounter, DeathsCounter, bool>>(UpdateDashesCounterOffset);

                hasHeart = false;
            }

            cursor.Index = 0;

            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdfld<OuiChapterPanel>("deaths"),
                instr => instr.MatchLdfld<Component>("Visible"))) {

                Logger.Log("DashCountMod", $"Patching chapter panel columns count at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<bool, bool>>(orig => orig || (dashesCounter?.Visible ?? false));
            }
        }

        private float ShiftCountersPosition(float position) {
            return dashesCounter.Visible && dashesOffset.Y != 160f ? position - 40 : position;
        }

        private void UpdateDashesCounterOffset(bool approach, StrawberriesCounter strawberries, DeathsCounter deaths, bool hasHeart) {
            int shift = 0;
            if (strawberries.Visible) shift += 40;
            if (deaths.Visible) shift += 40;
            if (speedBerryPBInChapterPanel != null && speedBerryPBInChapterPanel.GetValue(null) is Component component && component.Visible) shift += 40;
            if (shift == 120f) shift += 40;
            dashesOffset = Approach(dashesOffset, new Vector2(hasHeart ? 120f : 0f, shift), !approach);
        }

        // vanilla method copypaste
        private Vector2 Approach(Vector2 from, Vector2 to, bool snap) {
            if (snap) return to;
            return from += (to - from) * (1f - (float) Math.Pow(0.0010000000474974513, Engine.DeltaTime));
        }

        private IEnumerator ModOuiChapterPanelIncrementStatsDisplay(On.Celeste.OuiChapterPanel.orig_IncrementStatsDisplay orig, OuiChapterPanel self, AreaModeStats modeStats,
            AreaModeStats newModeStats, bool doHeartGem, bool doStrawberries, bool doDeaths, bool doRemixUnlock) {

            IEnumerator origMethod = orig(self, modeStats, newModeStats, doHeartGem, doStrawberries, doDeaths, doRemixUnlock);
            while (origMethod.MoveNext()) yield return origMethod.Current;

            int oldBestDashes = dashCounterInChapterPanel == DashCountOptions.Fewest ? modeStats.BestDashes : (_SaveData as DashCountModSaveData).OldDashCount;
            int newBestDashes = GetDashCountForChapterPanel(newModeStats, self.Area);

            if (newModeStats.SingleRunCompleted && oldBestDashes != newBestDashes) {
                yield return 0.5f;

                Audio.Play("event:/ui/postgame/death_appear");
                dashesCounter.CanWiggle = true;
                dashesCounter.Visible = true;
                while (newBestDashes != oldBestDashes) {
                    int jumpSize;
                    yield return HandleDashTick(oldBestDashes, newBestDashes, out jumpSize);
                    oldBestDashes += Math.Sign(newBestDashes - oldBestDashes) * jumpSize;
                    dashesCounter.Amount = oldBestDashes;
                    if (oldBestDashes == newBestDashes) {
                        Audio.Play("event:/ui/postgame/death_final");
                    } else {
                        Audio.Play("event:/ui/postgame/death_count");
                    }
                    Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
                }
                dashesCounter.CanWiggle = false;

                yield return 0.8f;
            }

            yield break;
        }

        private int ModOuiChapterPanelGetModeHeight(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
            int origModeHeight = orig(self);
            if (origModeHeight == 540 && dashesOffset.Y == 160f) {
                return 610;
            }
            return origModeHeight;
        }

        // nearly another vanilla method copypaste
        private float HandleDashTick(int oldDashes, int newDashes, out int jumpSize) {
            int difference = Math.Abs(newDashes - oldDashes);
            if (difference < 3) {
                jumpSize = 1;
                return 0.3f;
            }
            if (difference < 8) {
                jumpSize = 2;
                return 0.2f;
            }
            if (difference < 30) {
                jumpSize = 5;
                return 0.1f;
            }
            if (difference < 100) {
                jumpSize = 10;
                return 0.1f;
            }
            if (difference < 1000) {
                jumpSize = 25;
                return 0.1f;
            }
            jumpSize = 100;
            return 0.1f;
        }
    }
}
