﻿# JSON Chart Color Provider

Create a JSON file to supply colors to the chart image instead of `Charts.unity3d`.

This feature is enabled by using the `JsonChartColorProvider` color provider.

This file can be located at the following places:
* [Map]/Editor/chart_colors.json
* [Map]/Editor/Charts.json
* [Map]/chart_colors.json
* [Map]/Charts.json

The default configuration file looks like this which exactly matches Washington's configuration.
```jsonc
{
  "$schema": "https://raw.githubusercontent.com/DanielWillett/DevkitServer/master/Module/Schemas/chart_colors_schema.json",

  /*
   *  Various colors for different types of assets.
   */

  // Anything below sea level.
  "water_color": "#305a59",

  // Concrete spline roads >= 8m in width.
  "highway_color": "#e37728",

  // Concrete spline roads < 8m in width.
  "road_color": "#d9a236",

  // Level objects defined as roads.
  "object_road_color": "#bfbfbf",

  // Non-concrete spline roads.
  "path_color": "#8f846f",

  // Level objects defined as cliffs (usually cliff rocks).
  "cliff_color": "#7f7f7f",

  // Resources like trees, etc.
  "resource_color": "#5e4a36",

  // Default for 'LARGE' level objects.
  "object_large_color": "#5a5a5a",

  // Default for 'MEDIUM' level objects.
  "object_medium_color": "#787878",

  // Default color for invalid layers, etc. This usually won't be used.
  "fallback_color": "#ff00ff",

  /*
   *  Defines keyframes on an RGB graph.
   *  
   *  'interpolation_mode' can be either 'Constant', 'Linear', or 'Smooth' and defines how colors are estimated between keyframes.
   *    - Constant defines a keyframe that continues as the same value until the next keyframe.
   *    - Linear defines a keyframe that linearly interpolates between itself and the next keyframe.
   *    - Smooth defines a keyframe that uses a 'ease in and out' interpolation between itself and the next keyframe.
   *
   *  'height_unit' can be either 'Relative' or 'Absolute' and defines how 'height' is interpreted.
   *    - Relative is from [0, 1] where 0 is sea level and 1 is the max terrain height, 256m, or the top of the cartography volume if it exists.
   *    - Absolute is a Y-value in world coordinates. 
   */
  "height_color_graph": [
    // The first element is usually sand/rock for the beaches.
    {
      "height": 0,
      "color": "#777777",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },

    // the remaining elements are usually the grass color getting lighter as height increases
    {
      "height": 0.0322580598,
      "color": "#55714e",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },
    {
      "height": 0.161290303,
      "color": "#5a7752",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },
    {
      "height": 0.290322602,
      "color": "#5e7d56",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },
    {
      "height": 0.419354796,
      "color": "#64845b",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },
    {
      "height": 0.516129017,
      "color": "#66875c",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },
    {
      "height": 0.645161271,
      "color": "#6d9063",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },
    {
      "height": 0.774193525,
      "color": "#729768",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    },
    {
      "height": 0.935483873,
      "color": "#7ea075",
      "interpolation_mode": "Constant",
      "height_unit": "Relative"
    }
  ]
}
```

The `height_color_graph` is used to define a graph of height against color.

The heights may have to be lowered significantly to see a major difference on maps that are more flat.

To get the best topographical look, it's recommended to use Constant interpolation with somewhat major color differences between each step.