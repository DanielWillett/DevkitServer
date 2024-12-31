# Compositor Pipelines
Basically, cartography render presets.

They allow you to define a specific configuration and render them by pressing a button when needed.
These could be used for textures in mod items, stylized charts for holidays, and many more.
I also have plans to expand them more in the future.

![image](https://github.com/user-attachments/assets/9d8da631-da88-4916-b0db-0ec3ead4e754)

Any JSON files that are valid pipeline files will show up on the left side of your screen in the pause menu.

Each button can be left-clicked to begin rendering and compositing the image, or right-clicked to open the pipeline file.

Any changes to existing files or new files created will update within a few seconds in-game.

## Format

The bare minimum format requires a type parameter defining which base image to use. This can either be `Satellite` (GPS), `Chart`, or `None`. If you choose `None`, you can supply a background color instead.

The `type` property is how the UI knows whether or not to recognize a JSON file as a pipeline file so it must be present. The `$schema` just helps your editor recommend properties and perform error-checking.

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/DanielWillett/DevkitServer/refs/heads/master/Module/Schemas/cartography_compositor_pipeline_schema.json",

  // 'Satellite', 'Chart', or 'None'
  "type": "Satellite"
}
```

| Property | Type | Description |
| ---- | --- | -- |
| `type`| Text (CartographyType) | The type of map to use as the base image. |
| `name` | Text | Overrides the name displayed in-game. By default it displays the file name. |
| `output_file` | Text (File Path) | Specifies where the image should be placed on your computer when it finished rendering. This defaults to the Editor folder in the map. |
| `auto_open` | Boolean | If this is set to `true`, the image will automatically open in your default image viewer when it's finished rendering. |
| `time` | Text (HH:MM) | The time of day (where 6 AM is sunrise and 6 PM is sunset) the image should be rendered. This matters most on satellite renders. |
| `background_color` | Text (Hex Color) | If `type` is `None`, this is the starting color of the image compositors work on. |
| `chart_color_provider` | Text or Object | If `type` is `Chart`, this is the color provider used to supply colors to the chart renderer. |
| `compositors` | List of Text or Object | Ordered list of type names or objects like: `{ "type": "< type name >", "other config...": "etc" }` which are used to apply effects to the base image. |
| `chart_type_overrides` | Dictionary | List of chart color overrides for specific object `GUID`s or road material names. Values should be `WATER`, `CLIFF`, `ROAD`, etc. | 

## Example file
```jsonc
{
  "$schema": "https://raw.githubusercontent.com/DanielWillett/DevkitServer/refs/heads/master/Module/Schemas/cartography_compositor_pipeline_schema.json",

  // 'Satellite', 'Chart', or 'None'
  "type": "Chart",

  // Display name
  "name": "Example Pipeline",

  // Override the output file, in this case overriding the Map.png image (assuming this file is in a folder under the map folder, like Editor)
  "output_file": "../Map.png",

  // the image will open when it's done
  "auto_open": true,

  // noon (this doesn't really do anything for chart rendering)
  "time": "12:00 PM"

  // apply a green background color (this also doesn't do anything for chart rendering)
  "background_color": "99ffcc",

  // Pull from the JSON Chart Colors (see docs) file. You can also just supply a type directly like: "chart_color_provider": "JsonChartColorProvider".
  "chart_color_provider":
  {
    "type": "JsonChartColorProvider",
    "file": "../example_chart_colors.json"
  },

  // Use a specific overlay file. You could also just list "compositors": [  "OverlayCartographyCompositor"  ] and let it find the images automatically.
  "compositors":
  [
    {
      "type": "OverlayCartographyCompositor",
      "image": "../Overlays/overlay.png"
    }
  ],

  // Change the 'Racetrack' road to a STREET chart color instead of a highway (it's default). You could also put an object's GUID in place of Racetrack
  // valid values: "GROUND", "IGNORE", "HIGHWAY", "ROAD", "STREET", "PATH", "LARGE", "MEDIUM", "WATER", "CLIFF"
  "chart_type_overrides": {
    "Racetrack": "STREET"
  }
}
```