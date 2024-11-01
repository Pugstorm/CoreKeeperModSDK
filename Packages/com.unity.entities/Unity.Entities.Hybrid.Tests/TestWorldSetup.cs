using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor.Compilation;

namespace Unity.Entities.Tests
{
    public static class TestWorldSetup
    {
        public static readonly string[] EntitiesPackage = { "com.unity.entities" };
        public static IEnumerable<Type> FilterSystemsToPackages(IEnumerable<Type> systems, IEnumerable<string> packageNames)
        {
            const string packagePrefix = "Packages";
            foreach (var s in systems)
            {
                var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(s.Assembly.GetName().Name);
                if (path == null)
                    continue;
                if (!path.StartsWith(packagePrefix))
                    continue;
                var packagePath = path.Substring(packagePrefix.Length + 1);
                if (packageNames.Any(packagePath.StartsWith))
                    yield return s;
            }
        }

        public static World CreateEntityWorld(string name, bool isEditor)
        {
            var systems = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.Default, true);
            var world = new World(name, isEditor ? WorldFlags.Editor : WorldFlags.Game);
            
            //currently FilterSystemsToPackages filters assemblies and looks for package names, which means we have to 
            //use the bad reflection path here. at some point, we could have a non-reflection way of doing this, but 
            //today we don't. 
            var typesList = new List<Type>();
            for (int i=0; i<systems.Length; i++)
                typesList.Add(TypeManager.GetSystemType(systems[i]));
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, FilterSystemsToPackages(typesList, EntitiesPackage));
            return world;
        }

        public enum TestWorldSystemFilterFlags
        {
            Default,
            OnlyStreaming
        }

        public static World CreateEntityWorld(string name, TestWorldSystemFilterFlags testFlags)
        {
            NativeList<SystemTypeIndex> systems;
            if (testFlags == TestWorldSystemFilterFlags.OnlyStreaming)
            {
                systems = TypeManager.GetSystemTypeIndices(WorldSystemFilterFlags.Streaming, WorldSystemFilterFlags.Streaming);
            }
            else
            {
                systems = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.Default, true);
            }

            var world = new World(name, WorldFlags.Game);
            
            //currently FilterSystemsToPackages filters assemblies and looks for package names, which means we have to 
            //use the bad reflection path here. at some point, we could have a non-reflection way of doing this, but 
            //today we don't. 
            var typesList = new List<Type>();
            for (int i=0; i<systems.Length; i++)
                typesList.Add(TypeManager.GetSystemType(systems[i]));
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, FilterSystemsToPackages(typesList, EntitiesPackage));
            return world;
        }
    }
}
