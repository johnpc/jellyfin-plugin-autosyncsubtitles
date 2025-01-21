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
rm -rf bin
dotnet format
dotnet publish --configuration Release --output bin
cd bin && zip -r Jellyfin-Plugin-AutosyncSubtitles.zip ./Jellyfin.Plugin.AutoSyncSubtitles.dll && cd -
md5sum bin/Jellyfin-Plugin-AutosyncSubtitles.zip
```
4. Place the resulting .dll file in a folder called ```plugins/Autosync Subtitles``` under  the program data directory or inside the portable install directory
5. Upload ./bin/Jellyfin-Plugin-AutosyncSubtitles.zip as GH release and update https://github.com/johnpc/JellyfinPluginManifest with the release (checksum from `md5sum bin/Jellyfin-Plugin-AutosyncSubtitles.zip`)
6. Manifest url is https://raw.githubusercontent.com/johnpc/JellyfinPluginManifest/<hash>/manifest.json

## Deploy Process

For now, the deploy process is manual. In github, go to the release or create the release (like https://github.com/johnpc/jellyfin-plugin-autosyncsubtitles/releases/edit/v0.0.1). From there you can upload the `Jellyfin-Plugin-AutosyncSubtitles.zip` generated from the build process.

## Install process

Install the plugin in jellyfin by visiting your Jellyfin Admin dashboard and choosing Plugins > Catelog > Gear Icon.

There you can add this repository as a plugin source:

```bash
Name: @johnpc (AutosyncSubtitles)
Repo url: https://raw.githubusercontent.com/johnpc/JellyfinPluginManifest/f754e9e88610a7d7fbd480af08916fad499e1060/manifest.json
```

Then find, go back to Plugins > Catelog and you'll see Autosync Subtitles there. Click it, choose install, and restart your jellyfin server!

BUT THAT'S NOT ALL!

For now, in order for this to work, your jellyfin container must have dependencies installed. In the future I want to package the ffsubsync binary into a single executable that ships with this plugin, but for reasons that isn't the case right now.

First, docker exec into your jellyfin container:

```bash
sudo docker exec -it jellyfin_server_1  bash
```

Then, install all the dependencies and set necessary permissions

```bash
apt update
apt install python3 python3-pip ffmpeg pipx libsndfile1
pipx install ffsubsync
pipx install autosubsync
chmod -R +rx /root/
chsh -s /bin/bash abc
mkdir -p /home/abc
chown abc:abc /home/abc
```

To make matters worse, you'll need to redo the dependency installation step every time you restart your jellyfin server. Yes this really sucks and I will fix it soon. Right now this is in a proof of concept phase.
