# Botanical Expression production beta - persistence qualification
Native 1.7.5014 rev1195, AllPerformance loadout, source 035f219cf0785a2997efabfc10336ddc01dcde0f, isolated run d2accb41f8264f53887b91e2653ddb73.

PASS all five stages, including normal full-colony SaveGame and LoadGame. The earlier timeout was a GUI repaint dependency in the fixture's queued synchronous save event. The test now queues a textless, hidden loading event, without changing serialization.
- 300 re-created plant instances matched ID, cultivar, traits, growth history, seed, growth and map coordinates. Three reloaded cultivar records matched actual lineage and traits.
- 243 modified plants matched native mesh records before/after, excluding material object identity from comparison. 15/15 representative before/after native GPU PNGs were byte-identical.
- 18,601,649-byte isolated colony save. SHA-256 recorded in save evidence. No actual colony save is included in this evidence repository.
- 16 shared materials, 8 source maps, 5,107,712 texture bytes before and after. Max source preparation time 168.34 ms. Update intervals were measured under paused test conditions, not gameplay FPS.
- GIFs use 120 native GPU readbacks, not screen images or AI-generated art. The readback uses actual live mesh growth, seed, bias, and environment, with sway disabled. Native screenshot API did not write files during the background test.
- Growth stages were explicitly advanced; event probabilities and slot rolls controlled for inheritance. The comparison cultivars were assigned; the three sibling cultivars were actually crossbred.
- Normal config, preferences and saves unchanged. Test process stopped and package removed. Beta remains opt-in.
