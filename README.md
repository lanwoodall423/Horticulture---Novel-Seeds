# Horticulture - complete standing-plant visual catalog

Actual RimWorld 1.7.5014 rev1195 captures, isolated AllPerformance run
1fa9a2511a834db2aea32f76f36accd8, source commit
f89c6a92b9f394c2bba54851998c4bab3753b79c.

[Open the full contact sheet](catalog-gallery.jpg). For individual readable
plots use the links below; each has the exact cultivar labels and art tiers.
The first plant in each row is an unmodified control.

| Labeled plot | Content | Raw, unedited screenshot |
|---|---|---|
| [1](catalog-plot-1.jpg) | Size, hardiness, labor | [PNG](panel-1.png) |
| [2](catalog-plot-2.jpg) | Climate, resources, habitat | [PNG](panel-2.png) |
| [3](catalog-plot-3.jpg) | Resins, beauty, medicinal and exceptional cosmetic traits | [PNG](panel-3.png) |
| [4](catalog-plot-4.jpg) | Nutrition +5% to +75% | [PNG](panel-4.png) |
| [5](catalog-plot-5.jpg) | Nutrition +80% to +100%, companion synergy, produce colors | [PNG](panel-5.png) |
| [6](catalog-plot-6.jpg) | Leaf and stem colors, combined genetics | [PNG](panel-6.png) |

[Exact per-plant genetics and renderer inventory](garden-inventory.tsv) |
[Complete appearance catalog (872 loaded definitions)](trait-appearance-catalog.tsv).

108 plants, 90 styled cases, 18 controls. Every concrete authored trait
was tested individually; family roots are configuration entries rather
than cultivars. All 20 nutrition strengths, five synergy stat types, 15
representative color alleles and five combined genetic cases were rendered.
All 65 cases expected to use a procedural motif reached the GPU; 64 were
mask-free and one combined regional pigment with a procedural motif.
Art tiers are not new rarity probabilities. The screenshot sheets crop
and label rows from actual captures; no plant pixels were retouched.
Some incidental map items partially obscure plants. Habitat/resource
traits were assigned for visual review, not tested in their natural
environments. The shader remains experimental, and item rendering,
designer previews, natural breeding and performance need qualification.
All nine stages passed. Normal saves, preferences and configuration
were preserved, and the test package/process were removed.