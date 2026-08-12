# Default configuration

These are the clean-install defaults for the RimWorld 1.6 release candidate. The bundled `1.6/Defaults/DefaultConfiguration.xml` is kept in sync with the code constants and is applied to new settings and full resets.

| Setting | Default | Meaning |
| --- | ---: | --- |
| Spontaneous mutation chance | 8% | Chance used when a normal sown plant receives a mutation roll. |
| Cross-pollination chance | 0.7% | Chance checked when a plant is sown and eligible nearby donors exist. |
| Wild mutation chance | 0.5% | Chance for an eligible naturally occurring plant to begin as a wild discovery. |
| Minimum donor growth | 50% | Nearby donors must be at least this mature, healthy, sown, and able to grow. |
| Second cross-pollination trait chance | 10% | Roll for the second mechanical trait slot after a valid donor trait is selected. |
| Later cross-pollination trait chance | 1% | Roll for each later mechanical trait slot. |
| Maximum new traits per mutation | 3 | Upper bound on mechanical traits added by one mutation event. |
| Maximum mechanical traits per cross | 3 | Upper bound on mechanical traits inherited by one cross. |
| Produce visuals | Enabled | Harvested produce may carry the cultivar's configured visual data. |
| Trait balancing | Enabled | Trait selection considers the resulting positive/negative balance. |
| Balance strength | 75% | Strength of the balancing preference. |

Cross-pollination colors are separate from the mechanical trait budget. The rate is checked at sowing; nearby cultivars influence donor selection, not the global chance itself.

Plant, group, profile, and trait overrides are applied after these global defaults. Existing saves retain their serialized values; a full reset restores the table above.
