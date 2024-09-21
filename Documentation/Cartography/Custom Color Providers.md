# Creating a custom IChartColorProvider

Plugins can define custom color providers.

They must implement one of the following types:

| Type                          | Use Case                                                                                        |
| ----------------------------- | ----------------------------------------------------------------------------------------------- |
| `RaycastChartColorProvider`   | Provides an API for working with the results of ray casts directly, using Unity Jobs for speed. |
| `ISamplingChartColorProvider` | Provides an API for simply returning a color for each pixel given it's world position.          |
| `IFullChartColorProvider`     | Provides an API for capturing the entire image in a fully custom way.                           |

The provider uses the `bool TryInitialize` function to decide whether or not the provider can be used.

This could be by checking for the presence of a file, etc.

Note that when using `RaycastChartColorProvider` it's important to call `base.TryInitialize` if you're overriding it.

An `isExplicitlyDefined` parameter says whether or not the `cartography_config.json` file explicitly asked for this provider.

