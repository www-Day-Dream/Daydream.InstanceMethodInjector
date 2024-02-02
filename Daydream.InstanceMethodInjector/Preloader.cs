using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Daydream.InstanceMethodInjector;

public static class Preloader
{
    private const string CacheName = "instance_injector";
    internal static ManualLogSource Logger { get; set; }
    private static Dictionary<string, List<ComparableMethod>> RequiresMethods { get; set; }

    // ReSharper disable once InconsistentNaming
    public static IEnumerable<string> TargetDLLs { get; } = new[]
    {
        "Assembly-CSharp.dll"
    };
    

    public static void Initialize()
    {
        Logger = BepInEx.Logging.Logger.CreateLogSource("DayDream.InstanceMethodInjector");
        RequiresMethods = FindRequiredMethods(Paths.PluginPath);
    }

    public static void Patch(AssemblyDefinition assemblyDefinition)
    {
        var allTypes = assemblyDefinition.MainModule.GetTypes().ToArray();
        
        foreach (var typeDefinition in allTypes)
        {
            if (!RequiresMethods.TryGetValue(typeDefinition.FullName, out var requiredMethods))
                continue;
            foreach (var requiredMethod in requiredMethods)
                CreateDesiredMethod(typeDefinition, requiredMethod);
        }
        assemblyDefinition.Write(Path.Combine(Paths.CachePath, "Assembly-CSharp.dll"));
    }

    internal class CacheData : ICacheable
    {
        void ICacheable.Save(BinaryWriter bw)
        {
        }

        void ICacheable.Load(BinaryReader br)
        {
        }
    }

    private static Dictionary<string, List<ComparableMethod>> FindRequiredMethods(string directory)
    {
        var dictionary = new Dictionary<string, List<ComparableMethod>>();
        var dontScanUnlessNew = 
            TypeLoader.LoadAssemblyCache<CacheData>(CacheName) ?? new Dictionary<string, CachedAssembly<CacheData>>();
        Logger.LogWarning($"Ignoring {dontScanUnlessNew.Count} disregarded plugin{(dontScanUnlessNew.Count == 1 ? "" : "s")}.");
        foreach (var fileDir in Directory.GetFiles(
                     Path.GetFullPath(directory), "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                if (dontScanUnlessNew.TryGetValue(fileDir, out var cachedAssembly) &&
                    cachedAssembly.Timestamp == File.GetLastWriteTimeUtc(fileDir).Ticks)
                    continue;
                
                if (dontScanUnlessNew.ContainsKey(fileDir))
                    dontScanUnlessNew.Remove(fileDir);
                
                var assemblyDefinition = AssemblyDefinition.ReadAssembly(fileDir, TypeLoader.ReaderParameters);

                var numAdded = 0;
                foreach (var comp in assemblyDefinition.CustomAttributes
                             .Select(ComparableMethod.FromCecilConstructor))
                {
                    if (comp == null)
                        continue;
                    if (!dictionary.ContainsKey(comp.TypeName))
                        dictionary.Add(comp.TypeName, new List<ComparableMethod>());

                    numAdded++;
                    if (dictionary[comp.TypeName].All(compCheck => !compCheck.IsEqual(comp)))
                    {
                        Logger.LogInfo(assemblyDefinition.Name.Name + " queued method '" + comp + "' for creation!");
                        dictionary[comp.TypeName].Add(comp);
                    }  else
                        Logger.LogWarning(assemblyDefinition.Name.Name + " skipped queuing method '" + comp + "' because it's already queued.");
                }
                
                if (numAdded == 0)
                {
                    // Blacklist this one
                    // We've marked this one as not containing any definitions so until it changes, we'll disregard it.
                    Logger.LogWarning($"Disregarding {Path.GetFileName(fileDir)} for future loads due to requiring " +
                                      $"no additional methods.");
                    dontScanUnlessNew.Add(fileDir, new CachedAssembly<CacheData>
                    {
                        CacheItems = new List<CacheData>(){ new CacheData() }, 
                        Timestamp = 0
                    });
                    assemblyDefinition.Dispose();
                    continue;
                }

                assemblyDefinition.Dispose();
            }
            catch (BadImageFormatException ex)
            {
                Logger.LogDebug("Skipping loading " + fileDir + " because it's not a valid .NET assembly. Full error: " + ex.Message);
            }
            catch (Exception ex2)
            {
                Logger.LogError(ex2.ToString());
            }
        }

        var exportDict = dontScanUnlessNew
            .ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value.CacheItems );
        TypeLoader.SaveAssemblyCache(CacheName, exportDict);
        return dictionary;
    }
    private static void CreateDesiredMethod(TypeDefinition typeDefinition,
        ComparableMethod requiredMethod)
    {
        var methodDefinition = new MethodDefinition(requiredMethod.MethodName, 
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
            typeDefinition.Module.TypeSystem.Void);

        if (typeDefinition.Methods.Any(requiredMethod.IsEqual))
        {
            Logger.LogError("Skipping " + requiredMethod.TypeName + ":" + requiredMethod.MethodName + 
                            " because it already contains a definition.");
            return;
        }

        var paramTypeCounts = new Dictionary<string, int>();
        var paramTypeCounter = new Dictionary<string, int>();
        foreach (var argument in requiredMethod.Arguments)
        {
            var asTypeRef = (TypeReference)argument.Value;
            var key = asTypeRef.IsGenericInstance ? asTypeRef.Name
                .Substring(0, asTypeRef.Name.IndexOf('`')) : asTypeRef.Name;
            if (!paramTypeCounter.ContainsKey(key))
                paramTypeCounter.Add(key, 0);
            if (!paramTypeCounts.ContainsKey(key))
                paramTypeCounts.Add(key, 0);
            paramTypeCounts[key]++;
        }
        foreach (var argument in requiredMethod.Arguments)
        {
            var asTypeRef = (TypeReference)argument.Value;
            var key = asTypeRef.IsGenericInstance
                ? asTypeRef.Name.Substring(0, asTypeRef.Name.IndexOf('`'))
                : asTypeRef.Name;
            var count = paramTypeCounts[key];
            var index = paramTypeCounter[key]++;
            var paramName = "p" + key + (count > 1 ? "_" + index : "");
            if (!typeDefinition.Module.TryGetTypeReference(asTypeRef.FullName, out var typeRef))
                typeRef = typeDefinition.Module.ImportReference(asTypeRef);
            methodDefinition.Parameters.Add(new ParameterDefinition(paramName, ParameterAttributes.None, typeRef));
        }

        typeDefinition.Methods.Add(methodDefinition);
        methodDefinition.Body.InitLocals = true;
        methodDefinition.Body.GetILProcessor().Emit(OpCodes.Ret);
    }
}