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

    public sealed class Dialog_ExportPlantMasks : Window, IHorticultureCollectionDialogSurface
    {
        private readonly NovelSeedsSettings settings;
        private readonly ThingDef fixedPlant;
        private readonly List<PlantSettingsRecord> candidates;
        private readonly HashSet<string> selectedPlants = new HashSet<string>();
        private string fileName = string.Empty;
        private string errorText;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

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
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => fixedPlant == null ? "Export Plant Masks" : "Export Masks - " + fixedPlant.LabelCap,
                DescriptionProvider = () => fixedPlant == null
                    ? "Choose the painted Plant and Produce masks to save. The export preserves the existing serialized mask format."
                    : "Save this plant's Plant and Produce masks without changing the source settings.",
                SearchProvider = () => string.Empty,
                SearchSetter = value => { },
                RowsProvider = ExportRows,
                EmptyProvider = () => "No painted plant or produce masks are available to export.",
                EntryLabelProvider = () => "File name",
                EntryProvider = () => fileName,
                EntrySetter = value => fileName = value,
                PrimaryLabelProvider = () => "Export",
                PrimaryActionCallback = Export,
                SecondaryLabelProvider = () => fixedPlant == null ? "Select all" : string.Empty,
                SecondaryActionCallback = SelectAll,
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.mask.export");
        }

        public override void DoWindowContents(Rect inRect)
        {
            canvasDocument?.Draw(inRect);
        }

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
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

        private void SelectAll()
        {
            foreach (PlantSettingsRecord record in candidates) selectedPlants.Add(record.PlantDefName);
        }

        private IReadOnlyList<HorticultureDialogRow> ExportRows()
        {
            return candidates.Select((record, index) => new HorticultureDialogRow
            {
                Id = "plant-" + index,
                Label = PlantLabel(record.PlantDefName),
                Detail = "Plant and Produce masks",
                Status = selectedPlants.Contains(record.PlantDefName) ? "Selected" : "Not selected",
                Selected = selectedPlants.Contains(record.PlantDefName),
                CanToggle = true,
                Toggle = value =>
                {
                    if (value) selectedPlants.Add(record.PlantDefName); else selectedPlants.Remove(record.PlantDefName);
                }
            }).ToArray();
        }

        string IHorticultureCollectionDialogSurface.Title => fixedPlant == null ? "Export Plant Masks" : "Export Masks - " + fixedPlant.LabelCap;
        string IHorticultureCollectionDialogSurface.Description => fixedPlant == null
            ? "Choose the painted Plant and Produce masks to save. The export preserves the existing serialized mask format."
            : "Save this plant's Plant and Produce masks without changing the source settings.";
        string IHorticultureCollectionDialogSurface.Search { get => string.Empty; set { } }
        IReadOnlyList<HorticultureDialogRow> IHorticultureCollectionDialogSurface.Rows => ExportRows();
        string IHorticultureCollectionDialogSurface.EmptyText => "No painted plant or produce masks are available to export.";
        string IHorticultureCollectionDialogSurface.EntryLabel => "File name";
        string IHorticultureCollectionDialogSurface.Entry { get => fileName; set => fileName = value ?? string.Empty; }
        Action IHorticultureCollectionDialogSurface.EntryAction => null;
        string IHorticultureCollectionDialogSurface.PrimaryLabel => "Export";
        Action IHorticultureCollectionDialogSurface.PrimaryAction => Export;
        string IHorticultureCollectionDialogSurface.SecondaryLabel => fixedPlant == null ? "Select all" : string.Empty;
        Action IHorticultureCollectionDialogSurface.SecondaryAction => fixedPlant == null ? (Action)SelectAll : null;
        void IHorticultureCollectionDialogSurface.Close() => Close();

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

    public sealed class Dialog_ImportPlantMasks : Window, IHorticultureCollectionDialogSurface
    {
        private readonly PlantMaskFileInfo file;
        private readonly NovelSeedsSettings settings;
        private readonly PlantMaskLibraryFile library;
        private readonly HashSet<string> selectedPlants = new HashSet<string>();
        private readonly string loadError;
        private string importError;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

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
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => "Import Plant Masks",
                DescriptionProvider = () => loadError.NullOrEmpty()
                    ? "Choose which Plant and Produce masks to import. Unchecked plants keep their current masks."
                    : "Could not read the selected plant mask file: " + loadError,
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = ImportRows,
                EmptyProvider = () => loadError.NullOrEmpty() ? "No plants are available in this file." : "The mask file could not be read.",
                PrimaryLabelProvider = () => "Import",
                PrimaryActionCallback = Import,
                SecondaryLabelProvider = () => "Select all",
                SecondaryActionCallback = SelectAll,
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.mask.import");
        }

        public override void DoWindowContents(Rect inRect)
        {
            canvasDocument?.Draw(inRect);
        }

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
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

        private void SelectAll()
        {
            if (library == null) return;
            foreach (PlantMaskExportRecord record in library.Plants)
                if (AvailablePlant(record.PlantDefName) != null) selectedPlants.Add(record.PlantDefName);
        }

        private IReadOnlyList<HorticultureDialogRow> ImportRows()
        {
            if (library == null) return new HorticultureDialogRow[0];
            return library.Plants.Select((record, index) =>
            {
                ThingDef plant = AvailablePlant(record.PlantDefName);
                bool available = plant != null;
                return new HorticultureDialogRow
                {
                    Id = "plant-" + index,
                    Label = available ? plant.LabelCap.ToString() : "Unavailable plant",
                    Detail = available ? "Plant and Produce masks" : "This plant is not loaded in the current game.",
                    Status = available
                        ? (selectedPlants.Contains(record.PlantDefName) ? "Selected" : "Not selected")
                        : "Unavailable",
                    Selected = available && selectedPlants.Contains(record.PlantDefName),
                    CanToggle = available,
                    Toggle = available ? (Action<bool>)(value =>
                    {
                        if (value) selectedPlants.Add(record.PlantDefName); else selectedPlants.Remove(record.PlantDefName);
                    }) : null,
                    Warning = !available
                };
            }).ToArray();
        }

        string IHorticultureCollectionDialogSurface.Title => "Import Plant Masks";
        string IHorticultureCollectionDialogSurface.Description => loadError.NullOrEmpty()
            ? "Choose which Plant and Produce masks to import. Unchecked plants keep their current masks."
            : "Could not read the selected plant mask file: " + loadError;
        string IHorticultureCollectionDialogSurface.Search { get => search; set => search = value ?? string.Empty; }
        IReadOnlyList<HorticultureDialogRow> IHorticultureCollectionDialogSurface.Rows => ImportRows();
        string IHorticultureCollectionDialogSurface.EmptyText => loadError.NullOrEmpty() ? "No plants are available in this file." : "The mask file could not be read.";
        string IHorticultureCollectionDialogSurface.EntryLabel => string.Empty;
        string IHorticultureCollectionDialogSurface.Entry { get => string.Empty; set { } }
        Action IHorticultureCollectionDialogSurface.EntryAction => null;
        string IHorticultureCollectionDialogSurface.PrimaryLabel => loadError.NullOrEmpty() ? "Import" : string.Empty;
        Action IHorticultureCollectionDialogSurface.PrimaryAction => loadError.NullOrEmpty() ? (Action)Import : null;
        string IHorticultureCollectionDialogSurface.SecondaryLabel => "Select all";
        Action IHorticultureCollectionDialogSurface.SecondaryAction => SelectAll;
        void IHorticultureCollectionDialogSurface.Close() => Close();

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

    public sealed class Dialog_ImportPlantMaskForPlant : Window, IHorticultureCollectionDialogSurface
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
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

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
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => "Import Masks - " + plantDef.LabelCap,
                DescriptionProvider = () => "Choose a saved mask file. This replaces only this plant's Plant and Produce masks.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = FileRows,
                EmptyProvider = () => "No compatible plant mask files were found.",
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.mask.import-single");
        }

        public override void DoWindowContents(Rect inRect)
        {
            canvasDocument?.Draw(inRect);
        }

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
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

        private IReadOnlyList<HorticultureDialogRow> FileRows()
        {
            return choices.Select((choice, index) => new HorticultureDialogRow
            {
                Id = "file-" + index,
                Label = choice.file.Name + ".xml",
                Detail = !choice.error.NullOrEmpty() ? "Unreadable file" : choice.containsPlant ? "Contains masks for this plant" : "No masks for this plant",
                Status = choice.containsPlant && choice.error.NullOrEmpty() ? "Available" : "Unavailable",
                Warning = !choice.containsPlant || !choice.error.NullOrEmpty(),
                Activate = choice.containsPlant && choice.error.NullOrEmpty() ? (Action)(() => Import(choice.file)) : null,
                ActionLabel = "Import"
            }).ToArray();
        }

        string IHorticultureCollectionDialogSurface.Title => "Import Masks - " + plantDef.LabelCap;
        string IHorticultureCollectionDialogSurface.Description => "Choose a saved mask file. This replaces only this plant's Plant and Produce masks.";
        string IHorticultureCollectionDialogSurface.Search { get => search; set => search = value ?? string.Empty; }
        IReadOnlyList<HorticultureDialogRow> IHorticultureCollectionDialogSurface.Rows => FileRows();
        string IHorticultureCollectionDialogSurface.EmptyText => "No compatible plant mask files were found.";
        string IHorticultureCollectionDialogSurface.EntryLabel => string.Empty;
        string IHorticultureCollectionDialogSurface.Entry { get => string.Empty; set { } }
        Action IHorticultureCollectionDialogSurface.EntryAction => null;
        string IHorticultureCollectionDialogSurface.PrimaryLabel => string.Empty;
        Action IHorticultureCollectionDialogSurface.PrimaryAction => null;
        string IHorticultureCollectionDialogSurface.SecondaryLabel => string.Empty;
        Action IHorticultureCollectionDialogSurface.SecondaryAction => null;
        void IHorticultureCollectionDialogSurface.Close() => Close();
    }
}
