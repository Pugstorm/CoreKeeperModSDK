using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;

public readonly partial struct AspectSimple : global::Unity.Entities.IAspect, global::Unity.Entities.IAspectCreate<AspectSimple>
{
	/// <summary>
	/// Construct an instance of the enclosing aspect from all required data references.
	/// </summary>
	public AspectSimple(global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData> aspectsimple_dataRef)
	{
		this.Data = aspectsimple_dataRef;
	}

	/// <summary>
	/// Create an instance of the enclosing aspect struct pointing at a specific entity's components data.
	/// </summary>
	/// <param name="entity">The entity to create the aspect struct from.</param>
	/// <param name="systemState">The system state from which data is extracted.</param>
	/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
	public AspectSimple CreateAspect(global::Unity.Entities.Entity entity, ref global::Unity.Entities.SystemState systemState)
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
	public struct Lookup : global::Unity.Entities.Internal.InternalCompilerInterface.IAspectLookup<AspectSimple>
	{
		global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData> AspectSimple_DataCAc;

		/// <summary>
		/// Create the aspect lookup from an system state.
		/// </summary>
		/// <param name="state">The system state to create the aspect lookup from.</param>
		public Lookup(ref global::Unity.Entities.SystemState state)
		{
			this.AspectSimple_DataCAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData>(false);
		}

		/// <summary>
		/// Update the lookup container.
		/// Must be called every frames before using the lookup.
		/// </summary>
		/// <param name="state">The system state the aspect lookup was created from.</param>
		public void Update(ref global::Unity.Entities.SystemState state)
		{
			this.AspectSimple_DataCAc.Update(ref state);
		}

		/// <summary>
		/// Get an aspect instance pointing at a specific entity's components data.
		/// </summary>
		/// <param name="entity">The entity to create the aspect struct from.</param>
		/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
		public AspectSimple this[global::Unity.Entities.Entity entity]
		{
			get
			{
				return new AspectSimple(this.AspectSimple_DataCAc.GetRefRW(entity));
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
		/// Chunk data for aspect field 'AspectSimple.Data'
		/// </summary>
		public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData> AspectSimple_DataNaC;

		/// <summary>
		/// Get an aspect instance pointing at a specific entity's component data in the chunk index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns>Aspect for the entity in the chunk at the given index.</returns>
		public AspectSimple this[int index]
			=> new AspectSimple(new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData>(this.AspectSimple_DataNaC, index));

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
		global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData> AspectSimple_DataCAc;

		/// <summary>
		/// Create the aspect type handle from an system state.
		/// </summary>
		/// <param name="state">System state to create the type handle from.</param>
		public TypeHandle(ref global::Unity.Entities.SystemState state)
		{
			this.AspectSimple_DataCAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData>(false);
		}

		/// <summary>
		/// Update the type handle container.
		/// Must be called every frames before using the type handle.
		/// </summary>
		/// <param name="state">The system state the aspect type handle was created from.</param>
		public void Update(ref global::Unity.Entities.SystemState state)
		{
			this.AspectSimple_DataCAc.Update(ref state);
		}

		/// <summary>
		/// Get the enclosing aspect's <see cref="ResolvedChunk"/> from an <see cref="global::Unity.Entities.ArchetypeChunk"/>.
		/// </summary>
		/// <param name="chunk">The ArchetypeChunk to extract the aspect's ResolvedChunk from.</param>
		/// <returns>A ResolvedChunk representing all instances of the aspect in the chunk.</returns>
		public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk chunk)
		{
			ResolvedChunk resolved;
			resolved.AspectSimple_DataNaC = chunk.GetNativeArray(ref this.AspectSimple_DataCAc);
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
	public struct Enumerator : global::System.Collections.Generic.IEnumerator<AspectSimple>, global::System.Collections.Generic.IEnumerable<AspectSimple>
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
	    public AspectSimple Current {
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
	    global::System.Collections.Generic.IEnumerator<AspectSimple> global::System.Collections.Generic.IEnumerable<AspectSimple>.GetEnumerator() => throw new global::System.NotImplementedException();
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
namespace AspectTests
{

	public readonly partial struct Aspect2 : global::Unity.Entities.IAspect, global::Unity.Entities.IAspectCreate<Aspect2>
	{
		/// <summary>
		/// Construct an instance of the enclosing aspect from all required data references.
		/// </summary>
		public Aspect2(global::Unity.Entities.Entity aspect2_selfE, 
			global::Unity.Entities.DynamicBuffer<global::Unity.Entities.Tests.EcsIntElement> aspect2_dynamicbufferDb, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData> aspect2_dataRef, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData2> aspect2_data2Ref, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData3> aspect2_data3Ref, 
			global::Unity.Entities.RefRO<global::Unity.Entities.Tests.EcsTestData4> aspect2_dataroRef, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData5> aspect2_dataoptionalRef, 
			global::Unity.Entities.EnabledRefRO<global::Unity.Entities.Tests.EcsTestDataEnableable> aspect2_ecstestdataenableableEnref, 
			global::Unity.Entities.Tests.EcsTestSharedComp aspect2_ecstestsharedcompSc)
		{
			this.Self = aspect2_selfE;
			this.DynamicBuffer = aspect2_dynamicbufferDb;
			this.Data = aspect2_dataRef;
			this.Data2 = aspect2_data2Ref;
			this.Data3 = aspect2_data3Ref;
			this.DataRO = aspect2_dataroRef;
			this.DataOptional = aspect2_dataoptionalRef;
			this.EcsTestDataEnableable = aspect2_ecstestdataenableableEnref;
			this.EcsTestSharedComp = aspect2_ecstestsharedcompSc;
			this.NestedAspectSimple = new global::AspectSimple(aspect2_dataRef);
		}

		/// <summary>
		/// Create an instance of the enclosing aspect struct pointing at a specific entity's components data.
		/// </summary>
		/// <param name="entity">The entity to create the aspect struct from.</param>
		/// <param name="systemState">The system state from which data is extracted.</param>
		/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
		public Aspect2 CreateAspect(global::Unity.Entities.Entity entity, ref global::Unity.Entities.SystemState systemState)
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
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsIntElement>(),
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData>(),
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData2>(),
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData3>(),
					global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestData4>(),
					global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestDataEnableable>(),
					global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestSharedComp>(),
				};
			global::Unity.Entities.Internal.InternalCompilerInterface.MergeWith(ref all, ref allRequiredComponentsInAspect);
			allRequiredComponentsInAspect.Dispose();
		}
		/// <summary>
		/// Get the number of required (i.e. non-optional) components contained in this aspect.
		/// </summary>
		/// <returns>The number of required (i.e. non-optional) components contained in this aspect.</returns>
		public static int GetRequiredComponentTypeCount() => 7;
		/// <summary>
		/// Add component requirements from this aspect into the provided span.
		/// </summary>
		/// <param name="componentTypes">The span to which all required components in this aspect are added.</param>
		public static void AddRequiredComponentTypes(ref global::System.Span<global::Unity.Entities.ComponentType> componentTypes)
		{
			componentTypes[0] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsIntElement>();
				componentTypes[1] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData>();
				componentTypes[2] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData2>();
				componentTypes[3] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData3>();
				componentTypes[4] = global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestData4>();
				componentTypes[5] = global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestDataEnableable>();
				componentTypes[6] = global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestSharedComp>();			
		}

		/// <summary>
		/// A container type that provides access to instances of the enclosing Aspect type, indexed by <see cref="Unity.Entities.Entity"/>.
		/// Equivalent to <see cref="global::Unity.Entities.ComponentLookup{T}"/> but for aspect types.
		/// Constructed from an system state via its constructor.
		public struct Lookup : global::Unity.Entities.Internal.InternalCompilerInterface.IAspectLookup<Aspect2>
		{
			global::Unity.Entities.EntityStorageInfoLookup _m_Esil;
			global::Unity.Entities.BufferLookup<global::Unity.Entities.Tests.EcsIntElement> Aspect2_DynamicBufferBAc;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData> Aspect2_Data;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData2> Aspect2_Data2CAc;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData3> Aspect2_Data3CAc;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData4> Aspect2_DataROCAc;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData5> Aspect2_DataOptionalCAc;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable> Aspect2_EcsTestDataEnableableCAc;
			global::Unity.Entities.SharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp> Aspect2_EcsTestSharedCompScAc;

			/// <summary>
			/// Create the aspect lookup from an system state.
			/// </summary>
			/// <param name="state">The system state to create the aspect lookup from.</param>
			public Lookup(ref global::Unity.Entities.SystemState state)
			{
				this._m_Esil = state.GetEntityStorageInfoLookup();
				this.Aspect2_DynamicBufferBAc = state.GetBufferLookup<global::Unity.Entities.Tests.EcsIntElement>(false);
				this.Aspect2_Data = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData>(false);
				this.Aspect2_Data2CAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData2>(false);
				this.Aspect2_Data3CAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData3>(false);
				this.Aspect2_DataROCAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData4>(true);
				this.Aspect2_DataOptionalCAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData5>(false);
				this.Aspect2_EcsTestDataEnableableCAc = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable>(true);
				this.Aspect2_EcsTestSharedCompScAc = state.GetSharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp>();
			}

			/// <summary>
			/// Update the lookup container.
			/// Must be called every frames before using the lookup.
			/// </summary>
			/// <param name="state">The system state the aspect lookup was created from.</param>
			public void Update(ref global::Unity.Entities.SystemState state)
			{
				this._m_Esil.Update(ref state);
				this.Aspect2_DynamicBufferBAc.Update(ref state);
				this.Aspect2_Data.Update(ref state);
				this.Aspect2_Data2CAc.Update(ref state);
				this.Aspect2_Data3CAc.Update(ref state);
				this.Aspect2_DataROCAc.Update(ref state);
				this.Aspect2_DataOptionalCAc.Update(ref state);
				this.Aspect2_EcsTestDataEnableableCAc.Update(ref state);
				this.Aspect2_EcsTestSharedCompScAc.Update(ref state);
			}

			/// <summary>
			/// Get an aspect instance pointing at a specific entity's components data.
			/// </summary>
			/// <param name="entity">The entity to create the aspect struct from.</param>
			/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
			public Aspect2 this[global::Unity.Entities.Entity entity]
			{
				get
				{
					var chunk = this._m_Esil[entity].Chunk;
					return new Aspect2(entity, 
						this.Aspect2_DynamicBufferBAc[entity], 
						this.Aspect2_Data.GetRefRW(entity), 
						this.Aspect2_Data2CAc.GetRefRW(entity), 
						this.Aspect2_Data3CAc.GetRefRW(entity), 
						this.Aspect2_DataROCAc.GetRefRO(entity), 
						this.Aspect2_DataOptionalCAc.GetRefRWOptional(entity), 
						this.Aspect2_EcsTestDataEnableableCAc.GetEnabledRefRO<global::Unity.Entities.Tests.EcsTestDataEnableable>(entity), 
						chunk.GetSharedComponent<global::Unity.Entities.Tests.EcsTestSharedComp>(this.Aspect2_EcsTestSharedCompScAc));
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
			/// Chunk data for aspect field 'Aspect2.Self'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Entity> Aspect2_SelfNaE;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.DynamicBuffer'
			/// </summary>
			public global::Unity.Entities.BufferAccessor<global::Unity.Entities.Tests.EcsIntElement> Aspect2_DynamicBufferBa;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.Data'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData> Aspect2_DataNaC;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.Data2'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData2> Aspect2_Data2NaC;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.Data3'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData3> Aspect2_Data3NaC;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.DataRO'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData4> Aspect2_DataRONaC;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.DataOptional'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData5> Aspect2_DataOptionalNaC;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.EcsTestDataEnableable'
			/// </summary>
			public global::Unity.Entities.EnabledMask Aspect2_EcsTestDataEnableableEnm;
			/// <summary>
			/// Chunk data for aspect field 'Aspect2.EcsTestSharedComp'
			/// </summary>
			public global::Unity.Entities.Tests.EcsTestSharedComp Aspect2_EcsTestSharedCompSc;

			/// <summary>
			/// Get an aspect instance pointing at a specific entity's component data in the chunk index.
			/// </summary>
			/// <param name="index"></param>
			/// <returns>Aspect for the entity in the chunk at the given index.</returns>
			public Aspect2 this[int index]
				=> new Aspect2(this.Aspect2_SelfNaE[index],
			this.Aspect2_DynamicBufferBa[index],
			new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData>(this.Aspect2_DataNaC, index),
			new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData2>(this.Aspect2_Data2NaC, index),
			new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData3>(this.Aspect2_Data3NaC, index),
			new global::Unity.Entities.RefRO<global::Unity.Entities.Tests.EcsTestData4>(this.Aspect2_DataRONaC, index),
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData5>.Optional(this.Aspect2_DataOptionalNaC, index),
			this.Aspect2_EcsTestDataEnableableEnm.GetEnabledRefRO<global::Unity.Entities.Tests.EcsTestDataEnableable>(index),
			Aspect2_EcsTestSharedCompSc);

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
			global::Unity.Entities.EntityTypeHandle Aspect2_SelfEAc;
			global::Unity.Entities.BufferTypeHandle<global::Unity.Entities.Tests.EcsIntElement> Aspect2_DynamicBufferBAc;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData> Aspect2_Data;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData2> Aspect2_Data2CAc;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData3> Aspect2_Data3CAc;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData4> Aspect2_DataROCAc;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData5> Aspect2_DataOptionalCAc;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestDataEnableable> Aspect2_EcsTestDataEnableableCAc;
			public global::Unity.Entities.SharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp> Aspect2_EcsTestSharedCompScAc;

			/// <summary>
			/// Create the aspect type handle from an system state.
			/// </summary>
			/// <param name="state">System state to create the type handle from.</param>
			public TypeHandle(ref global::Unity.Entities.SystemState state)
			{
				this.Aspect2_SelfEAc = state.GetEntityTypeHandle();
				this.Aspect2_DynamicBufferBAc = state.GetBufferTypeHandle<global::Unity.Entities.Tests.EcsIntElement>(false);
				this.Aspect2_Data = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData>(false);
				this.Aspect2_Data2CAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData2>(false);
				this.Aspect2_Data3CAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData3>(false);
				this.Aspect2_DataROCAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData4>(true);
				this.Aspect2_DataOptionalCAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData5>(false);
				this.Aspect2_EcsTestDataEnableableCAc = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestDataEnableable>(true);
				this.Aspect2_EcsTestSharedCompScAc = state.GetSharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp>();
			}

			/// <summary>
			/// Update the type handle container.
			/// Must be called every frames before using the type handle.
			/// </summary>
			/// <param name="state">The system state the aspect type handle was created from.</param>
			public void Update(ref global::Unity.Entities.SystemState state)
			{
				this.Aspect2_SelfEAc.Update(ref state);
				this.Aspect2_DynamicBufferBAc.Update(ref state);
				this.Aspect2_Data.Update(ref state);
				this.Aspect2_Data2CAc.Update(ref state);
				this.Aspect2_Data3CAc.Update(ref state);
				this.Aspect2_DataROCAc.Update(ref state);
				this.Aspect2_DataOptionalCAc.Update(ref state);
				this.Aspect2_EcsTestDataEnableableCAc.Update(ref state);
				this.Aspect2_EcsTestSharedCompScAc.Update(ref state);
			}

			/// <summary>
			/// Get the enclosing aspect's <see cref="ResolvedChunk"/> from an <see cref="global::Unity.Entities.ArchetypeChunk"/>.
			/// </summary>
			/// <param name="chunk">The ArchetypeChunk to extract the aspect's ResolvedChunk from.</param>
			/// <returns>A ResolvedChunk representing all instances of the aspect in the chunk.</returns>
			public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk chunk)
			{
				ResolvedChunk resolved;
				resolved.Aspect2_SelfNaE = chunk.GetNativeArray(this.Aspect2_SelfEAc);
				resolved.Aspect2_DynamicBufferBa = chunk.GetBufferAccessor(ref this.Aspect2_DynamicBufferBAc);
				resolved.Aspect2_DataNaC = chunk.GetNativeArray(ref this.Aspect2_Data);
				resolved.Aspect2_Data2NaC = chunk.GetNativeArray(ref this.Aspect2_Data2CAc);
				resolved.Aspect2_Data3NaC = chunk.GetNativeArray(ref this.Aspect2_Data3CAc);
				resolved.Aspect2_DataRONaC = chunk.GetNativeArray(ref this.Aspect2_DataROCAc);
				resolved.Aspect2_DataOptionalNaC = chunk.GetNativeArray(ref this.Aspect2_DataOptionalCAc);
				resolved.Aspect2_EcsTestDataEnableableEnm = chunk.GetEnabledMask(ref Aspect2_EcsTestDataEnableableCAc);
				resolved.Aspect2_EcsTestSharedCompSc = chunk.GetSharedComponent<global::Unity.Entities.Tests.EcsTestSharedComp>(this.Aspect2_EcsTestSharedCompScAc);
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
		public struct Enumerator : global::System.Collections.Generic.IEnumerator<Aspect2>, global::System.Collections.Generic.IEnumerable<Aspect2>
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
		    public Aspect2 Current {
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
		    global::System.Collections.Generic.IEnumerator<Aspect2> global::System.Collections.Generic.IEnumerable<Aspect2>.GetEnumerator() => throw new global::System.NotImplementedException();
		    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=> throw new global::System.NotImplementedException();
		}

		/// <summary>
		/// Completes the dependency chain required for this aspect to have read access.
		/// So it completes all write dependencies of the components, buffers, etc. to allow for reading.
		/// </summary>
		/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
		public void CompleteDependencyBeforeRO(ref global::Unity.Entities.SystemState state)
		{
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsIntElement>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData2>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData3>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData4>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestDataEnableable>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestSharedComp>();
		}

		/// <summary>
		/// Completes the dependency chain required for this component to have read and write access.
		/// So it completes all write dependencies of the components, buffers, etc. to allow for reading,
		/// and it completes all read dependencies, so we can write to it.
		/// </summary>
		/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
		public void CompleteDependencyBeforeRW(ref global::Unity.Entities.SystemState state)
		{
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsIntElement>();
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData>();
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData2>();
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData3>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData4>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestDataEnableable>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestSharedComp>();
		}
	}
}
namespace AspectTests
{

	public readonly partial struct AspectNestedAliasing : global::Unity.Entities.IAspect, global::Unity.Entities.IAspectCreate<AspectNestedAliasing>
	{
		/// <summary>
		/// Construct an instance of the enclosing aspect from all required data references.
		/// </summary>
		public AspectNestedAliasing(global::Unity.Entities.Entity aspectnestedaliasing_selfE, 
			global::Unity.Entities.DynamicBuffer<global::Unity.Entities.Tests.EcsIntElement> aspectnestedaliasing_dynamicbufferDb, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData> aspectnestedaliasing_dataRef, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData2> aspectnestedaliasing_data2Ref, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData3> aspectnestedaliasing_data3Ref, 
			global::Unity.Entities.RefRO<global::Unity.Entities.Tests.EcsTestData4> aspectnestedaliasing_dataroRef, 
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData5> aspectnestedaliasing_dataoptionalRef, 
			global::Unity.Entities.EnabledRefRO<global::Unity.Entities.Tests.EcsTestDataEnableable> aspectnestedaliasing_ecstestdataenableableEnref, 
			global::Unity.Entities.Tests.EcsTestSharedComp aspectnestedaliasing_ecstestsharedcompSc)
		{
			this.Self = aspectnestedaliasing_selfE;
			this.DynamicBuffer = aspectnestedaliasing_dynamicbufferDb;
			this.Data = aspectnestedaliasing_dataRef;
			this.Data2 = aspectnestedaliasing_data2Ref;
			this.Data3 = aspectnestedaliasing_data3Ref;
			this.DataRO = aspectnestedaliasing_dataroRef;
			this.DataOptional = aspectnestedaliasing_dataoptionalRef;
			this.EcsTestDataEnableable = aspectnestedaliasing_ecstestdataenableableEnref;
			this.EcsTestSharedComp = aspectnestedaliasing_ecstestsharedcompSc;
			this.Aspect2 = new global::AspectTests.Aspect2(aspectnestedaliasing_selfE, 
				aspectnestedaliasing_dynamicbufferDb, 
				aspectnestedaliasing_dataRef, 
				aspectnestedaliasing_data2Ref, 
				aspectnestedaliasing_data3Ref, 
				aspectnestedaliasing_dataroRef, 
				aspectnestedaliasing_dataoptionalRef, 
				aspectnestedaliasing_ecstestdataenableableEnref, 
				aspectnestedaliasing_ecstestsharedcompSc);
			this.NestedAspectSimple = new global::AspectSimple(aspectnestedaliasing_dataRef);
		}

		/// <summary>
		/// Create an instance of the enclosing aspect struct pointing at a specific entity's components data.
		/// </summary>
		/// <param name="entity">The entity to create the aspect struct from.</param>
		/// <param name="systemState">The system state from which data is extracted.</param>
		/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
		public AspectNestedAliasing CreateAspect(global::Unity.Entities.Entity entity, ref global::Unity.Entities.SystemState systemState)
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
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsIntElement>(),
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData>(),
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData2>(),
					global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData3>(),
					global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestData4>(),
					global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestDataEnableable>(),
					global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestSharedComp>(),
				};
			global::Unity.Entities.Internal.InternalCompilerInterface.MergeWith(ref all, ref allRequiredComponentsInAspect);
			allRequiredComponentsInAspect.Dispose();
		}
		/// <summary>
		/// Get the number of required (i.e. non-optional) components contained in this aspect.
		/// </summary>
		/// <returns>The number of required (i.e. non-optional) components contained in this aspect.</returns>
		public static int GetRequiredComponentTypeCount() => 7;
		/// <summary>
		/// Add component requirements from this aspect into the provided span.
		/// </summary>
		/// <param name="componentTypes">The span to which all required components in this aspect are added.</param>
		public static void AddRequiredComponentTypes(ref global::System.Span<global::Unity.Entities.ComponentType> componentTypes)
		{
			componentTypes[0] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsIntElement>();
				componentTypes[1] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData>();
				componentTypes[2] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData2>();
				componentTypes[3] = global::Unity.Entities.ComponentType.ReadWrite<global::Unity.Entities.Tests.EcsTestData3>();
				componentTypes[4] = global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestData4>();
				componentTypes[5] = global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestDataEnableable>();
				componentTypes[6] = global::Unity.Entities.ComponentType.ReadOnly<global::Unity.Entities.Tests.EcsTestSharedComp>();			
		}

		/// <summary>
		/// A container type that provides access to instances of the enclosing Aspect type, indexed by <see cref="Unity.Entities.Entity"/>.
		/// Equivalent to <see cref="global::Unity.Entities.ComponentLookup{T}"/> but for aspect types.
		/// Constructed from an system state via its constructor.
		public struct Lookup : global::Unity.Entities.Internal.InternalCompilerInterface.IAspectLookup<AspectNestedAliasing>
		{
			global::Unity.Entities.EntityStorageInfoLookup _m_Esil;
			global::Unity.Entities.BufferLookup<global::Unity.Entities.Tests.EcsIntElement> AspectNestedAliasing_DynamicBuffer;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData> AspectNestedAliasing_Data;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData2> AspectNestedAliasing_Data2;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData3> AspectNestedAliasing_Data3;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData4> AspectNestedAliasing_DataRO;
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData5> AspectNestedAliasing_DataOptional;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable> AspectNestedAliasing_EcsTestDataEnableable;
			global::Unity.Entities.SharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp> AspectNestedAliasing_EcsTestSharedComp;

			/// <summary>
			/// Create the aspect lookup from an system state.
			/// </summary>
			/// <param name="state">The system state to create the aspect lookup from.</param>
			public Lookup(ref global::Unity.Entities.SystemState state)
			{
				this._m_Esil = state.GetEntityStorageInfoLookup();
				this.AspectNestedAliasing_DynamicBuffer = state.GetBufferLookup<global::Unity.Entities.Tests.EcsIntElement>(false);
				this.AspectNestedAliasing_Data = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData>(false);
				this.AspectNestedAliasing_Data2 = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData2>(false);
				this.AspectNestedAliasing_Data3 = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData3>(false);
				this.AspectNestedAliasing_DataRO = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData4>(true);
				this.AspectNestedAliasing_DataOptional = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData5>(false);
				this.AspectNestedAliasing_EcsTestDataEnableable = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable>(true);
				this.AspectNestedAliasing_EcsTestSharedComp = state.GetSharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp>();
			}

			/// <summary>
			/// Update the lookup container.
			/// Must be called every frames before using the lookup.
			/// </summary>
			/// <param name="state">The system state the aspect lookup was created from.</param>
			public void Update(ref global::Unity.Entities.SystemState state)
			{
				this._m_Esil.Update(ref state);
				this.AspectNestedAliasing_DynamicBuffer.Update(ref state);
				this.AspectNestedAliasing_Data.Update(ref state);
				this.AspectNestedAliasing_Data2.Update(ref state);
				this.AspectNestedAliasing_Data3.Update(ref state);
				this.AspectNestedAliasing_DataRO.Update(ref state);
				this.AspectNestedAliasing_DataOptional.Update(ref state);
				this.AspectNestedAliasing_EcsTestDataEnableable.Update(ref state);
				this.AspectNestedAliasing_EcsTestSharedComp.Update(ref state);
			}

			/// <summary>
			/// Get an aspect instance pointing at a specific entity's components data.
			/// </summary>
			/// <param name="entity">The entity to create the aspect struct from.</param>
			/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>
			public AspectNestedAliasing this[global::Unity.Entities.Entity entity]
			{
				get
				{
					var chunk = this._m_Esil[entity].Chunk;
					return new AspectNestedAliasing(entity, 
						this.AspectNestedAliasing_DynamicBuffer[entity], 
						this.AspectNestedAliasing_Data.GetRefRW(entity), 
						this.AspectNestedAliasing_Data2.GetRefRW(entity), 
						this.AspectNestedAliasing_Data3.GetRefRW(entity), 
						this.AspectNestedAliasing_DataRO.GetRefRO(entity), 
						this.AspectNestedAliasing_DataOptional.GetRefRWOptional(entity), 
						this.AspectNestedAliasing_EcsTestDataEnableable.GetEnabledRefRO<global::Unity.Entities.Tests.EcsTestDataEnableable>(entity), 
						chunk.GetSharedComponent<global::Unity.Entities.Tests.EcsTestSharedComp>(this.AspectNestedAliasing_EcsTestSharedComp));
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
			/// Chunk data for aspect field 'AspectNestedAliasing.Self'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Entity> AspectNestedAliasing_SelfNaE;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.DynamicBuffer'
			/// </summary>
			public global::Unity.Entities.BufferAccessor<global::Unity.Entities.Tests.EcsIntElement> AspectNestedAliasing_DynamicBufferBa;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.Data'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData> AspectNestedAliasing_DataNaC;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.Data2'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData2> AspectNestedAliasing_Data2NaC;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.Data3'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData3> AspectNestedAliasing_Data3NaC;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.DataRO'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData4> AspectNestedAliasing_DataRONaC;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.DataOptional'
			/// </summary>
			public global::Unity.Collections.NativeArray<global::Unity.Entities.Tests.EcsTestData5> AspectNestedAliasing_DataOptionalNaC;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.EcsTestDataEnableable'
			/// </summary>
			public global::Unity.Entities.EnabledMask AspectNestedAliasing_EcsTestDataEnableableEnm;
			/// <summary>
			/// Chunk data for aspect field 'AspectNestedAliasing.EcsTestSharedComp'
			/// </summary>
			public global::Unity.Entities.Tests.EcsTestSharedComp AspectNestedAliasing_EcsTestSharedCompSc;

			/// <summary>
			/// Get an aspect instance pointing at a specific entity's component data in the chunk index.
			/// </summary>
			/// <param name="index"></param>
			/// <returns>Aspect for the entity in the chunk at the given index.</returns>
			public AspectNestedAliasing this[int index]
				=> new AspectNestedAliasing(this.AspectNestedAliasing_SelfNaE[index],
			this.AspectNestedAliasing_DynamicBufferBa[index],
			new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData>(this.AspectNestedAliasing_DataNaC, index),
			new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData2>(this.AspectNestedAliasing_Data2NaC, index),
			new global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData3>(this.AspectNestedAliasing_Data3NaC, index),
			new global::Unity.Entities.RefRO<global::Unity.Entities.Tests.EcsTestData4>(this.AspectNestedAliasing_DataRONaC, index),
			global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData5>.Optional(this.AspectNestedAliasing_DataOptionalNaC, index),
			this.AspectNestedAliasing_EcsTestDataEnableableEnm.GetEnabledRefRO<global::Unity.Entities.Tests.EcsTestDataEnableable>(index),
			AspectNestedAliasing_EcsTestSharedCompSc);

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
			global::Unity.Entities.EntityTypeHandle AspectNestedAliasing_Self;
			global::Unity.Entities.BufferTypeHandle<global::Unity.Entities.Tests.EcsIntElement> AspectNestedAliasing_DynamicBuffer;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData> AspectNestedAliasing_Data;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData2> AspectNestedAliasing_Data2;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData3> AspectNestedAliasing_Data3;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData4> AspectNestedAliasing_DataRO;
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData5> AspectNestedAliasing_DataOptional;
			[global::Unity.Collections.ReadOnly]
			global::Unity.Entities.ComponentTypeHandle<global::Unity.Entities.Tests.EcsTestDataEnableable> AspectNestedAliasing_EcsTestDataEnableable;
			public global::Unity.Entities.SharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp> AspectNestedAliasing_EcsTestSharedComp;

			/// <summary>
			/// Create the aspect type handle from an system state.
			/// </summary>
			/// <param name="state">System state to create the type handle from.</param>
			public TypeHandle(ref global::Unity.Entities.SystemState state)
			{
				this.AspectNestedAliasing_Self = state.GetEntityTypeHandle();
				this.AspectNestedAliasing_DynamicBuffer = state.GetBufferTypeHandle<global::Unity.Entities.Tests.EcsIntElement>(false);
				this.AspectNestedAliasing_Data = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData>(false);
				this.AspectNestedAliasing_Data2 = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData2>(false);
				this.AspectNestedAliasing_Data3 = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData3>(false);
				this.AspectNestedAliasing_DataRO = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData4>(true);
				this.AspectNestedAliasing_DataOptional = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestData5>(false);
				this.AspectNestedAliasing_EcsTestDataEnableable = state.GetComponentTypeHandle<global::Unity.Entities.Tests.EcsTestDataEnableable>(true);
				this.AspectNestedAliasing_EcsTestSharedComp = state.GetSharedComponentTypeHandle<global::Unity.Entities.Tests.EcsTestSharedComp>();
			}

			/// <summary>
			/// Update the type handle container.
			/// Must be called every frames before using the type handle.
			/// </summary>
			/// <param name="state">The system state the aspect type handle was created from.</param>
			public void Update(ref global::Unity.Entities.SystemState state)
			{
				this.AspectNestedAliasing_Self.Update(ref state);
				this.AspectNestedAliasing_DynamicBuffer.Update(ref state);
				this.AspectNestedAliasing_Data.Update(ref state);
				this.AspectNestedAliasing_Data2.Update(ref state);
				this.AspectNestedAliasing_Data3.Update(ref state);
				this.AspectNestedAliasing_DataRO.Update(ref state);
				this.AspectNestedAliasing_DataOptional.Update(ref state);
				this.AspectNestedAliasing_EcsTestDataEnableable.Update(ref state);
				this.AspectNestedAliasing_EcsTestSharedComp.Update(ref state);
			}

			/// <summary>
			/// Get the enclosing aspect's <see cref="ResolvedChunk"/> from an <see cref="global::Unity.Entities.ArchetypeChunk"/>.
			/// </summary>
			/// <param name="chunk">The ArchetypeChunk to extract the aspect's ResolvedChunk from.</param>
			/// <returns>A ResolvedChunk representing all instances of the aspect in the chunk.</returns>
			public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk chunk)
			{
				ResolvedChunk resolved;
				resolved.AspectNestedAliasing_SelfNaE = chunk.GetNativeArray(this.AspectNestedAliasing_Self);
				resolved.AspectNestedAliasing_DynamicBufferBa = chunk.GetBufferAccessor(ref this.AspectNestedAliasing_DynamicBuffer);
				resolved.AspectNestedAliasing_DataNaC = chunk.GetNativeArray(ref this.AspectNestedAliasing_Data);
				resolved.AspectNestedAliasing_Data2NaC = chunk.GetNativeArray(ref this.AspectNestedAliasing_Data2);
				resolved.AspectNestedAliasing_Data3NaC = chunk.GetNativeArray(ref this.AspectNestedAliasing_Data3);
				resolved.AspectNestedAliasing_DataRONaC = chunk.GetNativeArray(ref this.AspectNestedAliasing_DataRO);
				resolved.AspectNestedAliasing_DataOptionalNaC = chunk.GetNativeArray(ref this.AspectNestedAliasing_DataOptional);
				resolved.AspectNestedAliasing_EcsTestDataEnableableEnm = chunk.GetEnabledMask(ref AspectNestedAliasing_EcsTestDataEnableable);
				resolved.AspectNestedAliasing_EcsTestSharedCompSc = chunk.GetSharedComponent<global::Unity.Entities.Tests.EcsTestSharedComp>(this.AspectNestedAliasing_EcsTestSharedComp);
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
		public struct Enumerator : global::System.Collections.Generic.IEnumerator<AspectNestedAliasing>, global::System.Collections.Generic.IEnumerable<AspectNestedAliasing>
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
		    public AspectNestedAliasing Current {
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
		    global::System.Collections.Generic.IEnumerator<AspectNestedAliasing> global::System.Collections.Generic.IEnumerable<AspectNestedAliasing>.GetEnumerator() => throw new global::System.NotImplementedException();
		    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=> throw new global::System.NotImplementedException();
		}

		/// <summary>
		/// Completes the dependency chain required for this aspect to have read access.
		/// So it completes all write dependencies of the components, buffers, etc. to allow for reading.
		/// </summary>
		/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
		public void CompleteDependencyBeforeRO(ref global::Unity.Entities.SystemState state)
		{
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsIntElement>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData2>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData3>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData4>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestDataEnableable>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestSharedComp>();
		}

		/// <summary>
		/// Completes the dependency chain required for this component to have read and write access.
		/// So it completes all write dependencies of the components, buffers, etc. to allow for reading,
		/// and it completes all read dependencies, so we can write to it.
		/// </summary>
		/// <param name="state">The <see cref="global::Unity.Entities.SystemState"/> containing an <see cref="global::Unity.Entities.EntityManager"/> storing all dependencies.</param>
		public void CompleteDependencyBeforeRW(ref global::Unity.Entities.SystemState state)
		{
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsIntElement>();
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData>();
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData2>();
			state.EntityManager.CompleteDependencyBeforeRW<global::Unity.Entities.Tests.EcsTestData3>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestData4>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestDataEnableable>();
			state.EntityManager.CompleteDependencyBeforeRO<global::Unity.Entities.Tests.EcsTestSharedComp>();
		}
	}
}
