TODO 📋
=======

Finished 👍
- [X] Load Mods (from file) (tested with: ReeSabers, BeatLeader)
- [X] Allow the opportunity to load mods from the Plugins folder (if dropped in at any time)
- [X] Mods using SiraUtil doesn't work atm (something with the CecilAssemblyResolver) (FIXED)

Not Finished 🙄
- [ ] Test if mods loading from streams and byte arrays works
- [ ] Add `Conflicts` Support (Check for conflicts with or by other mods and print an warn (don't load the mod))
- [ ] Add `LoadBefore` Support (Just say it may cause problems )
- [ ] Add `LoadAfter` Support (Just load it ig? and ignore that shit)
- [ ] Add `Dependencies` Support (Check if dependencies are installed and load them if (SiraUtil))
- [ ] Get people to test it
- [ ] Mods should be only loaded in the menu (or else the game crashes), so adding to the queue that it should only load when being in the menu
- [ ] Improve Code
- [ ] Add AntiMalwareScanning
- [ ] And much more ig 🙄

Bugged mods ☠️
- [ ] Chroma (requires a second soft reload or else every chroma map won't show up)
- [ ] BetterSongSearch requires an second soft reload in order to download maps (cause the first download throws an error and every other wont download until second soft restart)
- [ ] Counters+ doesn't load at all
- [ ] CustomPlatforms doesn't load at all
- [ ] JDFixer doesn't load at all