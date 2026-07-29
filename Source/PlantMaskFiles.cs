using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class PlantMaskFileInfo
    {
        public string Name;
        public string Path;
        public DateTime Modified;
    }

    public sealed class PlantMaskLibraryFile : IExposable
    {
        private int formatVersion = 2;
        private List<PlantMaskExportRecord> plants = new List<PlantMaskExportRecord>();

        public int FormatVersion => formatVersion;
        public IReadOnlyList<PlantMaskExportRecord> Plants => plants;

        public PlantMaskLibraryFile() { }

        public PlantMaskLibraryFile(IEnumerable<PlantSettingsRecord> records)
        {
            plants = (records ?? Enumerable.Empty<PlantSettingsRecord>())
                .Where(record => record?.HasMaskData == true)
                .Select(record => new PlantMaskExportRecord(record))
                .OrderBy(record => record.PlantDefName)
                .ToList();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref formatVersion, "formatVersion", 2);
            Scribe_Collections.Look(ref plants, "plants", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        public void Normalize()
        {
            if (plants == null) plants = new List<PlantMaskExportRecord>();
            plants.RemoveAll(record => record == null || record.PlantDefName.NullOrEmpty());
            foreach (PlantMaskExportRecord record in plants) record.Normalize();
            plants = plants.GroupBy(record => record.PlantDefName).Select(group => group.First()).OrderBy(record => record.PlantDefName).ToList();
        }
    }

    public sealed class PlantMaskExportRecord : IExposable
    {
        private string plantDefName;
        private bool usePlantMasks;
        private List<VisualMaskLayerRecord> plantMaskLayers = new List<VisualMaskLayerRecord>();
        private List<PlantMaskVariationRecord> plantMaskVariations = new List<PlantMaskVariationRecord>();
        private bool useProduceMasks;
        private List<VisualMaskLayerRecord> produceMaskLayers = new List<VisualMaskLayerRecord>();

        public string PlantDefName => plantDefName;

        public PlantMaskExportRecord() { }

        public PlantMaskExportRecord(PlantSettingsRecord source)
        {
            plantDefName = source?.PlantDefName;
            usePlantMasks = source?.usePlantMasks == true;
            plantMaskLayers = source?.PlantMaskLayers.Select(layer => layer.Clone()).ToList() ?? new List<VisualMaskLayerRecord>();
            plantMaskVariations = source?.PlantMaskVariationRecords
                .Select(record => new PlantMaskVariationRecord(record.VariationIndex, record.Layers)).ToList() ?? new List<PlantMaskVariationRecord>();
            useProduceMasks = source?.useProduceMasks == true;
            produceMaskLayers = source?.ProduceMaskLayers.Select(layer => layer.Clone()).ToList() ?? new List<VisualMaskLayerRecord>();
            Normalize();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref plantDefName, "plantDef");
            Scribe_Values.Look(ref usePlantMasks, "usePlantMasks", false);
            Scribe_Collections.Look(ref plantMaskLayers, "plantMaskLayers", LookMode.Deep);
            Scribe_Collections.Look(ref plantMaskVariations, "plantMaskVariations", LookMode.Deep);
            Scribe_Values.Look(ref useProduceMasks, "useProduceMasks", false);
            Scribe_Collections.Look(ref produceMaskLayers, "produceMaskLayers", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        public void Normalize()
        {
            if (plantMaskLayers == null) plantMaskLayers = new List<VisualMaskLayerRecord>();
            if (plantMaskVariations == null) plantMaskVariations = new List<PlantMaskVariationRecord>();
            if (produceMaskLayers == null) produceMaskLayers = new List<VisualMaskLayerRecord>();
            plantMaskLayers.RemoveAll(layer => layer == null);
            plantMaskVariations.RemoveAll(record => record == null);
            produceMaskLayers.RemoveAll(layer => layer == null);
            foreach (PlantMaskVariationRecord record in plantMaskVariations) record.Normalize();
        }

        public void ApplyTo(PlantSettingsRecord destination)
        {
            destination?.ReplaceMasks(usePlantMasks, plantMaskLayers, plantMaskVariations, useProduceMasks, produceMaskLayers);
        }
    }

    public static class PlantMaskFileManager
    {
        private const string Extension = ".xml";
        private static List<PlantMaskFileInfo> cachedFiles;

        public static string DirectoryPath => System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "HorticultureNovelSeedsMasks");

        public static IReadOnlyList<PlantMaskFileInfo> Files
        {
            get
            {
                if (cachedFiles == null) Refresh();
                return cachedFiles;
            }
        }

        public static void Refresh()
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                cachedFiles = new DirectoryInfo(DirectoryPath).GetFiles("*" + Extension)
                    .OrderBy(file => file.Name)
                    .Select(file => new PlantMaskFileInfo
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(file.Name),
                        Path = file.FullName,
                        Modified = file.LastWriteTime
                    }).ToList();
            }
            catch (Exception exception)
            {
                cachedFiles = new List<PlantMaskFileInfo>();
                Log.Error("Horticulture - Novel Seeds could not list plant mask files: " + exception);
            }
        }

        public static string NormalizeName(string name) => SettingsProfileManager.NormalizeName(name);

        public static bool Exists(string name)
        {
            string normalized = NormalizeName(name);
            return !normalized.NullOrEmpty() && File.Exists(PathFor(normalized));
        }

        public static bool Export(string name, NovelSeedsSettings settings, out int plantCount, out string error)
        {
            return Export(name, settings, null, out plantCount, out error);
        }

        public static bool Export(string name, NovelSeedsSettings settings, IEnumerable<string> selectedPlants, out int plantCount, out string error)
        {
            plantCount = 0;
            error = null;
            string normalized = NormalizeName(name);
            if (normalized.NullOrEmpty())
            {
                error = "Enter a file name.";
                return false;
            }
            if (settings == null)
            {
                error = "No active mask configuration is available.";
                return false;
            }

            HashSet<string> selected = selectedPlants == null ? null : new HashSet<string>(selectedPlants.Where(item => !item.NullOrEmpty()));
            IEnumerable<PlantSettingsRecord> records = settings.PlantSettingsRecords;
            if (selected != null) records = records.Where(record => record != null && selected.Contains(record.PlantDefName));
            PlantMaskLibraryFile library = new PlantMaskLibraryFile(records);
            plantCount = library.Plants.Count;
            if (plantCount == 0)
            {
                error = "Select at least one plant with mask data.";
                return false;
            }
            string path = PathFor(normalized);
            string temporaryPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                Scribe.saver.InitSaving(temporaryPath, "HorticultureNovelSeedsPlantMasks");
                Scribe_Deep.Look(ref library, "maskLibrary");
                Scribe.saver.FinalizeSaving();
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
                Refresh();
                return true;
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                TryDelete(temporaryPath);
                error = exception.Message;
                return false;
            }
        }

        public static bool TryLoad(PlantMaskFileInfo file, out PlantMaskLibraryFile library, out string error)
        {
            library = null;
            error = null;
            if (file == null || !File.Exists(file.Path))
            {
                error = "The selected plant mask file is unavailable.";
                return false;
            }

            try
            {
                Scribe.loader.InitLoading(file.Path);
                Scribe_Deep.Look(ref library, "maskLibrary");
                Scribe.loader.FinalizeLoading();
                if (library == null)
                {
                    error = "The file did not contain a plant mask library.";
                    return false;
                }
                if (library.FormatVersion > 2)
                {
                    error = "This mask file was created by a newer, incompatible version of the mod.";
                    return false;
                }

                library.Normalize();
                return true;
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                library = null;
                error = exception.Message;
                return false;
            }
        }

        public static bool Import(PlantMaskFileInfo file, NovelSeedsSettings settings, IEnumerable<string> selectedPlants, out int imported, out int skipped, out string error)
        {
            imported = 0;
            skipped = 0;
            error = null;
            if (settings == null)
            {
                error = "No active mask configuration is available.";
                return false;
            }
            if (!TryLoad(file, out PlantMaskLibraryFile library, out error)) return false;

            try
            {
                HashSet<string> selected = selectedPlants == null
                    ? new HashSet<string>(library.Plants.Select(record => record.PlantDefName))
                    : new HashSet<string>(selectedPlants.Where(item => !item.NullOrEmpty()));
                foreach (PlantMaskExportRecord exported in library.Plants.Where(record => selected.Contains(record.PlantDefName)))
                {
                    ThingDef plantDef = DefDatabase<ThingDef>.GetNamedSilentFail(exported.PlantDefName);
                    if (plantDef?.plant == null)
                    {
                        skipped++;
                        continue;
                    }
                    exported.ApplyTo(settings.GetPlantSettings(plantDef));
                    imported++;
                }
                settings.Normalize();
                settings.ClearVisualCache();
                ProduceMaskRenderer.ClearAll();
                RefreshMapGraphics();
                settings.Write();
                return true;
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                error = exception.Message;
                return false;
            }
        }

        public static bool Delete(PlantMaskFileInfo file, out string error)
        {
            error = null;
            if (file == null) return false;
            try
            {
                if (File.Exists(file.Path)) File.Delete(file.Path);
                Refresh();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool OpenDirectory(out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                Application.OpenURL(new Uri(DirectoryPath).AbsoluteUri);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string PathFor(string normalizedName) => System.IO.Path.Combine(DirectoryPath, normalizedName + Extension);

        private static void RefreshMapGraphics()
        {
            if (Current.Game == null) return;
            foreach (Map map in Find.Maps)
            {
                if (map?.mapDrawer == null || map.listerThings == null) continue;
                foreach (Thing thing in map.listerThings.AllThings)
                    if (thing is Plant plant && plant.TryGetComp<CompPlantVariety>()?.HasAnyTraits == true)
                        map.mapDrawer.MapMeshDirty(plant.Position, (ulong)MapMeshFlagDefOf.Things);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    public sealed class Dialog_ExportPlantMasks : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly ThingDef fixedPlant;
        private readonly List<PlantSettingsRecord> candidates;
        private readonly HashSet<string> selectedPlants = new HashSet<string>();
        private string fileName = string.Empty;
        private string errorText;
        private bool focused;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => fixedPlant == null ? new Vector2(760f, 700f) : new Vector2(580f, 310f);

        public Dialog_ExportPlantMasks(NovelSeedsSettings settings, ThingDef fixedPlant = null)
        {
            this.settings = settings;
            this.fixedPlant = fixedPlant;
            candidates = (settings?.PlantSettingsRecords ?? Enumerable.Empty<PlantSettingsRecord>())
                .Where(record => record?.HasMaskData == true && (fixedPlant == null || record.PlantDefName == fixedPlant.defName))
                .OrderBy(record => PlantLabel(record.PlantDefName)).ToList();
            foreach (PlantSettingsRecord record in candidates) selectedPlants.Add(record.PlantDefName);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string title = fixedPlant == null ? "Export Plant Masks" : "Export Masks - " + fixedPlant.LabelCap;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), title);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 42f, inRect.width, 26f), "Name this plant mask file.");
            GUI.SetNextControlName("HNS_MaskFileName");
            fileName = Widgets.TextField(new Rect(inRect.x, inRect.y + 72f, inRect.width, 32f), fileName, 64);
            if (!focused)
            {
                UI.FocusControl("HNS_MaskFileName", this);
                focused = true;
            }
            float contentTop = 116f;
            if (fixedPlant == null)
            {
                if (Widgets.ButtonText(new Rect(inRect.x, contentTop, 110f, 30f), "Select All"))
                    foreach (PlantSettingsRecord record in candidates) selectedPlants.Add(record.PlantDefName);
                if (Widgets.ButtonText(new Rect(inRect.x + 120f, contentTop, 110f, 30f), "Clear All")) selectedPlants.Clear();
                Widgets.Label(new Rect(inRect.x + 248f, contentTop + 5f, inRect.width - 248f, 24f), selectedPlants.Count + " of " + candidates.Count + " plants selected");
                contentTop += 42f;

                Rect listRect = new Rect(inRect.x, contentTop, inRect.width, inRect.height - contentTop - 54f);
                Widgets.DrawMenuSection(listRect);
                Rect outRect = listRect.ContractedBy(8f);
                Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, candidates.Count * 38f));
                Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
                float y = 0f;
                foreach (PlantSettingsRecord record in candidates)
                {
                    Rect row = new Rect(0f, y, viewRect.width, 34f);
                    Widgets.DrawHighlightIfMouseover(row);
                    bool enabled = selectedPlants.Contains(record.PlantDefName);
                    Widgets.CheckboxLabeled(row.ContractedBy(6f, 3f), PlantLabel(record.PlantDefName), ref enabled);
                    if (enabled) selectedPlants.Add(record.PlantDefName); else selectedPlants.Remove(record.PlantDefName);
                    y += 38f;
                }
                if (candidates.Count == 0) Widgets.Label(new Rect(12f, 12f, viewRect.width - 24f, 30f), "No painted plant or produce masks are available to export.");
                Widgets.EndScrollView();
            }
            else
            {
                string summary = candidates.Count == 0
                    ? "This plant does not currently have any mask data to export."
                    : "This file will contain the Plant and Produce masks for " + fixedPlant.LabelCap + ".";
                Widgets.Label(new Rect(inRect.x, contentTop + 6f, inRect.width, 48f), summary);
            }

            if (!errorText.NullOrEmpty()) DrawError(new Rect(inRect.x, inRect.yMax - 78f, inRect.width - 230f, 44f), errorText);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 220f, inRect.yMax - 34f, 100f, 30f), "Cancel")) Close();
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && selectedPlants.Count > 0;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Export")) Export();
            GUI.enabled = oldEnabled;
        }

        public override void OnAcceptKeyPressed()
        {
            Export();
        }

        private void Export()
        {
            string normalized = PlantMaskFileManager.NormalizeName(fileName);
            if (normalized.NullOrEmpty())
            {
                errorText = "Enter a file name.";
                return;
            }
            if (PlantMaskFileManager.Exists(normalized))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Replace the plant mask file '" + normalized + "'?", delegate { ExportNow(normalized); }, true));
                return;
            }
            ExportNow(normalized);
        }

        private void ExportNow(string normalized)
        {
            if (PlantMaskFileManager.Export(normalized, settings, selectedPlants, out int count, out string error))
            {
                Messages.Message("Exported masks for " + count + " plants to '" + normalized + ".xml'.", MessageTypeDefOf.TaskCompletion, false);
                Close();
            }
            else errorText = "Could not export plant masks: " + error;
        }

        private static string PlantLabel(string defName)
        {
            ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return plant?.LabelCap.ToString() ?? defName;
        }

        private static void DrawError(Rect rect, string text)
        {
            Color old = GUI.color;
            GUI.color = ColorLibrary.RedReadable;
            Widgets.Label(rect, text);
            GUI.color = old;
        }
    }

    public sealed class Dialog_ImportPlantMasks : Window
    {
        private readonly PlantMaskFileInfo file;
        private readonly NovelSeedsSettings settings;
        private readonly PlantMaskLibraryFile library;
        private readonly HashSet<string> selectedPlants = new HashSet<string>();
        private readonly string loadError;
        private Vector2 scrollPosition;
        private string importError;

        public override Vector2 InitialSize => new Vector2(760f, 700f);

        public Dialog_ImportPlantMasks(PlantMaskFileInfo file, NovelSeedsSettings settings)
        {
            this.file = file;
            this.settings = settings;
            if (PlantMaskFileManager.TryLoad(file, out PlantMaskLibraryFile loaded, out string error))
            {
                library = loaded;
                foreach (PlantMaskExportRecord record in library.Plants)
                    if (AvailablePlant(record.PlantDefName) != null) selectedPlants.Add(record.PlantDefName);
            }
            else loadError = error;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "Import Plant Masks");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, inRect.width, 44f), "Choose which masks to import from " + file.Name + ".xml. Unchecked plants keep their current masks.");
            if (!loadError.NullOrEmpty())
            {
                DrawError(new Rect(0f, 94f, inRect.width, 80f), "Could not read this mask file: " + loadError);
                if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Close")) Close();
                return;
            }

            IReadOnlyList<PlantMaskExportRecord> records = library.Plants;
            if (Widgets.ButtonText(new Rect(0f, 92f, 110f, 30f), "Select All"))
                foreach (PlantMaskExportRecord record in records) if (AvailablePlant(record.PlantDefName) != null) selectedPlants.Add(record.PlantDefName);
            if (Widgets.ButtonText(new Rect(120f, 92f, 110f, 30f), "Clear All")) selectedPlants.Clear();
            Widgets.Label(new Rect(248f, 97f, inRect.width - 248f, 24f), selectedPlants.Count + " of " + records.Count + " plants selected");

            Rect listRect = new Rect(0f, 134f, inRect.width, inRect.height - 190f);
            Widgets.DrawMenuSection(listRect);
            Rect outRect = listRect.ContractedBy(8f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, records.Count * 44f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (PlantMaskExportRecord record in records)
            {
                ThingDef plant = AvailablePlant(record.PlantDefName);
                Rect row = new Rect(0f, y, viewRect.width, 40f);
                Widgets.DrawHighlightIfMouseover(row);
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && plant != null;
                bool enabled = selectedPlants.Contains(record.PlantDefName);
                Widgets.CheckboxLabeled(new Rect(6f, y + 3f, viewRect.width - 180f, 32f), plant?.LabelCap.ToString() ?? record.PlantDefName, ref enabled);
                GUI.enabled = oldEnabled;
                if (plant != null)
                {
                    if (enabled) selectedPlants.Add(record.PlantDefName); else selectedPlants.Remove(record.PlantDefName);
                }
                else
                {
                    TextAnchor oldAnchor = Text.Anchor;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Color oldColor = GUI.color;
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(viewRect.width - 174f, y + 3f, 166f, 32f), "Plant not loaded");
                    GUI.color = oldColor;
                    Text.Anchor = oldAnchor;
                }
                y += 44f;
            }
            Widgets.EndScrollView();

            if (!importError.NullOrEmpty()) DrawError(new Rect(0f, inRect.yMax - 48f, inRect.width - 230f, 40f), importError);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 220f, inRect.yMax - 34f, 100f, 30f), "Cancel")) Close();
            bool canImport = selectedPlants.Count > 0;
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && canImport;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Import")) Import();
            GUI.enabled = guiEnabled;
        }

        private void Import()
        {
            if (PlantMaskFileManager.Import(file, settings, selectedPlants, out int imported, out int skipped, out string error))
            {
                string skippedText = skipped > 0 ? " " + skipped + " unavailable plants were skipped." : string.Empty;
                Messages.Message("Imported masks for " + imported + " plants." + skippedText, MessageTypeDefOf.TaskCompletion, false);
                Close();
            }
            else importError = "Could not import plant masks: " + error;
        }

        private static ThingDef AvailablePlant(string defName)
        {
            ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return plant?.plant == null ? null : plant;
        }

        private static void DrawError(Rect rect, string text)
        {
            Color old = GUI.color;
            GUI.color = ColorLibrary.RedReadable;
            Widgets.Label(rect, text);
            GUI.color = old;
        }
    }

    public sealed class Dialog_ImportPlantMaskForPlant : Window
    {
        private sealed class Choice
        {
            public PlantMaskFileInfo file;
            public bool containsPlant;
            public string error;
        }

        private readonly NovelSeedsSettings settings;
        private readonly ThingDef plantDef;
        private readonly List<Choice> choices = new List<Choice>();
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public Dialog_ImportPlantMaskForPlant(NovelSeedsSettings settings, ThingDef plantDef)
        {
            this.settings = settings;
            this.plantDef = plantDef;
            foreach (PlantMaskFileInfo file in PlantMaskFileManager.Files)
            {
                Choice choice = new Choice { file = file };
                if (PlantMaskFileManager.TryLoad(file, out PlantMaskLibraryFile library, out string error))
                    choice.containsPlant = library.Plants.Any(record => record.PlantDefName == plantDef.defName);
                else choice.error = error;
                choices.Add(choice);
            }
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "Import Masks - " + plantDef.LabelCap);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, inRect.width, 36f), "Choose a saved mask file. This replaces only this plant's Plant and Produce masks.");

            Rect listRect = new Rect(0f, 84f, inRect.width, inRect.height - 132f);
            Widgets.DrawMenuSection(listRect);
            Rect outRect = listRect.ContractedBy(8f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, choices.Count * 62f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Choice choice in choices)
            {
                Rect row = new Rect(0f, y, viewRect.width, 58f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(8f, y + 6f, viewRect.width - 220f, 24f), choice.file.Name + ".xml");
                string status = !choice.error.NullOrEmpty() ? "Unreadable file" : choice.containsPlant ? "Contains masks for this plant" : "No masks for this plant";
                Color oldColor = GUI.color;
                GUI.color = choice.containsPlant ? Color.gray : ColorLibrary.RedReadable;
                Widgets.Label(new Rect(8f, y + 30f, viewRect.width - 220f, 22f), status);
                GUI.color = oldColor;
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && choice.containsPlant;
                if (Widgets.ButtonText(new Rect(viewRect.width - 104f, y + 14f, 96f, 30f), "Import")) Import(choice.file);
                GUI.enabled = oldEnabled;
                if (!choice.error.NullOrEmpty()) TooltipHandler.TipRegion(row, choice.error);
                y += 62f;
            }
            if (choices.Count == 0) Widgets.Label(new Rect(12f, 12f, viewRect.width - 24f, 30f), "No plant mask files found.");
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Close")) Close();
        }

        private void Import(PlantMaskFileInfo file)
        {
            if (PlantMaskFileManager.Import(file, settings, new[] { plantDef.defName }, out int imported, out _, out string error) && imported == 1)
            {
                Messages.Message("Imported masks for " + plantDef.LabelCap + ".", MessageTypeDefOf.TaskCompletion, false);
                Close();
            }
            else Messages.Message("Could not import masks for " + plantDef.LabelCap + ": " + (error ?? "the plant was not present in the file"), MessageTypeDefOf.RejectInput, false);
        }
    }
}
