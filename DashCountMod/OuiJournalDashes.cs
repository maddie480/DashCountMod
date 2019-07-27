using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.DashCountMod {
    class OuiJournalDashes : OuiJournalPage {
        private readonly Table table;

        // very, very heavily based on OuiJournalDeaths from the base game, since this is kinda the same thing but with dashes instead of deaths.
        // we always call Dialog.Deaths since it only formats the numbers as "1.2k" or "2.12m" if they are higher than 9999.
        public OuiJournalDashes(OuiJournal journal) : base(journal) {
            PageTexture = "page";

            // builds the first line with column headers
            table = new Table().AddColumn(new TextCell(Dialog.Clean("journal_dashes", null), new Vector2(1f, 0.5f), 0.7f, TextColor, 300f, false));
            for (int i = 0; i < SaveData.Instance.UnlockedModes; i++) {
                table.AddColumn(new TextCell(Dialog.Clean("journal_mode_" + (AreaMode)i, null), TextJustify, 0.6f, TextColor, 240f, false));
            }

            bool[] hasFullyUnlockedMode = new bool[] {
                true,
                SaveData.Instance.UnlockedModes >= 2,
                SaveData.Instance.UnlockedModes >= 3
            };

            int[] modeTotals = new int[3];
            foreach (AreaStats areaStats in SaveData.Instance.Areas) {
                AreaData areaData = AreaData.Get(areaStats.ID);
                if (!areaData.Interlude) {
                    if (areaData.ID > SaveData.Instance.UnlockedAreas) {
                        // area was not reached yet: do not display a row for it
                        hasFullyUnlockedMode[0] = (hasFullyUnlockedMode[1] = (hasFullyUnlockedMode[2] = false));
                        break;
                    }

                    // add a row for this area
                    Row row = table.AddRow();
                    row.Add(new TextCell(Dialog.Clean(areaData.Name, null), new Vector2(1f, 0.5f), 0.6f, TextColor, 0f, false));

                    // and add a column for each mode
                    for (int j = 0; j < SaveData.Instance.UnlockedModes; j++) {
                        if (areaStats.Modes[j].SingleRunCompleted) {
                            // completed mode
                            int bestDashes = areaStats.Modes[j].BestDashes;
                            row.Add(new TextCell(Dialog.Deaths(bestDashes), TextJustify, 0.5f, TextColor, 0f, false));
                            modeTotals[j] += bestDashes;
                        } else {
                            // mode that was never completed
                            row.Add(new IconCell("dot", 0f));
                            hasFullyUnlockedMode[j] = false;
                        }
                    }
                }
            }

            if (hasFullyUnlockedMode[0] || hasFullyUnlockedMode[1] || hasFullyUnlockedMode[2]) {
                // display the Totals row, since one mode has been fully unlocked
                table.AddRow();
                Row row2 = table.AddRow();
                row2.Add(new TextCell(Dialog.Clean("journal_totals", null), new Vector2(1f, 0.5f), 0.7f, TextColor, 0f, false));
                for (int k = 0; k < SaveData.Instance.UnlockedModes; k++) {
                    row2.Add(new TextCell(Dialog.Deaths(modeTotals[k]), TextJustify, 0.6f, TextColor, 0f, false));
                }

                if (hasFullyUnlockedMode[0] && hasFullyUnlockedMode[1] && hasFullyUnlockedMode[2]) {
                    // since all modes have been fully unlocked, we can now display the Grand Total
                    TextCell textCell = new TextCell(Dialog.Deaths(modeTotals[0] + modeTotals[1] + modeTotals[2]), TextJustify, 0.6f, TextColor, 0f, false) {
                        SpreadOverColumns = 3
                    };
                    table.AddRow().Add(new TextCell(Dialog.Clean("journal_grandtotal", null), new Vector2(1f, 0.5f), 0.7f, TextColor, 0f, false)).Add(textCell);
                }
            }
        }

        // this is a copypaste of the vanilla method though
        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);
            Draw.SpriteBatch.Begin();
            table.Render(new Vector2(60f, 20f));
            if (SaveData.Instance.AssistMode) {
                GFX.Gui["fileselect/assist"].DrawCentered(new Vector2(1250f, 810f), Color.White * 0.5f, 1f, 0.2f);
            }
            if (SaveData.Instance.CheatMode) {
                GFX.Gui["fileselect/cheatmode"].DrawCentered(new Vector2(1400f, 860f), Color.White * 0.5f, 1f, 0f);
            }
            Draw.SpriteBatch.End();
        }
    }
}
