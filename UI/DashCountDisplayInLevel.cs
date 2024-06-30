using Celeste.Mod.CollabUtils2;
using Celeste.Mod.CollabUtils2.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod.UI {
    public class DashCountDisplayInLevel : AbstractCountDisplayInLevel {
        public DashCountDisplayInLevel(Session session, DashCountModSettings.ShowCountInGameOptions format)
            : base(session, format, GFX.Gui["collectables/dashes"], new List<AbstractCountDisplayInLevel>()) {
        }

        private string getCountToDisplay() {
            int dashCountInLevel = 0;

            AreaKey area = SceneAs<Level>().Session.Area;
            if ((DashCountModModule.Instance._SaveData as DashCountModSaveData).DashCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                if (areaModes.TryGetValue(area.Mode, out int totalDashes)) {
                    dashCountInLevel = totalDashes;
                }
            }

            switch (format) {
                case DashCountModSettings.ShowCountInGameOptions.Session:
                    return "" + session.Dashes;
                case DashCountModSettings.ShowCountInGameOptions.Chapter:
                    return "" + dashCountInLevel;
                case DashCountModSettings.ShowCountInGameOptions.File:
                    return "" + SaveData.Instance.TotalDashes;
                default:
                    return SaveData.Instance.TotalDashes + " (" + dashCountInLevel + ")";
            }
        }

        protected override int GetCountForSession() {
            return session.Dashes;
        }

        protected override int GetCountForChapter() {
            return GetCountFromSaveData(ModSaveData.DashCountPerLevel);
        }
        protected override int GetCountForFile() {
            return SaveData.Instance.TotalDashes;
        }
    }
}
