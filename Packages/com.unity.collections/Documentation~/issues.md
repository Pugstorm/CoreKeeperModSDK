---
uid: collections-known-issues
---

# Known issues

All containers allocated with `Allocator.Temp` on the same thread use a shared `AtomicSafetyHandle` instance rather than each having their own. Most of the time, this isn't an issue because you can't pass `Temp` allocated collections into a job.

However, when you use `Native*HashMap`, `NativeParallelMultiHashMap`, `Native*HashSet`, and `NativeList` together with their secondary safety handle, this shared `AtomicSafetyHandle` instance is a problem.

A secondary safety handle ensures that a `NativeArray` which aliases a `NativeList` is invalidated when the `NativeList` is reallocated due to resizing.

Operations that invalidate an enumerator for these collection types, or invalidate the `NativeArray` that `NativeList.AsArray` returns also invalidates all other previously acquired enumerators. For example, the following throws an error when safety checks are enabled:

```c#
var list = new NativeList<int>(Allocator.Temp);
list.Add(1);

// This array uses the secondary safety handle of the list, which is
// shared between all Allocator.Temp allocations.
var array = list.AsArray();

var list2 = new NativeHashSet<int>(Allocator.Temp);

// This invalidates the secondary safety handle, which is also used
// by the list above.
list2.TryAdd(1);

// This throws an InvalidOperationException because the shared safety
// handle was invalidated.
var x = array[0];
```
