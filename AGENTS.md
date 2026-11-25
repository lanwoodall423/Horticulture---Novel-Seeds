# Content Mod Development

This repository is a RimWorld content mod. Preserve the existing 1.6 release.

For the 1.7 / Waylith development branch, use the native Innovations-style
workflow: .NET 10 build, standalone/source regressions, exact commit-stamped
artifact, guarded package installation and isolated RimWorld Quicktest.
RimTest/RimLiaison is optional for this branch, not a prerequisite. Keep its
existing catalogs for future integration.

Default routine tests use the Innovations AllPerformance loadout and native
UltraBoost where applicable. See docs/PROJECTNEXUS.md and
DevTools/Get-NativeTestLoadout.ps1. Use Minimal to diagnose optimizer effects.

Never launch or deploy while another RimWorld instance is running. Do not
close the player's game, alter ordinary saves/configuration/preferences,
or manually overwrite the live mod. Only stop the test process that the
runner itself created and whose identity is verified. Runtime logs are
shared by this engine, so concurrent sessions are not supported.

Artifact freshness, actual loaded package identities, genuine runtime
evidence and safe cleanup must be proven before runtime results are accepted.
A loadout preflight or static regression is not a gameplay or shader pass.
Do not claim 1.7 support before native qualification. See docs/NATIVE17.md for the test-only packaging and runner; do not use the legacy 1.6 release builder on this branch.
