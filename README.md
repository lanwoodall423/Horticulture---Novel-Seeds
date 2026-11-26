# Adaptive Cultivation: upright-render regression proof

[Animated proof](adaptive-cultivation-upright-proof.gif) | [Native garden](corrected-mature-garden.png) | [Before / after](orientation-before-after.png) | [Phenotype inspector](corrected-phenotype-inspector.png)

Source: 6b9719e3502ac3667e24d69d04da01e99a5a2fce. Native run: f60942e5ea704b05bf0be6ad2091a682. Result: PASS. Corrected direct texture sampling and mandatory orientation regression. The montage uses raw game captures and native GPU readbacks, cropped and enlarged with labels; nothing was rotated or painted. The before row is from the preceding run, not the same random individual. The new fixture compares each expressed specimen against the native source and its vertically inverted reference. Actual resource delivery and harvest are pawn jobs; growth comparisons are fixture-advanced.

- [Orientation regression table](orientation-regression.tsv)
- [Runtime inventory](adaptive-inventory.tsv)
- [Receipt](test-receipt.json)
- [Original game screenshots](original-captures)
- [Native GPU readbacks](native-gpu-readbacks)
