namespace Celeste.Mod.DashCountMod {
    class DashCountModSettings : EverestModuleSettings {
        private bool dashCountInChapterPanel = false;

        [SettingInGame(false)]
        public bool DashCountInChapterPanel {
            get { return dashCountInChapterPanel; }
            set {
                dashCountInChapterPanel = value;
                DashCountModModule.Instance.SetDashCounterInChapterPanelEnabled(value);
            }
        }
    }
}
