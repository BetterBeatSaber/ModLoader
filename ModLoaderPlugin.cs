using System;
using System.IO;
using IPA;
using IPA.Config;
using IPA.Config.Stores;

namespace ModLoader;

[Plugin(RuntimeOptions.SingleStartInit)]
public sealed class ModLoaderPlugin {

    public static IPA.Logging.Logger Logger = null!;
    public static ModLoaderPluginConfig Config = null!;
    
    private FileSystemWatcher? _watcher;
    
    [Init]
    public void Init(IPA.Logging.Logger logger, Config config) {
        
        Logger = logger;
        Config = config.Generated<ModLoaderPluginConfig>();

        if (!Config.EnableModLoader)
            return;
        
        ModLoader.Init();
        
        Logger.Info("Mod Loader has been enabled");

    }

    [OnStart]
    public void OnStart() {
        
        if (!Config.EnableModLoader)
            return;
        
        ModLoader.Instantiate();

        if (!Config.EnablePluginFolderWatcher)
            return;
        
        _watcher = new FileSystemWatcher(Path.Combine(Environment.CurrentDirectory, "Plugins"), "*.dll") {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };
        
        _watcher.Created += (_, args) => {
            
            if (args.ChangeType != WatcherChangeTypes.Created)
                return;

            ModLoader.EnqueueToLoad(args.FullPath);
            
        };
        
        _watcher.BeginInit();
        
        Logger.Info("Watching for new mods in the Plugins folder");

    }

    [OnDisable]
    public void OnDisable() {
        _watcher?.Dispose();
    }

}