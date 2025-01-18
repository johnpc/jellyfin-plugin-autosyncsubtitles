<h1 align="center">Jellyfin Autosync Subtitles Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

<p align="center">
Jellyfin Autosync Subtitles plugin is a plugin that automatically syncs subtitles files associated with your library;

</p>

## Install Process


## From Repository
1. In jellyfin, go to dashboard -> plugins -> Repositories -> add and paste this link https://raw.githubusercontent.com/johnpc/JellyfinPluginManifest/master/manifest.json
2. Go to Catalog and search for Autosync Subtitles
3. Click on it and install
4. Restart Jellyfin


## From .zip file
1. Download the .zip file from release page
2. Extract it and place the .dll file in a folder called ```plugins/Autosync Subtitles``` under  the program data directory or inside the portable install directory
3. Restart Jellyfin

## User Guide
1. To autosync subtitles you can do it from Schedule task or directly from the configuration of the plugin.
2. You need to have enabled the option "Autosync Subtitles" under display





## Build Process
1. Clone or download this repository
2. Ensure you have .NET Core SDK setup and installed
3. Build plugin with following command.
```sh
dotnet publish --configuration Release --output bin
cd bin && zip -r Jellyfin-Plugin-AutosyncSubtitles.zip ./Jellyfin.Plugin.AutoSyncSubtitles.dll && cd -
```
4. Place the resulting .dll file in a folder called ```plugins/Autosync Subtitles``` under  the program data directory or inside the portable install directory
5. Upload ./bin/Jellyfin-Plugin-AutosyncSubtitles.zip as GH release and update https://github.com/johnpc/JellyfinPluginManifest with the release

