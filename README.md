Horticulture - Novel Seeds

Plants can naturally mutate, developing unique traits that can alter appearance and behavior. A mutant plant will have a gizmo upon maturity, "Save Seeds". This allows a mutant plant to be named and saved as a permanent Variety.

New discoveries are intentionally rare, growing alongside your colony as another avenue of progression.

Features


🌱 Plant Mutations

Plants have a rare chance to develop mutations, granting them one or more unique traits.

Traits range from simple improvements to entirely new mechanics.

Examples
Giant — Larger plants that produce increased harvests.
Dwarf — Smaller plants with reduced yield.
Bioluminescent — Plants emit a colored glow.
Resinous (Joyresin) — Harvesting the plant without gloves exposes the grower to a psychoactive resin.
Aquatic — Alters where the plant can grow.

Most traits affect both gameplay and the plant's appearance.


🌾 Varieties

When a mutated plant reaches maturity, you can Save Seeds to permanently unlock that exact genetic combination as a Variety.

Once unlocked, a variety can be planted at any time without managing separate seed items.

Examples include:

Giant Cotton
Blue Bioluminescent Glow Pods
Aquatic Dwarf Tomatoes
Medicinal High-Yield Healroot


🌿 Wild Varieties

Wild plants begin with the Wild trait.

During world generation, there is a configurable chance that naturally occurring plants spawn as unique wild varieties.

These naturally evolved plants are extremely rare by default, rewarding exploration and collection.


🌸 Cross-Pollination

Different varieties can naturally cross-pollinate, producing entirely new hybrid varieties.

Cross-pollination is intentionally uncommon.


🍅 Produce Inheritance

Harvested produce inherits characteristics directly from its parent variety without generating duplicate ThingDefs.

Inherited properties can include:

Visual appearance
Color
Nutritional values
Trait effects
Chemical compounds
Consumable effects, including when cooked into meals

Different produce varieties remain distinct and do not stack together.

Products made from produce receive their traits. Clothing made from blue cloth is blue. Products made from multiple inherited colors use pigment-style subtractive blending rather than an RGB average.

This feature can be disabled if preferred.

🎨 Dynamic Coloring

Every playthrough generates unique color ranges for each plant species.

The ranges are derived deterministically from the world seed, saved with the game, and remain stable after reloading. Palette size, hue range, saturation/value limits, and restricted or unrestricted species colors can be configured in the mod settings. Existing saves generate their missing palettes safely when loaded.

For example:

Cotton may naturally produce red and purple varieties in one colony.
The same plant might instead produce blue and white varieties in another.

Each species receives its own unique palette every game, making every colony's flora feel different.

Customization
⚙️ Extensive Mod Configuration

Nearly every aspect of the mod can be customized.

Settings are available at three levels:

Global
Plant Group
Individual Plant

Most systems allow you to:

Enable or disable features
Adjust generation weights
Fine-tune behavior
Override settings at any level

Save/Load settings presets

Whether you want only cosmetic mutations or a complex breeding experience, the system is designed to be highly configurable.

🌿 Plant Groups

Plants can be organized into custom groups to simplify configuration.

Examples include:

Grains
Vegetables
Flowers
Trees
Medicinal Plants

Traits can then be enabled or disabled:

Globally
Per plant group
Per individual plant

For example, you could disable all Hardiness traits for the Grains group while allowing Wheat to opt back in individually.

🏷️ Automatic Plant Tags

Plants can automatically receive tags such as:

Edible
Medicinal
Material
Decorative
Animal Feed

The tagging system can automatically scan all loaded plants based on their definitions and harvested products.

Tags can determine whether inherited produce and product traits have an effect.

Tags can be rebuilt:

Globally
Individually per tag
Visual Customization


🎨 Trait Visual Designer

Every visual trait includes a powerful visual designer.

Customize how each trait appears by adjusting numerous visual properties, with settings available at the:

Global level
Plant Group level
Individual Plant level

Traits can affect color, shape, and add special visual effects.

This allows complete control over the appearance of every mutation.


🖌️ Visual Masks

The visual designer includes support for layered masking.

Masks can be painted for different portions of a plant, including:

Produce
Leaves
Stems

For example, you can make only the tomatoes on a tomato plant blue while leaving the foliage unchanged.

Manual masks for vanilla and supported modded plants remain authoritative. When a loaded plant
texture has no painted manual mask, Novel Seeds generates and caches a Produce, Leaves, and Stem
fallback from the source texture. The mask editor labels automatic results, flags uncertain masks,
and can promote an automatic mask to the existing editable/manual format. Automatic Stem tracing follows narrow,
root-connected branches through sparse junctions while rejecting dense canopy and groundcover regions. Tree metadata
can request a conservative second trace but cannot bypass the 30% structural credibility limit when Leaves compete with
Stem. A sprite with no eligible foliage can remain entirely structural. Tree morphology
comes from tree-category metadata rather than harvest type, and palette-only Produce matches at the root are rejected;
ambiguous pixels remain unmasked.

Automatic cache entries are per discovered texture variant and include the source mod, texture content,
state references, produce signature, and generator version. Generation runs outside drawing; render-time
lookups use validated cached records and derived textures/materials. Low-confidence automatic records stay
available for inspection and manual correction but apply no semantic recoloring until promoted. Runtime and
preview recoloring share an HSV/value-preserving transform that keeps sprite shading and attenuates changes
on very dark outline pixels. Diagnostic overlays use red for foliage, green for produce/flowers, and blue
for stems/branches.

The mask painter keeps the existing Plant/Produce pages and three semantic channels while adding a fast
manual workflow: Add/Remove/Replace brushes, connected-region modifiers, grow/shrink/smooth/feather commands,
island and hole cleanup, smart edge expansion, channel locks, original/mask/final previews, validation issue
overlays, and copy or alpha-bounds projection between discovered texture variations. These tools use the
existing mask records and undo history; old mask files and renderer behavior remain compatible.

With Dev mode enabled, **Horticulture - Novel Seeds > Plant 10x10 random varieties** activates a map
tool that fills the clicked 10x10 footprint with mature plants. Species are shuffled without replacement,
so every available species appears once before any base plant repeats; later passes remain balanced to within
one plant. A random active cultivar is then chosen for each species occurrence. On a fresh save, the tool can
create one `DEV grid` cultivar for up to 100 random growable species, matching the grid capacity. Existing
plants are replaced; buildings, non-growing terrain, and map boundaries are left untouched and reported as
skipped cells. Species whose own special spawn rules reject the clicked terrain are replaced by another
least-used species for that grid.

Traits with visual effects can be assigned to specific masks, allowing highly detailed customization.

Compatibility

Novel Seeds is designed with compatibility in mind.

Plant definitions from both vanilla RimWorld and supported mods are automatically scanned and categorized based on their properties, allowing the mutation system to integrate with a wide variety of custom crops without requiring manual setup.

Progression: Agriculture's seed unlock system is used and is required. 
It should be compatible with all plant mods. Existing manual masks always take priority; missing
growth-stage, collection, and directional masks use the persistent automatic fallback.

Deferred Reality dependency provenance
---------------------------------------
The optional Deferred Reality Horticulture adapter is built from the external
`DeferredRealityFramework/Source/Adapters/Horticulture/DeferredRealityHorticultureAdapter.cs`
and `DeferredReality.Horticulture.csproj` sources. The adapter targets .NET Framework 4.8,
RimWorld 1.6, and the Deferred Reality Framework API; it is included to provide the
regional wild-flora provider and does not own Horticulture cultivar state.

The currently vendored `1.6/Assemblies/DeferredReality.Horticulture.dll` is expected to have
SHA-256 `72404BD24D154C0760E59AAA35E43F326334541FB2F020AE331126331EBA736F`.
Its assembly metadata version is `0.0.0.0`.
The external source repository was observed at commit `1c8acdf2197bb440e053477457505fd50c0e7382`,
but its worktree was dirty and the exact binary build provenance could not be independently
reproduced. This remains a release blocker until the external source revision and build are
pinned to the vendored hash. The main Horticulture project does not directly reference this
adapter binary; the external adapter references HorticultureNovelSeeds and DeferredRealityFramework.
