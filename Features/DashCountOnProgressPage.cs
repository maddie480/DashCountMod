using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod.Features {
    public static class DashCountOnProgressPage {
        private static DashCountOptions fewestDashesInProgressPage = DashCountOptions.None;

        public static void Initialize() {
            EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");
            if (collabUtils != null) {
                if (((DashCountModSettings) DashCountModModule.Instance._Settings).DashCountOnProgressPage != DashCountOptions.None) {
                    hookCollabUtilsJournalPage();
                }
            }
        }

        private static Hook collabUtilsJournalsHook = null;
        private static Hook collabUtilsJournalsTotalDashesHook = null;
        private static Hook collabUtilsJournalsLevelDashesHook = null;
        private static ILHook collabUtilsOverworldJournalSizeHook = null;
        private static ILHook collabUtilsLobbyJournalSizeHook = null;

        public static void SetValue(DashCountOptions newValue) {
            bool wasEnabled = (fewestDashesInProgressPage != DashCountOptions.None);
            bool isEnabled = (newValue != DashCountOptions.None);

            // (un)hook methods
            if (isEnabled && !wasEnabled) {
                Logger.Log("DashCountMod", "Hooking journal progress page rendering methods");

                IL.Celeste.OuiJournalProgress.ctor += modOuiJournalProgressColumnSizes;
                IL.Celeste.OuiJournalProgress.ctor += modOuiJournalProgressConstructor;
                hookCollabUtilsJournalPage();
            } else if (!isEnabled && wasEnabled) {
                Logger.Log("DashCountMod", "Unhooking journal progress page rendering methods");

                IL.Celeste.OuiJournalProgress.ctor -= modOuiJournalProgressColumnSizes;
                IL.Celeste.OuiJournalProgress.ctor -= modOuiJournalProgressConstructor;

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
                        typeof(DashCountOnProgressPage).GetMethod("enableDashCountModInCollabUtils", BindingFlags.NonPublic | BindingFlags.Static));

                    collabUtilsJournalsTotalDashesHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressDashCountMod").GetMethod("DisplaysTotalDashes", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountOnProgressPage).GetMethod("displaysTotalDashesInCollabUtils", BindingFlags.NonPublic | BindingFlags.Static));

                    collabUtilsJournalsLevelDashesHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressDashCountMod").GetMethod("GetLevelDashesForJournalProgress", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountOnProgressPage).GetMethod("getLevelDashesInJournalProgressInCollabUtils", BindingFlags.NonPublic | BindingFlags.Static));

                    collabUtilsOverworldJournalSizeHook = new ILHook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressInOverworld").GetConstructor(new Type[] { typeof(OuiJournal) }),
                        modOuiJournalProgressColumnSizes);

                    collabUtilsLobbyJournalSizeHook = new ILHook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressInLobby").GetConstructor(new Type[] { typeof(OuiJournal), typeof(string), typeof(bool) }),
                        modOuiJournalProgressColumnSizes);

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
            return getTotalDashesForJournalProgress(stats);
        }

        private static void modOuiJournalProgressColumnSizes(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // patch columns to be narrower (100 => 80, 150 => 120, 20 => 0)
            while (cursor.TryGotoNext(instr => instr.MatchLdcR4(100f) || instr.MatchLdcR4(150f) || instr.MatchLdcR4(20f))) {
                float currentValue = (float) cursor.Next.Operand;
                float newValue = (currentValue == 100f ? 80f : (currentValue == 150f ? 120f : 0f));
                Logger.Log("DashCountMod", $"Modding column size from {currentValue} to {newValue} at {cursor.Index} in CIL code for {il.Method.FullName}");
                cursor.Next.Operand = newValue;
            }
        }

        private static void modOuiJournalProgressConstructor(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // add a column header for dash counts, just before the "time" column
            if (cursor.TryGotoNext(instr => instr.MatchLdstr("time"))) {
                Logger.Log("DashCountMod", $"Adding column header for fewest dashes at {cursor.Index} in CIL code for OuiJournalProgress constructor");

                // At this point, we loaded this.table into the stack. Just use it.
                cursor.EmitDelegate<Action<OuiJournalPage.Table>>(addColumnHeaderForFewestDashes);

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
                    cursor.EmitDelegate<Action<AreaStats, OuiJournalPage.Row, OuiJournalProgress>>(addColumnValueForFewestDashes);

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
                cursor.EmitDelegate<Action<OuiJournalPage.Row, OuiJournalProgress>>(addColumnTotalForFewestDashes);

                // Then inject row back, so that the game can add the time column.
                cursor.Emit(OpCodes.Ldloc_S, varIndexForRow);
            }
        }

        private static void addColumnHeaderForFewestDashes(OuiJournalPage.Table table) {
            table.AddColumn(new OuiJournalPage.IconCell("max480/DashCountMod/dashes", 80f));
        }

        private static int getTotalDashesForJournalProgress(AreaStats stats) {
            if (fewestDashesInProgressPage == DashCountOptions.Fewest) {
                return stats.BestTotalDashes;
            }

            int count = 0;
            if (((DashCountModSaveData) DashCountModModule.Instance._SaveData).DashCountPerLevel.TryGetValue(stats.GetSID(), out Dictionary<AreaMode, int> result)) {
                foreach (int value in result.Values) {
                    count += value;
                }
            }
            return count;
        }

        private static void addColumnValueForFewestDashes(AreaStats areaStats, OuiJournalPage.Row row, OuiJournalProgress self) {
            if (fewestDashesInProgressPage == DashCountOptions.Total && areaStats.TotalTimePlayed > 0) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(getTotalDashesForJournalProgress(areaStats)), self.TextJustify, 0.5f, self.TextColor));
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
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(getTotalDashesForJournalProgress(areaStats)), self.TextJustify, 0.5f, self.TextColor));
            } else {
                row.Add(new OuiJournalPage.IconCell("dot"));
            }
        }

        private static void addColumnTotalForFewestDashes(OuiJournalPage.Row row, OuiJournalProgress self) {
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
                    if (allSingleRunCompleted) totalDashes += getTotalDashesForJournalProgress(areaStats);
                }
            }

            if (allSingleRunCompleted) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(totalDashes), self.TextJustify, 0.6f, self.TextColor));
            } else {
                row.Add(new OuiJournalPage.IconCell("dot"));
            }
        }
    }
}
