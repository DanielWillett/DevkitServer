# Bundle Color Provider

This is the default color provider which uses the vanilla `Charts.unity3d` file in the base map folder.

This feature is enabled by using the `BundledStripChartColorProvider` color provider.

The chart package should have two textures in it and be exported using the Bundle Tool in Unity.

* `Height_Strip.png`
* `Layer_Strip.png`

## Height Strip
The height strip is a `1 x n` image where the first pixel is the water color and the rest of the pixels create a color gradient based on the terrain height from sea level.

Example:

```
[blue (water)] [yellow (sand)] [dark green] [dark green] [dark green] [green] [green] [green] [light green] [light green] [light green]
```

## Layer Strip
The layer strip is a `1 x 32` image used to define the colors of specific object types.
Each pixel corresponds to the layer of the object, resource, etc. on the chart.

### Relevant 0-based pixel indices:

These are the main layers that you'll see. There are others that aren't likely to be used and can be left blank or purple.

| Layer           | X-position (from zero) | Description                       |
| --------------- | ---------------------- | --------------------------------- |
| Highway*        | 0                      | Large concrete roads (>8m wide)   |
| Road*           | 1                      | Small concrete roads (<= 8m wide) |
| Street*         | 2                      | Object roads (city blocks, etc)   |
| Path*           | 3                      | Non-concrete roads				   |
| Cliff*          | 4                      | Boulders, cliffs, etc			   |
| RESOURCE        | 14                     | Other resources				   |
| LARGE           | 15                     | Large objects					   |
| MEDIUM          | 16                     | Medium objects					   |
| SMALL           | 17                     | Small objects					   |
| BARRICADE       | 27                     | Buildable barricades			   |
| STRUCTURE       | 28                     | Buildable structures			   |

\* These aren't actually layers but just special values used.