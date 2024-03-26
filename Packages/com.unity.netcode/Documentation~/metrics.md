# Metrics

There are 2 ways of gathering metrics about the netcode simulation. The simplest and most straight forward way is to use the NetDbg from the Multiplayer Menu in the Editor. This will provide you with a simple web interface to view the metrics.

The second way is to create a Singleton of type [MetricsMonitorComponent](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.MetricsMonitor.html) 
and populate it with the data points you want to monitor.

In the following example we create a singleton containing all data metrics available.

```
    var typeList = new NativeArray<ComponentType>(8, Allocator.Temp);
    typeList[0] = ComponentType.ReadWrite<MetricsMonitor>();
    typeList[1] = ComponentType.ReadWrite<NetworkMetrics>();
    typeList[2] = ComponentType.ReadWrite<SnapshotMetrics>();
    typeList[3] = ComponentType.ReadWrite<GhostNames>();
    typeList[4] = ComponentType.ReadWrite<GhostMetrics>();
    typeList[5] = ComponentType.ReadWrite<GhostSerializationMetrics>();
    typeList[6] = ComponentType.ReadWrite<PredictionErrorNames>();
    typeList[7] = ComponentType.ReadWrite<PredictionErrorMetrics>();

    var metricSingleton = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(typeList));
    FixedString64Bytes singletonName = "MetricsMonitor";
    state.EntityManager.SetName(metricSingleton, singletonName);
```

## Data Points

| component type | description |
| -------------- | ----------- |
| `NetworkMetrics` | time related network metrics |
| `SnapshotMetrics` | snapshot related network metrics |
| `GhostMetrics` | ghost related metrics - indexed using `GhostNames` |
| `GhostSerializationMetrics` | ghost serialization metrics - indexed using `GhostNames` |
| `PredictionErrorMetrics` | prediction errors - indexed using `PredictionErrorNames` |
| `GhostNames` | a list of all availeble ghosts for this simulation |
| `PredictionErrorNames` | a list of all availeble prediciton errors for this simulation |

