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

The release also carries a validated `1.6/AutoMasks` bundle. Resolution order is authoritative manual,
validated local generated/promoted, validated bundled, then explicit local generation. Missing-mask work is
finite and visible in a long event; ordinary rendering never performs texture readbacks or generation. The
developer publisher in `DevTools/Publish-AutoMaskBundle.ps1` regenerates and validates a real in-game bundle
through DevBridge2 coordination, then emits the XML, manifest, low-confidence count, and failure report.

The mask painter keeps the existing Plant/Produce pages and three semantic channels while adding a fast
manual workflow: Add/Remove/Replace brushes, connected-region modifiers, grow/shrink/smooth/feather commands,
island and hole cleanup, smart edge expansion, channel locks, original/mask/final previews, validation issue
overlays, and copy or alpha-bounds projection between discovered texture variations. These tools use the
existing mask records and undo history; old mask files and renderer behavior remain compatible.

Projection confidence and area normalization
---------------------------------------------
Projection matching scores use position 30%, color 20%, area 15%, shape 15%, adjacency 10%, and connectivity
10%. Channel confidence is `0.25*expectedRecall + 0.25*assignedPrecision + 0.15*spatialAgreement +
0.20*semanticAgreement + 0.075*conflictFree + 0.075*ambiguityFree`. Every term and the result is finite and
clamped to 0..1; absent channels, channels with no expected transformed pixels, and channels with zero final
assignments are exactly zero. Global unmasked coverage is a displayed diagnostic only and never contributes to
confidence. Component area shares use one union of all visible source mask pixels and one alpha-visible target
domain, so split semantic regions do not stretch their own bounds or inflate confidence.

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

Knowledge Framework integration
-------------------------------
Novel Seeds consumes the additive Knowledge Framework API from
`https://github.com/lanwoodall423/Knowledge-Framework` at API generation 3. The validated
framework dependency is commit `be6b13a05323fe29902bdb9cf92a0d62cb96e1c8`
(release-manifest parent `ebcfba76764bd569fbfb51227c5a70ce02f9b6a4`, API implementation
parent `d90fbccce98a4bdab59d3f2f84dbe7c15b22301dd`), with
`KnowledgeFramework.dll` SHA-256 `33552DBEC78E0E777C3E074EA0EBA8F750629E879EA8A809B58D54AEFB1E71C0`.
Required capabilities are typed measurements, evidence transactions, claims, contexts,
witness learning, milestones, structural relations, consumer migration, domain aliases,
readiness inspection, safe registration, registration ownership, and targeted invalidation.
UI, structured comparison, and filtered transmission are optional; when absent, the
integration keeps neutral UI/comparison behavior and continues core cultivation behavior.

The integration registers `lan.horticulture.novelseeds.plants` only after framework readiness,
uses non-replacing registration, rejects foreign ownership, and keeps gameplay framework calls
behind `HorticultureKnowledgeAdapter`; internal registration and migration helpers use only the
supported consumer and alias APIs. The legacy `plants` domain is a permanent canonical alias.
Migration is versioned and retries incomplete imports without clearing serialized legacy data;
save/reload is idempotent. Consumers never inspect a framework game component or build global
schemas themselves.

Plant knowledge event routing uses bounded semantic identities based on plant, cultivar,
cycle, batch, parent, and documentation data rather than object references or ticks alone.
Duplicate hooks are deduplicated at the integration boundary while legitimate harvest cycles
remain distinct. The canonical supported-plant policy includes all sowable trees and excludes
non-sowable decorative or wild-only plants. Cultivar registration invalidates the cultivar,
species, parents, and directly related subjects instead of the whole domain during normal play.
Developer diagnostics expose registration state, framework compatibility, submitted and
deduplicated event counts, rejected plants, targeted/broad invalidations, and subject counts.
The repository-owned `knowledge` runtime scenario measures event identity, registration,
personal/colony observations, witness-capable routing, and targeted invalidation from a real
quicktest map. Duplicate submissions use the same semantic identity and separate harvest cycles
use separate cycle identities.
These are measurements rather than progression guarantees: personal/colony amounts,
familiarity, claim confidence, stage, expertise, and witness effects are reported before and
after controlled submissions. Horticulture's repository-owned runtime suite records these
values from a real quicktest map.
The only intentional progression correction is collapsing discovery and its parent-lineage
evidence into one Knowledge Framework transaction; event weights and cultivation modifiers are
otherwise unchanged.

Cross-repository release gate
-----------------------------
The validated compatibility pair is Knowledge Framework commit
`be6b13a05323fe29902bdb9cf92a0d62cb96e1c8` (release-manifest parent
`ebcfba76764bd569fbfb51227c5a70ce02f9b6a4`, API implementation parent
`d90fbccce98a4bdab59d3f2f84dbe7c15b22301dd`,
`3.1.0-beta.1`, API generation 3) and the
Horticulture Release DLL listed below. The current shipped Framework DLL and the exact local
Framework build have the same SHA-256, so the shipped-framework and exact-framework rows are
the same tested binary. No tagged or released older Framework artifact exists in the repository;
no older binary compatibility claim is made. Framework API/capability mismatch, unavailable
readiness, and foreign-domain cases are tested as safe-failure paths: no partial registration,
foreign replacement, or unsafe query is allowed. Legacy `plants` migration is versioned,
retryable, and clears serialized input only after a complete successful import.

The release assembly excludes the runtime test assembly, synthetic test Defs, and all
coordinator implementation code. Horticulture owns the runtime scenarios; DevBridge2 only
coordinates RimWorld generations, readiness, and shared test leases. Runtime outcomes and
exact result paths are recorded in `docs/RUNTIME_TESTS.md`.
Rollback requires restoring the preceding Horticulture commit and its paired Framework DLL;
never mix release assemblies from different compatibility pairs.

Optional integrations
---------------------
Novel Seeds remains usable when optional integrations are unavailable. The Horticulture runtime
suite records the Knowledge Framework release/API/hash it actually observes and reports an
incompatible framework as a release-blocking failure rather than claiming a pass.

Release artifact policy
-----------------------
The authoritative RimWorld 1.6 artifact is `1.6/Assemblies/HorticultureNovelSeeds.dll`, built with
`dotnet build Source/HorticultureNovelSeeds.csproj --configuration Release`. Runtime test builds are
disposable and excluded from the release package. See [docs/TESTING.md](docs/TESTING.md) for validation.
