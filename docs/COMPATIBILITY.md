# Compatibility policy

Novel Seeds targets RimWorld 1.6 and the following required dependencies:

| Dependency | Package ID | Policy |
| --- | --- | --- |
| Harmony | `brrainz.harmony` | Required for patches. |
| Knowledge Framework | `lan.knowledgeframework` | Required for cultivar knowledge registration and discovery evidence. |
| Progression: Agriculture | `ferny.progressionagriculture` | Required for the supported seed and progression flow. |

## Supported plant shape

The automatic policy covers conventional sowable plant definitions, including sowable trees. A plant is eligible when its definition exposes the normal sowing/growth/harvest or cutting path used by RimWorld and the plant policy accepts it. Decorative, non-sowable, and wild-only definitions are not claimed as fully supported.

| Content type | Compatibility claim | Notes |
| --- | --- | --- |
| Vanilla sowable crops | Supported | Mutation, cultivar selection, harvest identity, produce inheritance, and registry paths are tested. |
| Vanilla sowable trees | Supported | Tree mutation, cutting, replanting, and lineage paths are tested. |
| Conventional modded crops | Automatic support intended | The normal plant/harvest definitions are scanned; unusual product or graphic rules may need work. |
| Conventional modded sowable trees | Automatic support intended | Same caveat as other modded plants. |
| Custom planting menus | Conditional | The standard growing-zone path is supported; a custom menu may need an integration. |
| Custom harvesting or cutting | Conditional | Novel Seeds needs the custom action to route the normal plant event. |
| Custom graphics or masks | Conditional | Manual masks remain authoritative; unusual texture layouts may require a manual mask or compatibility rule. |
| Custom product-generation systems | Conditional | Standard harvested products are supported; custom products may not carry inherited data automatically. |

A universal plant-mod compatibility promise is not a release claim. A mod may load without errors while still requiring an integration for one of these custom systems.

## Load order and safe failure

Harmony, Knowledge Framework, and Progression: Agriculture are dependencies and are loaded first. The optional load-after entries in `About/About.xml` cover known plant/content integrations but do not turn those mods into required dependencies.

Unknown or unsupported plant definitions are rejected by the plant policy without registering partial Knowledge data. Missing cultivar IDs, missing masks, stale automatic-mask records, and unavailable optional integration details resolve to safe empty/fallback states.

## Tested set for this candidate

The RC matrix covers the RimWorld 1.6 vanilla quicktest plant set, sowable trees, the installed Knowledge Framework API generation 3, Progression: Agriculture, Harmony, and the repository's loaded conventional plant definitions. Custom planting, harvest, graphic, and product systems remain conditional and require manual confirmation with the specific mod list.
