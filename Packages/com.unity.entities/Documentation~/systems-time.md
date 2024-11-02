# Time considerations

A world controls the value of the [`Time`](xref:Unity.Entities.ComponentSystemBase.Time) property of [systems](concepts-systems.md) within it. A system's `Time` property is an alias for the current world time.

By default, Unity creates a [`TimeData`](xref:Unity.Core.TimeData) entity for each world, which an [`UpdateWorldTimeSystem`](xref:Unity.Entities.UpdateWorldTimeSystem) instance updates. This reflects the elapsed time since the previous frame.

Systems in the [`FixedStepSimulationSystemGroup`](xref:Unity.Entities.FixedStepSimulationSystemGroup) treat time differently than other system groups. Systems in the fixed step simulation group update at a fixed interval, instead of once at the current delta time, and might update more than once per frame if the fixed interval is a small enough fraction of the frame time.

If you need more control of time in a world, you can use [`World.SetTime`](ref:Unity.Entities.World.SetTime(Unity.Core.TimeData)) to specify a time value directly. You can also [`PushTime`](xref:Unity.Entities.World.PushTime(Unity.Core.TimeData)) to temporarily change the world time and [`PopTime`](xref:Unity.Entities.World.PopTime) to return to the previous time (in a time stack).