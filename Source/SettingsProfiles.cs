using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class SettingsProfileInfo
    {
        public string Name;
        public string Path;
        public DateTime Modified;
    }

    public static class SettingsProfileManager
    {
        private const string FilePrefix = "HNS_";
        private const string Extension = ".xml";
        private const string DefaultFileName = "DefaultConfiguration.xml";
        private static List<SettingsProfileInfo> cachedProfiles;

        private static string ProfileDirectory => System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "HorticultureNovelSeedsProfiles");
        public static string PublisherDirectory => System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "HorticultureNovelSeedsPublisher");
        public static string PublisherExportPath => System.IO.Path.Combine(PublisherDirectory, DefaultFileName);
        public static string BundledDefaultPath => HorticultureNovelSeedsMod.ContentRootPath.NullOrEmpty()
            ? null
            : System.IO.Path.Combine(HorticultureNovelSeedsMod.ContentRootPath, "1.6", "Defaults", DefaultFileName);
        public static bool HasBundledDefault => !BundledDefaultPath.NullOrEmpty() && File.Exists(BundledDefaultPath);

        public static IReadOnlyList<SettingsProfileInfo> Profiles
        {
            get
            {
                if (cachedProfiles == null) Refresh();
                return cachedProfiles;
            }
        }

        public static void Refresh()
        {
            try
            {
                Directory.CreateDirectory(ProfileDirectory);
                cachedProfiles = new DirectoryInfo(ProfileDirectory).GetFiles(FilePrefix + "*" + Extension)
                    .OrderBy(file => file.Name)
                    .Select(file => new SettingsProfileInfo
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(file.Name).Substring(FilePrefix.Length),
                        Path = file.FullName,
                        Modified = file.LastWriteTime
                    }).ToList();
            }
            catch (Exception exception)
            {
                cachedProfiles = new List<SettingsProfileInfo>();
                Log.Error("Horticulture - Novel Seeds could not list configuration profiles: " + exception);
            }
        }

        public static string NormalizeName(string name)
        {
            string trimmed = name?.Trim() ?? string.Empty;
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string cleaned = new string(trimmed.Where(character => !invalid.Contains(character) && !char.IsControl(character)).ToArray()).Trim().TrimEnd(new[] { '.' });
            if (cleaned.Length > 64) cleaned = cleaned.Substring(0, 64).Trim();
            return cleaned;
        }

        public static bool Exists(string name)
        {
            string normalized = NormalizeName(name);
            return !normalized.NullOrEmpty() && File.Exists(PathFor(normalized));
        }

        public static bool Save(string name, NovelSeedsSettings settings, out string error)
        {
            error = null;
            string normalized = NormalizeName(name);
            if (normalized.NullOrEmpty())
            {
                error = "Enter a profile name.";
                return false;
            }
            if (settings == null)
            {
                error = "No active configuration is available.";
                return false;
            }

            Directory.CreateDirectory(ProfileDirectory);
            bool saved = SaveToPath(PathFor(normalized), settings, out error);
            if (saved) Refresh();
            else Log.Error("Horticulture - Novel Seeds could not save configuration profile '" + normalized + "': " + error);
            return saved;
        }

        public static bool ExportPublisherDefault(NovelSeedsSettings settings, out string error)
        {
            if (settings == null)
            {
                error = "No active configuration is available.";
                return false;
            }

            Directory.CreateDirectory(PublisherDirectory);
            bool saved = SaveToPath(PublisherExportPath, settings, out error);
            if (!saved) Log.Error("Horticulture - Novel Seeds could not export the publisher default configuration: " + error);
            return saved;
        }

        public static bool ApplyDefault(NovelSeedsSettings destination, out bool usedBundledDefault, out string error)
        {
            usedBundledDefault = HasBundledDefault;
            if (!usedBundledDefault)
            {
                destination.ResetAll();
                error = null;
                return true;
            }

            bool loaded = LoadFromPath(BundledDefaultPath, destination, out error);
            if (!loaded) Log.Error("Horticulture - Novel Seeds could not load the bundled default configuration: " + error);
            return loaded;
        }

        public static bool LocalSettingsExist(string modIdentifier, string modHandleName)
        {
            if (modIdentifier.NullOrEmpty() || modHandleName.NullOrEmpty()) return false;
            string path = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "Mod_" + modIdentifier + "_" + modHandleName + Extension);
            return File.Exists(path);
        }

        public static bool OpenPublisherDirectory(out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(PublisherDirectory);
                Application.OpenURL(new Uri(PublisherDirectory).AbsoluteUri);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool SaveToPath(string path, NovelSeedsSettings settings, out string error)
        {
            error = null;
            string temporaryPath = path + ".tmp";
            try
            {
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!directory.NullOrEmpty()) Directory.CreateDirectory(directory);
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                NovelSeedsSettings snapshot = settings;
                Scribe.saver.InitSaving(temporaryPath, "HorticultureNovelSeedsProfile");
                Scribe_Deep.Look(ref snapshot, "settings");
                Scribe.saver.FinalizeSaving();
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
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

        public static bool Load(SettingsProfileInfo profile, NovelSeedsSettings destination, out string error)
        {
            if (profile == null || destination == null || !File.Exists(profile.Path))
            {
                error = "The selected profile is unavailable.";
                return false;
            }
            bool loaded = LoadFromPath(profile.Path, destination, out error);
            if (!loaded) Log.Error("Horticulture - Novel Seeds could not load configuration profile '" + profile.Name + "': " + error);
            return loaded;
        }

        internal static bool LoadFromPath(string path, NovelSeedsSettings destination, out string error)
        {
            error = null;
            try
            {
                NovelSeedsSettings loaded = null;
                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref loaded, "settings");
                Scribe.loader.FinalizeLoading();
                if (loaded == null)
                {
                    error = "The profile did not contain a configuration.";
                    return false;
                }
                destination.ApplyFrom(loaded);
                return true;
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                error = exception.Message;
                return false;
            }
        }
        public static bool Delete(SettingsProfileInfo profile, out string error)
        {
            error = null;
            if (profile == null) return false;
            try
            {
                if (File.Exists(profile.Path)) File.Delete(profile.Path);
                Refresh();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Log.Error("Horticulture - Novel Seeds could not delete configuration profile '" + profile.Name + "': " + exception);
                return false;
            }
        }

        private static string PathFor(string normalizedName)
        {
            return System.IO.Path.Combine(ProfileDirectory, FilePrefix + normalizedName + Extension);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    public class Dialog_SaveSettingsProfile : Window, IHorticultureInputDialogSurface
    {
        private readonly NovelSeedsSettings settings;
        private string profileName = string.Empty;
        private string errorText;
        private HorticultureInputDialogDocument canvasDocument;

        public override Vector2 InitialSize => new Vector2(520f, 250f);

        public Dialog_SaveSettingsProfile(NovelSeedsSettings settings)
        {
            this.settings = settings;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            canvasDocument = new HorticultureInputDialogDocument(this, "hns.settings-profile.save");
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
            Save();
        }

        private void Save()
        {
            string normalized = SettingsProfileManager.NormalizeName(profileName);
            if (normalized.NullOrEmpty())
            {
                errorText = "Enter a profile name.";
                return;
            }
            if (SettingsProfileManager.Exists(normalized))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Replace the saved configuration '" + normalized + "'?", delegate { SaveNow(normalized); }, true));
                return;
            }
            SaveNow(normalized);
        }

        private void SaveNow(string normalized)
        {
            if (SettingsProfileManager.Save(normalized, settings, out string error))
            {
                Messages.Message("Configuration saved as '" + normalized + "'.", MessageTypeDefOf.TaskCompletion, false);
                Close();
            }
            else errorText = "Could not save configuration: " + error;
        }

        string IHorticultureInputDialogSurface.Title => "Save Configuration Profile";
        string IHorticultureInputDialogSurface.Description => "Name a portable snapshot of the current Horticulture settings. Existing profile names require confirmation before replacement.";
        string IHorticultureInputDialogSurface.FieldLabel => "Profile name";
        string IHorticultureInputDialogSurface.Value { get => profileName; set => profileName = value ?? string.Empty; }
        string IHorticultureInputDialogSurface.ValidationMessage => errorText;
        string IHorticultureInputDialogSurface.PrimaryLabel => "Save profile";
        Action IHorticultureInputDialogSurface.PrimaryAction => Save;
        string IHorticultureInputDialogSurface.SecondaryLabel => string.Empty;
        Action IHorticultureInputDialogSurface.SecondaryAction => null;
        void IHorticultureInputDialogSurface.Close() => Close();
    }
}
