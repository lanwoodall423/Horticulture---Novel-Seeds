#if HNS_NATIVE_FIXTURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using LudeonTK;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // An actual graphics-mode probe; no player settings or assets are touched.
    [StaticConstructorOnStartup]
    internal static class NativeShaderProbe
    {
        private static readonly Dictionary<int, PlantVisualParameters> visuals = new();
        private static Action<string> onStage;
        private static Action<string, string> finish;
        private static string capturePath;
        private static int frames;
        private static bool requested;
        private static bool active;
        private static Map map;
        private static readonly List<Plant> probePlants = new();
        private static Material fieldMaterial;
        private static readonly HashSet<int> maskedPlantIds = new();
        internal static bool TryGetVisual(Plant plant, out PlantVisualParameters visual) =>
            visuals.TryGetValue(plant.thingIDNumber, out visual);

        internal static void Begin(string root, Action<string> stage, Action<string, string> completed)
        {
            onStage = stage; finish = completed;
            try
            {
                WaylithPhenotypeShader.Enabled = true;
                PixelProbe(root);
                onStage("ShaderPixelParity");
                MaskProbe(root);
                onStage("ShaderMaskParity");
                FallbackProbe();
                onStage("ShaderFallback");
                map = Find.CurrentMap;
                Require(map != null, "current map");
                // Choose a supported vanilla nonseasonal crop with no shared manual
                // layers. Disable only automatic masks in this disposable run.
                ThingDef def = null;
                foreach (string name in new[] { "Plant_Corn", "Plant_Rice", "Plant_Potato", "Plant_Strawberry" })
                {
                    var option = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (option == null) continue;
                    var record = HorticultureNovelSeedsMod.Settings.GetPlantSettings(option, true);
                    bool prior = record.disableAutoPlantMasks;
                    record.disableAutoPlantMasks = true;
                    if (!PlantMaskUtility.HasActiveMasks(option)) { def = option; break; }
                    record.disableAutoPlantMasks = prior;
                }
                Require(def != null, "a mask-free fixture crop");
                ThingDef maskedDef = null;
                int prepared = 0;
                foreach (string name in new[] { "Plant_Corn", "Plant_Strawberry", "Plant_Rice", "Plant_Cotton", "Plant_Healroot", "Plant_Psychoid" })
                {
                    var option = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (option == null || option == def || !PlantMaskUtility.HasActiveMasks(option)) continue;
                    var normal = Enumerable.Range(0, PlantMaskUtility.VariationCount(option)).Where(i => PlantMaskUtility.VariationLabel(option, i).StartsWith("Normal", StringComparison.Ordinal)).ToArray();
                    if (normal.Length == 0) continue;
                    int count = normal.Count(i => WaylithPhenotypeShader.PrepareMask(option, i));
                    if (count != normal.Length) continue;
                    maskedDef = option; prepared = count; break;
                }
                Require(maskedDef != null && prepared > 0, "stable resolved crop masks");
                var origin = map.mapPawns.FreeColonistsSpawned.First().Position;
                var start = GenRadial.RadialCellsAround(origin, 35, true).First(c =>
                    Enumerable.Range(0, 12).All(i => Available(c + new IntVec3(i % 3 * 2, 0, i / 3 * 2))));
                var color = PlantVisualParameters.Default;
                color.tintRed = .94f; color.tintGreen = .16f; color.tintBlue = .83f;
                color.saturation = 1.25f;
                for (int i = 0; i < 12; i++)
                {
                    var plant = (Plant)ThingMaker.MakeThing(i < 6 ? def : maskedDef);
                    plant.sown = true; plant.Growth = i < 6 && i % 3 == 2 ? .45f : 1f;
                    GenSpawn.Spawn(plant, start + new IntVec3(i % 3 * 2, 0, i / 3 * 2), map);
                    if (i < 3 || (i >= 6 && i < 9)) continue; // vanilla comparison rows
                    if (i >= 9) maskedPlantIds.Add(plant.thingIDNumber);
                    probePlants.Add(plant); visuals.Add(plant.thingIDNumber, color);
                    plant.DirtyMapMesh(map);
                }
                var mat = probePlants[0].Graphic.MatSingleFor(probePlants[0]);
                Require(WaylithPhenotypeShader.TryGet(mat, color, out fieldMaterial), "crop shader material");
                Require(ReferenceEquals(fieldMaterial.mainTexture, mat.mainTexture), "base texture retained");
                Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
                Find.Selector.ClearSelection();
                Find.CameraDriver.JumpToCurrentMapLoc(start + new IntVec3(2,0,3));
                Find.CameraDriver.SetRootSize(10f);
                capturePath = Path.Combine(root, "HNS-shader-field.png");
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                active = true;
                Log.Message("[HNS ShaderProbe] field crop=" + def.defName + "; whole=3; masked=" + maskedDef.defName + "; styledMasked=3; vanilla=6; preparedMasks=" + prepared);
            }
            catch (Exception e) { End("FAIL", e.ToString()); }
        }

        private static bool Available(IntVec3 c) => c.InBounds(map) && c.Standable(map) &&
            c.GetPlant(map) == null && c.GetEdifice(map) == null && c.GetZone(map) == null && !c.Fogged(map);

        internal static void Update()
        {
            if (!active) return;
            try
            {
                frames++;
                if (!requested && frames >= 90 && probePlants.All(p => WaylithPhenotypeShader.PrintedPlants.Contains(p.thingIDNumber)) && maskedPlantIds.All(id => WaylithPhenotypeShader.MaskedPlants.Contains(id)))
                {
                    Require(WaylithPhenotypeShader.MaterialCount <= WaylithPhenotypeShader.MaterialLimit, "bounded shader materials");
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    if (Find.WindowStack.TryRemove(typeof(EditWindow_Log), false)) { frames = 0; return; }
                    ScreenCapture.CaptureScreenshot(capturePath);
                    requested = true;
                    Log.Message("[HNS ShaderProbe] requested field capture; materials=" + WaylithPhenotypeShader.MaterialCount +
                        "; sway=" + fieldMaterial.GetFloat("_SwayHead"));
                }
                if (requested && File.Exists(capturePath) && new FileInfo(capturePath).Length > 1000)
                {
                    onStage("ShaderField");
                    End("PASS", "GPU pixel parity, packed mask parity, guarded fallback, field material and screenshot");
                }
                else if (frames > 900) throw new InvalidOperationException("field print or screenshot timeout");
            }
            catch (Exception e) { End("FAIL", e.ToString()); }
        }

        private static void PixelProbe(string root)
        {
            const int size = 64;
            var swatches = new[] { new Color(.15f,.72f,.24f,1), new Color(.82f,.32f,.12f,1),
                new Color(.07f,.09f,.06f,1), new Color(.9f,.2f,.9f,.05f) };
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = swatches[(x >= size/2 ? 1 : 0) + (y >= size/2 ? 2 : 0)];
            tex.SetPixels(pixels); tex.Apply();
            var source = new Material(ShaderDatabase.CutoutPlant) { mainTexture = tex, renderQueue = 2900 };
            source.SetFloat("_FallBehaviorEnabled", 0f);
            var neutral = PlantVisualParameters.Default;
            var variant = neutral; variant.tintRed = .85f; variant.tintGreen = .25f; variant.tintBlue = .78f;
            variant.hueShift = .13f; variant.saturation = 1.15f; variant.brightness = .93f; variant.contrast = 1.08f; variant.dullness = .17f;
            Require(WaylithPhenotypeShader.TryGet(source, neutral, out var baseline), "neutral shader material");
            Require(WaylithPhenotypeShader.TryGet(source, variant, out var styled), "styled shader material");
            Require(WaylithPhenotypeShader.TryGet(source, variant, out var cached) && ReferenceEquals(cached, styled), "shader material cache hit");
            Require(ReferenceEquals(tex, styled.mainTexture), "shared base texture");
            var original = Render(tex, baseline, Path.Combine(root, "HNS-shader-neutral.png"));
            var changed = Render(tex, styled, Path.Combine(root, "HNS-shader-styled.png"));
            float maxError = 0, maxChange = 0;
            var unmatched = new List<Color>(swatches.Take(3));
            int cutouts = 0;
            for (int i = 0; i < original.Length; i++)
            {
                var before = original[i];
                var after = changed[i];
                // Locate the original swatch rather than assuming framebuffer Y convention.
                int match = unmatched.FindIndex(c => Distance(c, before) < .045f);
                if (match >= 0)
                {
                    unmatched.RemoveAt(match);
                    var expected = PlantVisualColorUtility.Apply(before, variant.tintRed, variant.tintGreen,
                        variant.tintBlue, variant.hueShift, variant.saturation, variant.brightness,
                        variant.contrast, variant.opacity, variant.dullness);
                    maxError = Math.Max(maxError, Distance(expected, after));
                    maxChange = Math.Max(maxChange, Distance(before, after));
                }
                else if (Distance(before, Color.black) < .045f && Distance(after, Color.black) < .045f) cutouts++;
            }
            Log.Message("[HNS ShaderProbe] swatches unmatched=" + unmatched.Count + "; cutouts=" + cutouts +
                "; maxError=" + maxError.ToString("0.000") + "; maxChange=" + maxChange.ToString("0.000") +
                "; neutral=" + string.Join(" | ", original.Select(c => c.ToString())) +
                "; styled=" + string.Join(" | ", changed.Select(c => c.ToString())));
            Require(unmatched.Count == 0 && cutouts == 1, "neutral source and cutout swatches");
            Require(maxChange > .16f && maxError <= .06f, "GPU compositor matches CPU reference");
            // Source retained by bounded material cache and wind registry for this test process.
        }

        private static Color[] Render(Texture2D source, Material material, string path)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D read = null;
            try
            {
                Graphics.Blit(source, rt, material, 0);
                RenderTexture.active = rt;
                read = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                read.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                read.Apply();
                File.WriteAllBytes(path, read.EncodeToPNG());
                return new[] { read.GetPixel(16,16), read.GetPixel(48,16), read.GetPixel(16,48), read.GetPixel(48,48) };
            }
            finally
            {
                if (read != null) UnityEngine.Object.Destroy(read);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static float Distance(Color a, Color b) => Math.Max(Math.Abs(a.r-b.r), Math.Max(Math.Abs(a.g-b.g), Math.Abs(a.b-b.b)));

        private static PlantVisualParameters[] MaskStyles()
        {
            var a = PlantVisualParameters.Default; a.tintRed = .95f; a.tintGreen = .12f; a.tintBlue = .12f;
            var b = PlantVisualParameters.Default; b.tintRed = .13f; b.tintGreen = .20f; b.tintBlue = .95f;
            var c = PlantVisualParameters.Default; c.tintRed = .95f; c.tintGreen = .85f; c.tintBlue = .12f;
            return new[] { a, b, c };
        }
        internal static void TryOverrideMaskStyles(Plant plant, ref PlantVisualParameters a, ref PlantVisualParameters b, ref PlantVisualParameters c)
        {
            if (!maskedPlantIds.Contains(plant.thingIDNumber)) return;
            var styles = MaskStyles(); a = styles[0]; b = styles[1]; c = styles[2];
        }

        private static void MaskProbe(string root)
        {
            var layers = new[] { new VisualMaskLayerRecord(), new VisualMaskLayerRecord(), new VisualMaskLayerRecord() };
            int n = VisualMaskLayerRecord.Resolution;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int channel = (x >= n / 2 ? 1 : 0) + (y < n / 2 ? 2 : 0);
                    if (channel < 3) layers[channel].PaintPixel(x, y, true);
                    if (channel == 0) layers[1].PaintPixel(x, y, true); // precedence
                }
            var mask = PackedPhenotypeMask.Create(layers);
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            tex.SetPixels(Enumerable.Repeat(new Color(.20f,.72f,.24f,1f), 64 * 64).ToArray()); tex.Apply();
            var source = new Material(ShaderDatabase.CutoutPlant) { mainTexture = tex, renderQueue = 2900 };
            source.SetFloat("_FallBehaviorEnabled", 0f);
            var styles = MaskStyles();
            Require(WaylithPhenotypeShader.TryGetMasked(source, mask, styles[0], styles[1], styles[2], out var material), "packed mask shader");
            Require(ReferenceEquals(tex, material.mainTexture) && ReferenceEquals(mask, material.GetTexture("_HnsMask")), "shared source and mask textures");
            var result = Render(tex, material, Path.Combine(root, "HNS-shader-mask.png"));
            var input = (Color)(Color32)new Color(.20f,.72f,.24f,1f);
            float error = 0;
            for (int i = 0; i < 4; i++)
            {
                var v = i < 3 ? styles[i] : PlantVisualParameters.Default;
                var expected = i == 3 ? input : PlantVisualColorUtility.Apply(input, v.tintRed, v.tintGreen,
                    v.tintBlue, v.hueShift, v.saturation, v.brightness, v.contrast, v.opacity, v.dullness);
                error = Math.Max(error, Distance(expected, result[i]));
            }
            Log.Message("[HNS ShaderProbe] packed mask maxError=" + error.ToString("0.000") +
                "; result=" + string.Join(" | ", result.Select(c => c.ToString())));
            Require(error <= .06f, "packed mask RGB/neutral/overlap/Y mapping parity");
            var transparent = styles[1]; transparent.opacity = .5f;
            Require(!WaylithPhenotypeShader.TryGetMasked(source, mask, styles[0], transparent, styles[2], out _), "masked translucency fallback");
        }

        private static void FallbackProbe()
        {
            var texture = new Texture2D(2, 2);
            var source = new Material(ShaderDatabase.CutoutPlant) { mainTexture = texture };
            source.SetFloat("_FallBehaviorEnabled", 1f);
            Require(!WaylithPhenotypeShader.TryGet(source, PlantVisualParameters.Default, out _), "seasonal source fallback");
            source.SetFloat("_FallBehaviorEnabled", 0f);
            var transparent = PlantVisualParameters.Default; transparent.opacity = .5f;
            Require(!WaylithPhenotypeShader.TryGet(source, transparent, out _), "translucency fallback");
            var other = new Material(ShaderDatabase.Cutout) { mainTexture = texture };
            Require(!WaylithPhenotypeShader.TryGet(other, PlantVisualParameters.Default, out _), "nonplant shader fallback");
            UnityEngine.Object.Destroy(source); UnityEngine.Object.Destroy(other); UnityEngine.Object.Destroy(texture);
        }

        private static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); }
        private static void End(string status, string reason)
        {
            active = false;
            WaylithPhenotypeShader.Enabled = false;
            visuals.Clear();
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            finish(status, reason);
        }
    }
}
#endif
