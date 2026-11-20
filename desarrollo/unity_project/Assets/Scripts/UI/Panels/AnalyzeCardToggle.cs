namespace WebGL.UI.Panels
{
    /// <summary>
    /// What pressing an Analyze card does (PC; PLAN_PC 41). A card whose effect is ON and whose panel is open turns the effect OFF (like Explode);
    /// with the effect ON but the panel closed it re-opens the panel (the fine adjustment stays reachable); with the effect off and the panel open
    /// it only closes the panel; otherwise it opens the panel (Cut also switches itself on).
    /// </summary>
    public static class AnalyzeCardToggle
    {
        public enum Step { OpenPanel, ClosePanel, TurnOff }

        public static Step ForCard(bool effectOn, bool panelOpen)
        {
            if (panelOpen) return effectOn ? Step.TurnOff : Step.ClosePanel;
            return Step.OpenPanel;
        }
    }
}
