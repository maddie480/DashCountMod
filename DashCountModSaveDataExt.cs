namespace Celeste.Mod.DashCountMod {
    public static class DashCountModeSaveDataExt {
        // behold: the exact same as SaveDataExt.GetAreaStatsFor except it includes vanilla levels!
        public static AreaStats GetAreaStatsForIncludingCeleste(this SaveData saveData, AreaKey key) {
            return saveData
                .LevelSets.Find(set => set.Name == key.GetLevelSet())
                .AreasIncludingCeleste.Find(area => area.SID == key.GetSID());
        }
    }
}