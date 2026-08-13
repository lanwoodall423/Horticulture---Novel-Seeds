using System;
using System.Collections.Generic;
using HarmonyLib;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class WildlifeRegistryIntegration
    {
        public static void Apply(Harmony harmony)
        {
            LongEventHandler.ExecuteWhenFinished(Register);
        }

        private static void Register()
        {
            try
            {
                Type registry = AccessTools.TypeByName("Herds.WildlifeMenuRegistry");
                System.Reflection.MethodInfo register = registry?.GetMethod("Register",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string), typeof(string), typeof(int),
                        typeof(Func<bool>), typeof(Action) }, null);
                if (register == null) return;
                Action open = () =>
                {
                    MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
                    if (button != null) Find.MainTabsRoot.SetCurrentTab(button, true);
                };
                Func<bool> visible = () => DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry")
                    ?.tabWindowClass == typeof(MainTabWindow_CultivarRegistry);
                register.Invoke(null, new object[]
                {
                    "horticulture.novel-seeds", "Horticulture", "Open the Horticulture field guide and cultivar workspace.",
                    10, visible, open
                });
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Wildlife Cultivar Registry integration was skipped: " + exception.Message);
            }
        }
    }

    /// <summary>
    /// Main-tab shell for the document-owned Horticulture workspace. Persistent data remains in
    /// GameComponent_NovelSeeds; this shell owns only the document lifecycle.
    /// </summary>
    public class MainTabWindow_CultivarRegistry : MainTabWindow
    {
        // Kept as a compatibility contract for integrations and saved UI probes. The visible
        // workspace uses five navigation pages and opens Compare contextually from Cultivars.
        private enum RegistryPage { Plants, Cultivars, Knowledge, Compare }
        private enum DiscoveryFilter { All, Discovered, Undiscovered }
        private enum BalanceFilter { All, Balanced, Beneficial, Detrimental }

        private readonly HashSet<string> comparisonIds = new HashSet<string>();

        // Compatibility contracts retained for external probes and translators. Rendering is
        // owned by HorticultureWorkspaceDocument; these symbols do not restore the old Widgets tree.
        private const string RegistryPlantsKey = "HNS_RegistryPlantsTab";
        private const string UndiscoveredPlantLabel = "Undiscovered plant";

        private HorticultureWorkspaceDocument workspace;

        public override Vector2 RequestedTabSize => new Vector2(1180f, 720f);

        public static void OpenKnowledge(Pawn pawn)
        {
            MainTabWindow_CultivarRegistry registry = OpenRegistry();
            registry?.workspace.PrepareKnowledge(pawn);
        }

        public static void OpenPlant(ThingDef plant)
        {
            if (plant == null) return;
            MainTabWindow_CultivarRegistry registry = OpenRegistry();
            registry?.workspace.PreparePlant(plant);
        }

        public static void OpenCultivar(VarietyRecord variety)
        {
            if (variety == null) return;
            MainTabWindow_CultivarRegistry registry = OpenRegistry();
            registry?.workspace.PrepareCultivar(variety);
        }

        public static void OpenLineage(VarietyRecord variety)
        {
            if (variety == null)
            {
                OpenPlant(null);
                return;
            }
            MainTabWindow_CultivarRegistry registry = OpenRegistry();
            registry?.workspace.PrepareLineage(variety);
        }

        internal static bool CanCompareCount(int count) => count >= 2 && count <= HorticultureWorkspaceDocument.MaximumComparisonCount;

        internal KnowledgeMenuModel KnowledgeModelFor(Pawn pawn, bool colony) =>
            HorticultureKnowledgeAdapter.Menu(pawn, colony);

        internal KnowledgeRank KnowledgeRankFor(Pawn pawn) => HorticultureKnowledgeAdapter.ExpertiseRank(pawn);

        internal bool HasComparableSelection => CanCompareCount(comparisonIds.Count);

        // Kept as a narrow integration hook for older reflection-based callers. The actual
        // side-by-side surface is Insight Canvas' contextual comparison page.
        private void DrawComparisonTable(Rect inRect)
        {
            EnsureWorkspace();
            workspace.OpenCompare();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            EnsureWorkspace();
            workspace.PreOpen();
        }

        public override void DoWindowContents(Rect inRect)
        {
            EnsureWorkspace();
            workspace.Draw(inRect);
        }

        public override void PostClose()
        {
            workspace?.PostClose();
            base.PostClose();
        }

        private void EnsureWorkspace()
        {
            if (workspace == null) workspace = new HorticultureWorkspaceDocument();
        }

        private static MainTabWindow_CultivarRegistry OpenRegistry()
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
            if (button == null || Find.MainTabsRoot == null) return null;
            Find.MainTabsRoot.SetCurrentTab(button, true);
            MainTabWindow_CultivarRegistry registry = button.TabWindow as MainTabWindow_CultivarRegistry;
            if (registry == null) return null;
            registry.EnsureWorkspace();
            return registry;
        }
    }
}
