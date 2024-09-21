# Cartography
Cartography (chart/satellite baking) has been completely replaced in DevkitServer.
The outcome is still identical to vanilla with default settings but it is much more streamlined.

You will immediately notice that, depending on your system, charts can render around 6x faster than in vanilla. Satellite renders are about the same.

## Overlays
Both Chart and Satellite rendering can apply overlays to the output image after it's done.
Overlays will be searched for in the following places:

* [Map]/Editor/Overlays/...
* [Map]/Editor/Overlay/...
* [Map]/Overlays/...
* [Map]/Overlay/...
* [Map]/Editor/[named files]\*
* [Map]/Chart/[named files]\*
* [Map]/Terrain/[named files]\*
* [Map]/[named files]\*

\* Any locations that say 'named files' require that the file name contains the word 'overlay' in any casing.

They can be `jpg`/`jpeg`, or `png` and any size, although it's recommended to use a factor of the map image's size.

If the file name includes 'chart' or 'satellite' (case insensitive), it will only be used on the corresponding image.

This functionality is enabled by the `OverlayCartographyCompositor` compositor (see below).

## Cartography Config
A configuration file can be created that further customizes the image generation behavior.
It is looked for in the following places:
* [Map]/Editor/Editor/cartography_config.json
* [Map]/Editor/Editor/cartography.json
* [Map]/Editor/Chart/cartography_config.json
* [Map]/Editor/Chart/cartography.json
* [Map]/Editor/Terrain/cartography_config.json
* [Map]/Editor/Terrain/cartography.json
* [Map]/Editor/cartography_config.json
* [Map]/Editor/cartography.json

The default configuration file looks like this. Any property can be removed and the default value will be used.
```jsonc
{
  "$schema": "https://raw.githubusercontent.com/DanielWillett/DevkitServer/master/Module/Schemas/cartography_config_schema.json",

  /*
   *  Overrides the chart color provider to use when rendering charts.
   *  These types can be added by plugin types that implement the IChartColorProvider interface.
   *
   *  In vanilla you can either use 'BundledStripChartColorProvider' or 'JsonChartColorProvider'.
   *  By default the first successfully initialized provider is applied in the order of their Priority attributes.
   */
  "override_chart_color_provider": "BundledStripChartColorProvider",

  /*
   *  Provides an override ordered list of compositors to apply after rendering charts.
   *  These types can be added by plugin types that implement the ICartographyCompositor interface.
   *
   *  In vanilla you can only use 'OverlayCartographyCompositor'.
   *  By default all compositors are applied sorted by their Priority attributes.
   */
  "override_active_compositors": [
    "OverlayCartographyCompositor"
  ],

  /*
   *  Allows overriding specific object, resource, and road chart types
   *  without having to change their .dat files.
   */
  "chart_type_overrides": { 
    "cc906876e40f49ab948924b0e457a45d": "IGNORE", // ignore Airport_1 on charts
    "Path_1": "HIGHWAY"                           // setup the road material 'Path_1' to show as a highway on charts
  }
}
```

For more information on chart color providers see 'Charts.md' and 'JSON Chart Colors.md'.