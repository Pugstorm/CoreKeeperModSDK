using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;

public readonly partial struct MyAspectEFE : global::Unity.Entities.IAspect, global::Unity.Entities.IAspectCreate<MyAspectEFE>
{
	/// <summary>
	/// Construct an instance of the enclosing aspect from all required data references.
	/// </summary>
	public MyAspectEFE(global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData> myaspectefe_dataRef)
	{
		this.Data = myaspectefe_dataRef;
	}

	/// <summary>
	/// Create an instance of the enclosing aspect struct pointing at a specific entity's components data.
	/// </summary>
	/// <param name="entity">The entity to create the aspect struct from.</param>
	/// <param name="systemState">The system state from which data is extracted.</param>
	/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
	public MyAspectEFE CreateAspect(global::Unity.Entities.Entity entity, ref global::Unity.Entities.SystemState systemState)
	{
		var lookup = new Lookup(ref systemState);
		return lookup[entity];
	}

	/// <summary>
	/// Add component requirements from this aspect into all archetype lists.
	/// </summary>
	/// <param name="all">Archetype "all" component requirements.</param>
	public void AddComponentRequirementsTo(ref global::Unity.Collections.LowLevel.Unsafe.UnsafeList<global::Unity.Entities.ComponentType> all)
	{
		var allRequiredComponentsInAspect =
			new global::Unity.Collections.LowLevel.Unsafe.UnsafeList<global::Unity.Entities.ComponentType>(initialCapacity: 8, allocator: global::Unity.Collections.Allocator.Temp, options: global::Unity.Collections.NativeArrayOptions.ClearMemory)
			{
				global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData>(),
			};
		global::Unity.Entities.Internal.InternalCompilerInterface.MergeWith(ref all, ref allRequiredComponentsInAspect);
		allRequiredComponentsInAspect.Dispose();
	}
	/// <summary>
	/// Get the number of required (i.e. non-optional) components contained in this aspect.
	/// </summary>
	/// <returns>The number of required (i.e. non-optional) components contained in this aspect.</returns>
	public static int GetRequiredComponentTypeCount() => 1;
	/// <summary>
	/// Add component requirements from this aspect into the provided span.
	/// </summary>
	/// <param name="componentTypes">The span to which all required components in this aspect are added.</param>
	public static void AddRequiredComponentTypes(ref global::System.Span<global::Unity.Entities.ComponentType> componentTypes)
	{
		componentTypes[0] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData>();		
	}

	/// <summary>
	/// A container type that provides access to instances of the enclosing Aspect type, indexed by <see cref="Unity.Entities.Entity"/>.
	/// Equivalent to <see cref="global::Unity.Entities.ComponentLookup{T}"/> but for aspect types.
	/// Constructed from an system state via its constructor.
	public struct Lookup : global::Unity.Entities.Internal.InternalCompilerInterface.IAspectLookup<MyAspectEFE>
	{
		global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData> MyAspectEFE_DataCAc;

		/// <summary>
		/// Create the aspect lookup from an system state.
		/// </summary>
		/// <param name="state">The system state to create the aspect lookup from.</param>
		public Lookup(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE_DataCAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData>(false);
		}

		/// <summary>
		/// Update the lookup container.
		/// Must be called every frames before using the lookup.
		/// </summary>
		/// <param name="state">The system state the aspect lookup was created from.</param>
		public void Update(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE_DataCAc.Update(ref state);
		}

		/// <summary>
		/// Get an aspect instance pointing at a specific entity's components data.
		/// </summary>
		/// <param name="entity">The entity to create the aspect struct from.</param>
		/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
		public MyAspectEFE this[global::Unity.Entities.Entity entity]
		{
			get
			{
				return new MyAspectEFE(this.MyAspectEFE_DataCAc.GetRefRW(entity));
			}
		}
	}

	/// <summary>
	/// Chunk of the enclosing aspect instances.
	/// the aspect struct itself is instantiated from multiple component data chunks.
	/// </summary>
	public struct ResolvedChunk
	{
		/// <summary>
		/// Chunk data for aspect field 'MyAspectEFE.Data'
		/// </summary>
		public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData> MyAspectEFE_DataNaC;

		/// <summary>
		/// Get an aspect instance pointing at a specific entity's component data in the chunk index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns>Aspect for the entity in the chunk at the given index.</returns>
		public MyAspectEFE this[int index]
			=> new MyAspectEFE(new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData>(this.MyAspectEFE_DataNaC, index));

		/// <summary>
		/// Number of entities in this chunk.
		/// </summary>
		public int Length;
	}

	/// <summary>
	/// A handle to the enclosing aspect type, used to access a <see cref="ResolvedChunk"/>'s components data in a job.
	/// Equivalent to <see cref="Unity.Entities.ComponentTypeHandle{T}"/> but for aspect types.
	/// Constructed from an system state via its constructor.
	/// </summary>
	public struct TypeHandle
	{
		global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData> MyAspectEFE_DataCAc;

		/// <summary>
		/// Create the aspect type handle from an system state.
		/// </summary>
		/// <param name="state">System state to create the type handle from.</param>
		public TypeHandle(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE_DataCAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData>(false);
		}

		/// <summary>
		/// Update the type handle container.
		/// Must be called every frames before using the type handle.
		/// </summary>
		/// <param name="state">The system state the aspect type handle was created from.</param>
		public void Update(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE_DataCAc.Update(ref state);
		}

		/// <summary>
		/// Get the enclosing aspect's <see cref="ResolvedChunk"/> from an <see cref="global::Unity.Entities.ArchetypeChunk"/>.
		/// </summary>
		/// <param name="chunk">The ArchetypeChunk to extract the aspect's ResolvedChunk from.</param>
		/// <returns>A ResolvedChunk representing all instances of the aspect in the chunk.</returns>
		public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk chunk)
		{
			ResolvedChunk resolved;
			resolved.MyAspectEFE_DataNaC = chunk.GetNativeArray(ref this.MyAspectEFE_DataCAc);
			resolved.Length = chunk.Count;
			return resolved;
		}
	}

	/// <summary>
	/// Enumerate the enclosing aspect from all entities in a query.
	/// </summary>
	/// <param name="query">The entity query to enumerate.</param>
	/// <param name="typeHandle">The aspect's enclosing type handle.</param>
	/// <returns>An enumerator of all the entities instance of the enclosing aspect.</returns>
	public static Enumerator Query(global::Unity.Entities.EntityQuery query, TypeHandle typeHandle) { return new Enumerator(query, typeHandle); }

	/// <summary>
	/// Enumerable and Enumerator of the enclosing aspect.
	/// </summary>
	public struct Enumerator : global::System.Collections.Generic.IEnumerator<MyAspectEFE>, global::System.Collections.Generic.IEnumerable<MyAspectEFE>
	{
	    ResolvedChunk                                _Resolved;
	    global::Unity.Entities.Internal.InternalEntityQueryEnumerator _QueryEnumerator;
	    TypeHandle                                   _Handle;
	    internal Enumerator(global::Unity.Entities.EntityQuery query, TypeHandle typeHandle)
	    {
	        _QueryEnumerator = new global::Unity.Entities.Internal.InternalEntityQueryEnumerator(query);
	        _Handle = typeHandle;
	        _Resolved = default;
	    }

	    /// <summary>
	    /// Dispose of this enumerator.
	    /// </summary>
	    public void Dispose() { _QueryEnumerator.Dispose(); }

	    /// <summary>
	    /// Move to next entity.
	    /// </summary>
	    /// <returns>if this enumerator has not reach the end of the enumeration yet. Current is valid.</returns>
	    public bool MoveNext()
	    {
	        if (_QueryEnumerator.MoveNextHotLoop())
	            return true;
	        return MoveNextCold();
	    }

	    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	    bool MoveNextCold()
	    {
	        var didMove = _QueryEnumerator.MoveNextColdLoop(out var chunk);
	        if (didMove)
	            _Resolved = _Handle.Resolve(chunk);
	        return didMove;
	    }

	    /// <summary>
	    /// Get current entity aspect.
	    /// </summary>
	    public MyAspectEFE Current {
	        get {
	            #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
	                _QueryEnumerator.CheckDisposed();
	            #endif
	                return _Resolved[_QueryEnumerator.IndexInChunk];
	            }
	    }

	    /// <summary>
	    /// Get the Enumerator from itself as a Enumerable.
	    /// </summary>
	    /// <returns>An Enumerator of the enclosing aspect.</returns>
	    public Enumerator GetEnumerator()  { return this; }

	    void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotImplementedException();
	    object global::System.Collections.IEnumerator.Current => throw new global::System.NotImplementedException();
	    global::System.Collections.Generic.IEnumerator<MyAspectEFE> global::System.Collections.Generic.IEnumerable<MyAspectEFE>.GetEnumerator() => throw new global::System.NotImplementedException();
	    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=> throw new global::System.NotImplementedException();
	}

	/// <summary>
	/// Completes the dependency chain required for this aspect to have read access.
	/// So it completes all write dependencies of the components, buffers, etc. to allow for reading.
	/// </summary>
	/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
	public void CompleteDependencyBeforeRO(ref global::Unity.Entities.SystemState state)
	{
		state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData>();
	}

	/// <summary>
	/// Completes the dependency chain required for this component to have read and write access.
	/// So it completes all write dependencies of the components, buffers, etc. to allow for reading,
	/// and it completes all read dependencies, so we can write to it.
	/// </summary>
	/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
	public void CompleteDependencyBeforeRW(ref global::Unity.Entities.SystemState state)
	{
		state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData>();
	}
}

public readonly partial struct MyAspectEFE2 : global::Unity.Entities.IAspect, global::Unity.Entities.IAspectCreate<MyAspectEFE2>
{
	/// <summary>
	/// Construct an instance of the enclosing aspect from all required data references.
	/// </summary>
	public MyAspectEFE2(global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData2> myaspectefe2_dataRef)
	{
		this.Data = myaspectefe2_dataRef;
	}

	/// <summary>
	/// Create an instance of the enclosing aspect struct pointing at a specific entity's components data.
	/// </summary>
	/// <param name="entity">The entity to create the aspect struct from.</param>
	/// <param name="systemState">The system state from which data is extracted.</param>
	/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
	public MyAspectEFE2 CreateAspect(global::Unity.Entities.Entity entity, ref global::Unity.Entities.SystemState systemState)
	{
		var lookup = new Lookup(ref systemState);
		return lookup[entity];
	}

	/// <summary>
	/// Add component requirements from this aspect into all archetype lists.
	/// </summary>
	/// <param name="all">Archetype "all" component requirements.</param>
	public void AddComponentRequirementsTo(ref global::Unity.Collections.LowLevel.Unsafe.UnsafeList<global::Unity.Entities.ComponentType> all)
	{
		var allRequiredComponentsInAspect =
			new global::Unity.Collections.LowLevel.Unsafe.UnsafeList<global::Unity.Entities.ComponentType>(initialCapacity: 8, allocator: global::Unity.Collections.Allocator.Temp, options: global::Unity.Collections.NativeArrayOptions.ClearMemory)
			{
				global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData2>(),
			};
		global::Unity.Entities.Internal.InternalCompilerInterface.MergeWith(ref all, ref allRequiredComponentsInAspect);
		allRequiredComponentsInAspect.Dispose();
	}
	/// <summary>
	/// Get the number of required (i.e. non-optional) components contained in this aspect.
	/// </summary>
	/// <returns>The number of required (i.e. non-optional) components contained in this aspect.</returns>
	public static int GetRequiredComponentTypeCount() => 1;
	/// <summary>
	/// Add component requirements from this aspect into the provided span.
	/// </summary>
	/// <param name="componentTypes">The span to which all required components in this aspect are added.</param>
	public static void AddRequiredComponentTypes(ref global::System.Span<global::Unity.Entities.ComponentType> componentTypes)
	{
		componentTypes[0] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData2>();		
	}

	/// <summary>
	/// A container type that provides access to instances of the enclosing Aspect type, indexed by <see cref="Unity.Entities.Entity"/>.
	/// Equivalent to <see cref="global::Unity.Entities.ComponentLookup{T}"/> but for aspect types.
	/// Constructed from an system state via its constructor.
	public struct Lookup : global::Unity.Entities.Internal.InternalCompilerInterface.IAspectLookup<MyAspectEFE2>
	{
		global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData2> MyAspectEFE2_DataCAc;

		/// <summary>
		/// Create the aspect lookup from an system state.
		/// </summary>
		/// <param name="state">The system state to create the aspect lookup from.</param>
		public Lookup(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE2_DataCAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData2>(false);
		}

		/// <summary>
		/// Update the lookup container.
		/// Must be called every frames before using the lookup.
		/// </summary>
		/// <param name="state">The system state the aspect lookup was created from.</param>
		public void Update(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE2_DataCAc.Update(ref state);
		}

		/// <summary>
		/// Get an aspect instance pointing at a specific entity's components data.
		/// </summary>
		/// <param name="entity">The entity to create the aspect struct from.</param>
		/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
		public MyAspectEFE2 this[global::Unity.Entities.Entity entity]
		{
			get
			{
				return new MyAspectEFE2(this.MyAspectEFE2_DataCAc.GetRefRW(entity));
			}
		}
	}

	/// <summary>
	/// Chunk of the enclosing aspect instances.
	/// the aspect struct itself is instantiated from multiple component data chunks.
	/// </summary>
	public struct ResolvedChunk
	{
		/// <summary>
		/// Chunk data for aspect field 'MyAspectEFE2.Data'
		/// </summary>
		public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData2> MyAspectEFE2_DataNaC;

		/// <summary>
		/// Get an aspect instance pointing at a specific entity's component data in the chunk index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns>Aspect for the entity in the chunk at the given index.</returns>
		public MyAspectEFE2 this[int index]
			=> new MyAspectEFE2(new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData2>(this.MyAspectEFE2_DataNaC, index));

		/// <summary>
		/// Number of entities in this chunk.
		/// </summary>
		public int Length;
	}

	/// <summary>
	/// A handle to the enclosing aspect type, used to access a <see cref="ResolvedChunk"/>'s components data in a job.
	/// Equivalent to <see cref="Unity.Entities.ComponentTypeHandle{T}"/> but for aspect types.
	/// Constructed from an system state via its constructor.
	/// </summary>
	public struct TypeHandle
	{
		global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData2> MyAspectEFE2_DataCAc;

		/// <summary>
		/// Create the aspect type handle from an system state.
		/// </summary>
		/// <param name="state">System state to create the type handle from.</param>
		public TypeHandle(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE2_DataCAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData2>(false);
		}

		/// <summary>
		/// Update the type handle container.
		/// Must be called every frames before using the type handle.
		/// </summary>
		/// <param name="state">The system state the aspect type handle was created from.</param>
		public void Update(ref global::Unity.Entities.SystemState state)
		{
			this.MyAspectEFE2_DataCAc.Update(ref state);
		}

		/// <summary>
		/// Get the enclosing aspect's <see cref="ResolvedChunk"/> from an <see cref="global::Unity.Entities.ArchetypeChunk"/>.
		/// </summary>
		/// <param name="chunk">The ArchetypeChunk to extract the aspect's ResolvedChunk from.</param>
		/// <returns>A ResolvedChunk representing all instances of the aspect in the chunk.</returns>
		public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk chunk)
		{
			ResolvedChunk resolved;
			resolved.MyAspectEFE2_DataNaC = chunk.GetNativeArray(ref this.MyAspectEFE2_DataCAc);
			resolved.Length = chunk.Count;
			return resolved;
		}
	}

	/// <summary>
	/// Enumerate the enclosing aspect from all entities in a query.
	/// </summary>
	/// <param name="query">The entity query to enumerate.</param>
	/// <param name="typeHandle">The aspect's enclosing type handle.</param>
	/// <returns>An enumerator of all the entities instance of the enclosing aspect.</returns>
	public static Enumerator Query(global::Unity.Entities.EntityQuery query, TypeHandle typeHandle) { return new Enumerator(query, typeHandle); }

	/// <summary>
	/// Enumerable and Enumerator of the enclosing aspect.
	/// </summary>
	public struct Enumerator : global::System.Collections.Generic.IEnumerator<MyAspectEFE2>, global::System.Collections.Generic.IEnumerable<MyAspectEFE2>
	{
	    ResolvedChunk                                _Resolved;
	    global::Unity.Entities.Internal.InternalEntityQueryEnumerator _QueryEnumerator;
	    TypeHandle                                   _Handle;
	    internal Enumerator(global::Unity.Entities.EntityQuery query, TypeHandle typeHandle)
	    {
	        _QueryEnumerator = new global::Unity.Entities.Internal.InternalEntityQueryEnumerator(query);
	        _Handle = typeHandle;
	        _Resolved = default;
	    }

	    /// <summary>
	    /// Dispose of this enumerator.
	    /// </summary>
	    public void Dispose() { _QueryEnumerator.Dispose(); }

	    /// <summary>
	    /// Move to next entity.
	    /// </summary>
	    /// <returns>if this enumerator has not reach the end of the enumeration yet. Current is valid.</returns>
	    public bool MoveNext()
	    {
	        if (_QueryEnumerator.MoveNextHotLoop())
	            return true;
	        return MoveNextCold();
	    }

	    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	    bool MoveNextCold()
	    {
	        var didMove = _QueryEnumerator.MoveNextColdLoop(out var chunk);
	        if (didMove)
	            _Resolved = _Handle.Resolve(chunk);
	        return didMove;
	    }

	    /// <summary>
	    /// Get current entity aspect.
	    /// </summary>
	    public MyAspectEFE2 Current {
	        get {
	            #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
	                _QueryEnumerator.CheckDisposed();
	            #endif
	                return _Resolved[_QueryEnumerator.IndexInChunk];
	            }
	    }

	    /// <summary>
	    /// Get the Enumerator from itself as a Enumerable.
	    /// </summary>
	    /// <returns>An Enumerator of the enclosing aspect.</returns>
	    public Enumerator GetEnumerator()  { return this; }

	    void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotImplementedException();
	    object global::System.Collections.IEnumerator.Current => throw new global::System.NotImplementedException();
	    global::System.Collections.Generic.IEnumerator<MyAspectEFE2> global::System.Collections.Generic.IEnumerable<MyAspectEFE2>.GetEnumerator() => throw new global::System.NotImplementedException();
	    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=> throw new global::System.NotImplementedException();
	}

	/// <summary>
	/// Completes the dependency chain required for this aspect to have read access.
	/// So it completes all write dependencies of the components, buffers, etc. to allow for reading.
	/// </summary>
	/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
	public void CompleteDependencyBeforeRO(ref global::Unity.Entities.SystemState state)
	{
		state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData2>();
	}

	/// <summary>
	/// Completes the dependency chain required for this component to have read and write access.
	/// So it completes all write dependencies of the components, buffers, etc. to allow for reading,
	/// and it completes all read dependencies, so we can write to it.
	/// </summary>
	/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
	public void CompleteDependencyBeforeRW(ref global::Unity.Entities.SystemState state)
	{
		state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData2>();
	}
}
