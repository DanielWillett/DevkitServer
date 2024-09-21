# Creating a custom ICartographyCompositor

Plugins can define custom compositors to perform post-processing on the output images for satellites and charts.

They must implement `ICartographyCompositor` and can define which map types they support using `SupportsSatellite` and `SupportsChart`.

`bool Composite` will be invoked after the image is created, returning whether or not the compositor actually made any changes (just for logging).

If no compositors are defined in the `cartography_config.json`, all available compositors will be used in order of their priority (lowest priority first so the highest priority graphics render on top).