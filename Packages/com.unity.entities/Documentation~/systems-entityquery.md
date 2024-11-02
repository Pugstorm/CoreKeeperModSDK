---
uid: systems-entityquery
---

# Query data with EntityQuery

An [`EntityQuery`](xref:Unity.Entities.EntityQuery) finds [archetypes](concepts-archetypes.md) that have a specified set of component types. It then gathers the archetype's chunks into an array which a system can process. 

|**Topic**|**Description**|
|---|---|
|[EntityQuery overview](systems-entityquery-intro.md)|Understand what an EntityQuery is.|
|[EntityQuery filters](systems-entityquery-filters.md)|Use EntityQuery filters to sort information.|
|[Write groups](systems-write-groups.md)|Use write groups for one system to overwrite another, even when you can't change the other system. |
|[Version numbers (change filtering)](systems-version-numbers.md)|Use version numbers to detect potential changes and to implement efficient optimization strategies.|

## Additional resources

* [Iterate over data](systems-iterating-data-intro.md)
* [Archetype concepts](concepts-archetypes.md)
