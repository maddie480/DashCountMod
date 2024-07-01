using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.DashCountMod.UI {
    public abstract class AbstractOuiJournal : OuiJournalPage {
        private readonly Table table;

        // very, very heavily based on OuiJournalDeaths from the base game, since this is kinda the same thing but with dashes instead of deaths.
        // we always call Dialog.Deaths since it only formats the numbers as "1.2k" or "2.12m" if they are higher than 9999.
        protected AbstractOuiJournal(OuiJournal journal, string titleDialogId) : base(journal) {
            PageTexture = "page";

            // builds the first line with column headers
            table = new Table().AddColumn(new TextCell(Dialog.Clean(titleDialogId), new Vector2(1f, 0.5f), 0.7f, TextColor, 300f));
            for (int i = 0; i < SaveData.Instance.UnlockedModes; i++) {
                table.AddColumn(new TextCell(Dialog.Clean("journal_mode_" + (AreaMode) i), TextJustify, 0.6f, TextColor, 240f));
            }
            bool[] hasFullyUnlockedMode = new bool[3] {
                true,
                SaveData.Instance.UnlockedModes >= 2,
                SaveData.Instance.UnlockedModes >= 3
            };

            int[] modeTotals = new int[3];
            foreach (AreaStats areaStats in SaveData.Instance.Areas) {
                AreaData areaData = AreaData.Get(areaStats.ID);
                if (!areaData.Interlude && !areaData.IsFinal) {
                    if (areaData.ID > SaveData.Instance.UnlockedAreas) {
                        // area was not reached yet: do not display a row for it
                        hasFullyUnlockedMode[0] = (hasFullyUnlockedMode[1] = (hasFullyUnlockedMode[2] = false));
                        break;
                    }

                    // add a row for this area
                    Row row = table.AddRow();
                    row.Add(new TextCell(Dialog.Clean(areaData.Name), new Vector2(1f, 0.5f), 0.6f, TextColor));

                    // and add a column for each mode
                    for (int j = 0; j < SaveData.Instance.UnlockedModes; j++) {
                        if (areaData.HasMode((AreaMode) j)) {
                            if (areaStats.Modes[j].SingleRunCompleted) {
                                // completed mode
                                int best = GetBestCountFor(areaData.ToKey((AreaMode) j));
                                row.Add(new TextCell(Dialog.Deaths(best), TextJustify, 0.5f, TextColor));
                                modeTotals[j] += best;
                            } else {
                                // mode that was never completed
                                row.Add(new IconCell("dot"));
                                hasFullyUnlockedMode[j] = false;
                            }
                        } else {
                            // mode that does not exist
                            row.Add(new TextCell("-", TextJustify, 0.5f, TextColor));
                        }
                    }
                }
            }

            if (hasFullyUnlockedMode[0] || hasFullyUnlockedMode[1] || hasFullyUnlockedMode[2]) {
                // display the Totals row, since one mode has been fully unlocked
                table.AddRow();
                Row row2 = table.AddRow();
                row2.Add(new TextCell(Dialog.Clean("journal_totals"), new Vector2(1f, 0.5f), 0.7f, TextColor));
                for (int k = 0; k < SaveData.Instance.UnlockedModes; k++) {
                    row2.Add(new TextCell(Dialog.Deaths(modeTotals[k]), TextJustify, 0.6f, TextColor));
                }
                table.AddRow();
            }

            // now, display the rows for "final" levels (Farewell in vanilla Celeste)
            int finalAreasTotal = 0;
            foreach (AreaStats areaStats in SaveData.Instance.Areas) {
                AreaData areaData = AreaData.Get(areaStats.ID);
                if (areaData.IsFinal) {
                    if (areaData.ID > SaveData.Instance.UnlockedAreas) {
                        // area was not reached yet: do not display a row for it
                        break;
                    }

                    // add a row for this area
                    Row row = table.AddRow();
                    row.Add(new TextCell(Dialog.Clean(areaData.Name), new Vector2(1f, 0.5f), 0.6f, TextColor));

                    // and add the value for the A-side (other sides don't exist, so don't bother with them, that's what vanilla code does)
                    if (areaStats.Modes[0].SingleRunCompleted) {
                        int best = GetBestCountFor(areaData.ToKey(AreaMode.Normal));
                        TextCell entry = new TextCell(Dialog.Deaths(best), TextJustify, 0.5f, TextColor);
                        row.Add(entry);
                        finalAreasTotal += best;
                    } else {
                        row.Add(new IconCell("dot"));
                    }

                    table.AddRow();
                }
            }

            if (hasFullyUnlockedMode[0] && hasFullyUnlockedMode[1] && hasFullyUnlockedMode[2]) {
                // since all modes have been fully unlocked, we can now display the Grand Total
                TextCell textCell = new TextCell(Dialog.Deaths(modeTotals[0] + modeTotals[1] + modeTotals[2] + finalAreasTotal), TextJustify, 0.6f, TextColor) {
                    SpreadOverColumns = 3
                };
                table.AddRow().Add(new TextCell(Dialog.Clean("journal_grandtotal"), new Vector2(1f, 0.5f), 0.7f, TextColor)).Add(textCell);
            }
        }

        // this is a copypaste of the vanilla method though
        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);
            Draw.SpriteBatch.Begin();
            table.Render(new Vector2(60f, 20f));
            Draw.SpriteBatch.End();
        }

        protected abstract int GetBestCountFor(AreaKey areaKey);
    }
}
