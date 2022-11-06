using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IPA;
using IPA.Loader;
using IPA.Utilities;
using Mono.Cecil;
using Newtonsoft.Json;
using UnityEngine;

namespace ModLoader;

public class ModLoader : MonoBehaviour {

    internal static void Instantiate() {
        var gameObject = new GameObject("ModLoader");
        gameObject.AddComponent<ModLoader>();
        DontDestroyOnLoad(gameObject);
    }
    
    public static readonly ConcurrentQueue<string> ModuleQueue = new();
        
    private void Update() {
            
        if (ModuleQueue.Count <= 0)
            return;
            
        var e = ModuleQueue.TryDequeue(out var path);
        if (e) {
            LoadFromFile(path);
        }
            
    }
    
    public static void EnqueueToLoad(string filePath) {
        ModuleQueue.Enqueue(filePath);
    }
    
    private const string ManifestSuffix = ".manifest.json";

    private static readonly List<string> ModsLoadedByModLoader = new();

    private static ConstructorInfo _cecilLibLoaderConstructor = null!;

    #region Plugin Metadata

    private static Type _pluginMetadataType = null!;

    private static ConstructorInfo _pluginMetadataConstructor = null!;

    private static PropertyInfo _pluginMetadataFileProperty = null!;
    private static PropertyInfo _pluginMetadataPluginTypeProperty = null!;
    private static PropertyInfo _pluginMetadataRuntimeOptionsProperty = null!;
    private static PropertyInfo _pluginMetadataAssemblyProperty = null!;

    private static FieldInfo _pluginMetadataIsSelfField = null!;
    private static FieldInfo _pluginMetadataManifestField = null!;

    #endregion

    #region Plugin Manifest

    private static Type _pluginManifestType = null!;

    private static FieldInfo _pluginManifestIdField = null!;
    private static FieldInfo _pluginManifestNameField = null!;

    #endregion

    #region Plugin Executor

    private static Type _pluginExecutorType = null!;

    private static ConstructorInfo _pluginExecutorConstructor = null!;
    
    private static MethodInfo _pluginExecutorCreateMethod = null!;
    private static MethodInfo _pluginExecutorEnableMethod = null!;
    
    private static object _noneSpecial = null!;

    #endregion
    
    internal static void Init() {

        #region Plugin Metadata

        _pluginMetadataType = typeof(PluginMetadata);
        
        _pluginMetadataConstructor = _pluginMetadataType.GetConstructor(new Type[] { })!;
        
        _pluginMetadataFileProperty =_pluginMetadataType.GetProperty("File", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        _pluginMetadataPluginTypeProperty = _pluginMetadataType.GetProperty("PluginType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        _pluginMetadataRuntimeOptionsProperty = _pluginMetadataType.GetProperty("RuntimeOptions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        _pluginMetadataAssemblyProperty = _pluginMetadataType.GetProperty("Assembly", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        _pluginMetadataIsSelfField =_pluginMetadataType.GetField("IsSelf", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        _pluginMetadataManifestField =_pluginMetadataType.GetField("manifest", BindingFlags.NonPublic | BindingFlags.Instance)!;
        
        #endregion

        var cecilLibLoaderType = _pluginMetadataType.Assembly.GetType("IPA.Loader.CecilLibLoader");
        _cecilLibLoaderConstructor = cecilLibLoaderType.GetConstructor(new Type[] { })!;

        #region Plugin Manifest

        _pluginManifestType = _pluginMetadataType.Assembly.GetType("IPA.Loader.PluginManifest");
        _pluginManifestIdField = _pluginManifestType.GetField("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        _pluginManifestNameField = _pluginManifestType.GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        #endregion

        #region Plugin Executor

        _pluginExecutorType = _pluginMetadataType.Assembly.GetType("IPA.Loader.PluginExecutor");

        //PluginExecutor.Special.None
        
        var pluginExecutorSpecialType = _pluginExecutorType.GetNestedType("Special", BindingFlags.Public | BindingFlags.Instance)!;
        
        _noneSpecial = Enum.Parse(pluginExecutorSpecialType, "None");
        
        _pluginExecutorConstructor = _pluginExecutorType.GetConstructor(new[] { _pluginMetadataType, pluginExecutorSpecialType })!;
            
        _pluginExecutorCreateMethod = _pluginExecutorType.GetMethod("Create", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        _pluginExecutorEnableMethod = _pluginExecutorType.GetMethod("Enable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        
        #endregion

    }

    #region Load
    
    public static object? LoadFromFile(string path) {
        var pluginMetadata = LoadPluginMetadataFromFile(path);
        return InitPlugin(pluginMetadata);
    }

    [Obsolete("Mhhhh, might doesnt work cause ... stream to byte array and then loading, ... ARGHHHHH")]
    public static object? LoadFromStream(Stream stream) {
        var pluginMetadata = LoadPluginMetadataFromStream(stream);
        return InitPlugin(pluginMetadata);
    }
    
    public static object? LoadFromByteArray(byte[] bytes) {
        var pluginMetadata = LoadPluginMetadataFromByteArray(bytes);
        return InitPlugin(pluginMetadata);
    }
    
    #endregion

    #region Utilities

    public static bool HasBeenLoadedByModLoaderUR(string id) {
        var plugin = PluginManager.GetPluginFromId("ModLoader");
        if (plugin == null)
            return false;
        return plugin.Assembly.GetType("ModLoader.ModLoader")?.GetMethod("HasBeenLoadedByModLoader", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { id }) as bool? ?? false;
    }
    
    public static bool HasBeenLoadedByModLoader(string id) {
        return ModsLoadedByModLoader.Contains(id);
    }

    #endregion

    #region Init

    private static object? InitPlugin(object? pluginMetadata) {
        
        var pluginExecutor = _pluginExecutorConstructor.Invoke(new[] { pluginMetadata, _noneSpecial });

        AddPluginExecutorToPluginsList(pluginExecutor);
            
        _pluginExecutorCreateMethod.Invoke(pluginExecutor, EmptyObjects);
        _pluginExecutorEnableMethod.Invoke(pluginExecutor, EmptyObjects);

        ModsLoadedByModLoader.Add(GetIdFromMetadata(pluginMetadata));
        
        TriggerSoftRestart();
        
        return pluginExecutor;

    }

    private static void TriggerSoftRestart() {
        Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>()[0].RestartGame();
    }

    #endregion
    
    // TODO: Move Assembly to temp folder and load it ig? // new FileInfo(stream.GetHashCode().ToString())
    
    #region Read Assembly Definition

    private static AssemblyDefinition ReadAssemblyDefinitionFromFile(string path) {
        return AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
            ReadingMode = ReadingMode.Immediate,
            InMemory = true,
            ReadWrite = false,
            AssemblyResolver = ConstructCecilLibLoader()
        });
    }
    
    private static AssemblyDefinition ReadAssemblyDefinitionFromStream(Stream stream) {
        return AssemblyDefinition.ReadAssembly(stream, new ReaderParameters {
            ReadingMode = ReadingMode.Immediate,
            InMemory = true,
            ReadWrite = false,
            AssemblyResolver = ConstructCecilLibLoader()
        });
    }

    #endregion
    
    #region Reflection Stuff

    private static readonly Type[] EmptyTypes = Type.EmptyTypes;
    private static readonly object[] EmptyObjects = Array.Empty<object>();
    
    private static BaseAssemblyResolver ConstructCecilLibLoader() {
        var cecilLibLoader = (BaseAssemblyResolver) _cecilLibLoaderConstructor.Invoke(EmptyObjects);
        cecilLibLoader.AddSearchDirectory(UnityGame.LibraryPath);
        cecilLibLoader.AddSearchDirectory(UnityGame.PluginsPath);
        return cecilLibLoader;
    }
    
    private static void AddPluginExecutorToPluginsList(object pluginExecutor) {
        var bsPluginsList = typeof(PluginManager).GetField("_bsPlugins", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        if(bsPluginsList == null)
            throw new Exception("Failed to get the _bsPlugins list");
        var listTypeOfPluginExecutor = typeof(List<>).MakeGenericType(_pluginExecutorType);
        listTypeOfPluginExecutor.GetMethod("Add")!.Invoke(bsPluginsList, new[] { pluginExecutor });
    }

    private static string GetIdFromMetadata(object pluginMetadata) {
        return (string)_pluginManifestIdField.GetValue(_pluginMetadataManifestField.GetValue(pluginMetadata));
    }
    
    #endregion

    #region Load Plugin Metadata

    private static object? LoadPluginMetadataFromFile(string path) {
        return LoadPluginMetadata(
            ReadAssemblyDefinitionFromFile(path),
            Assembly.LoadFile(path)
        );
    }
    
    private static object? LoadPluginMetadataFromStream(Stream stream) {
        return LoadPluginMetadata(
            ReadAssemblyDefinitionFromStream(stream),
            LoadAssemblyFromStream(stream)
        );
    }
    
    private static object? LoadPluginMetadataFromByteArray(byte[] bytes) {
        return LoadPluginMetadata(
            ReadAssemblyDefinitionFromStream(new MemoryStream(bytes)),
            Assembly.Load(bytes)
        );
    }

    private static Assembly LoadAssemblyFromStream(Stream stream) {
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return Assembly.Load(memoryStream.ToArray());
    }
    
    private static object? LoadPluginMetadata(AssemblyDefinition assemblyDefinition, Assembly assembly) { // stream instead of file

        var pluginMetadata = _pluginMetadataConstructor.Invoke(new object[] { });

        _pluginMetadataFileProperty.SetValue(pluginMetadata, null);
        _pluginMetadataIsSelfField.SetValue(pluginMetadata, false);

        //AntiMalwareEngine.Engine.ScanFile()
        // TODO: Anti Malware
        
        var pluginModule = assemblyDefinition.MainModule;
        
        var pluginNamespace = "";

        object? pluginManifest = null;
        foreach (var moduleResource in pluginModule.Resources) {
            
            if (moduleResource is not EmbeddedResource embeddedResource ||
                !embeddedResource.Name.EndsWith(ManifestSuffix, StringComparison.Ordinal))
                continue;

            pluginNamespace = embeddedResource.Name.Substring(0, embeddedResource.Name.Length - ManifestSuffix.Length);

            using var manifestStream = embeddedResource.GetResourceStream();
            using var streamReader = new StreamReader(manifestStream);

            pluginManifest = JsonConvert.DeserializeObject(streamReader.ReadToEnd(), _pluginManifestType);

            streamReader.Close();
            manifestStream.Close();

            break;
            
        }
        
        if (pluginManifest == null)
            throw new Exception("Plugin manifest not found");

        var id = (string?)_pluginManifestIdField.GetValue(pluginManifest);
        if (id == null) {
            _pluginManifestIdField.SetValue(pluginManifest, _pluginManifestNameField.GetValue(pluginManifest));
        }
        
        // TODO: Conflicts, Load After, Load Before, Dependencies etc.

        _pluginMetadataManifestField.SetValue(pluginMetadata, pluginManifest);

        #region Inline Functions

        bool TryPopulatePluginType(TypeDefinition typeDefinition, object meta) {
            
            if (!typeDefinition.HasCustomAttributes)
                return false;

            var attr = typeDefinition.CustomAttributes.FirstOrDefault(a => a.Constructor.DeclaringType.FullName == typeof(PluginAttribute).FullName);
            if (attr is null)
                return false;

            if (!attr.HasConstructorArguments) {
                return false;
            }

            var args = attr.ConstructorArguments;
            if (args.Count != 1) {
                return false;
            }

            var rtOptionsArg = args[0];
            if (rtOptionsArg.Type.FullName != typeof(RuntimeOptions).FullName) {
                return false;
            }

            var rtOptionsValInt = (int)rtOptionsArg.Value; // `int` is the underlying type of RuntimeOptions

            _pluginMetadataRuntimeOptionsProperty.SetValue(meta, (RuntimeOptions)rtOptionsValInt);
            _pluginMetadataPluginTypeProperty.SetValue(meta, typeDefinition);
            
            return true;
        }

        void TryGetNamespacedPluginType(string ns, object meta) {
            foreach (var type in pluginModule.Types) {
                if (type.Namespace != ns) continue;

                if (TryPopulatePluginType(type, meta))
                    return;
            }
        }

        #endregion

        /*var hint = metadata.Manifest.Misc?.PluginMainHint;

        if (hint != null) {
            var type = pluginModule.GetType(hint);
            if (type == null || !TryPopulatePluginType(type, pluginMetadata))
                TryGetNamespacedPluginType(hint, pluginMetadata);
        }*/ // TODO: MAKE THIS

        var pt = _pluginMetadataPluginTypeProperty.GetValue(pluginMetadata);
        if (pt == null)
            TryGetNamespacedPluginType(pluginNamespace, pluginMetadata);

        if (_pluginMetadataPluginTypeProperty.GetValue(pluginMetadata) == null) {
            throw new Exception("DJEJEHJNEJRH");
        }

        _pluginMetadataAssemblyProperty.SetValue(pluginMetadata, assembly);
        
        //PluginsMetadata.Add(metadata);

        // TODO: IG? https://github.com/nike4613/BeatSaber-IPA-Reloaded/blob/master/IPA.Loader/Loader/PluginLoader.cs#L303-L342

        return pluginMetadata;
    }

    #endregion

}