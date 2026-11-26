# Native map capture and full colony persistence qualification

Source e66fecf46781b9742f36fff3f1c3a7cfe6a15915; native 1.7.5014 rev1195; AllPerformance; run 7e30953639ee4e9bbce369708eaafcbc. Five stages PASS. Normal config, prefs, and saves unchanged; isolated process stopped and temporary package removed.

Eight native 1600x900 screenshots were written using ScreenCapture and a redraw request for the isolated game's own window. The 40% growth frame has an automatic debug log overlay; it is retained in map-frames but excluded from GIFs. Five other growth states (55,65,80,90,100%) and two dense-field states were visually reviewed. Growth was advanced in controlled steps, rain and rice neighbors vary. GIFs revisit captured states without synthetic intermediate frames. The closeup is a crop from the actual map images.

300 re-created plants matched IDs, genotypes, histories, seeds, growth and positions. 243/243 modified mesh records matched, excluding material object identity. 15/15 representative before/after GPU PNGs were byte-identical. Global screen UI and surroundings need not be identical. 16 shared materials, 8 source maps, 5107712 texture bytes before and after. Paused update intervals are not a gameplay FPS benchmark. The beta remains opt-in for three qualified species. Fixture-only capture; not broad release qualification.
