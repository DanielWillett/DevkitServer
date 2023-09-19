**THIS PROJECT IS NOT YET STABLE, DO NOT USE FOR ANY REAL PROJECTS**

# Devkit Server
DevkitServer is a module/plug-in made for the game Unturned.
It adds a multiplayer component to the in-game map editor.
One of my goals for this project was to not rely on any outside programs or servers.
I would say I partially achieved this, however port-forwarding a TCP server is recommended (but not required) to help with high-capacity data-transfer speed.

The server runs from U3DS (Unturned 3 Dedicated Server) as a normal Steam server. Upon connecting you will be put into the editor which is synced up with any other players on the server.
The client must also install the module, but no further setup is required.

# Installation (Launcher - recommended)
Download the latest release of the launcher module zip file from the Releases section.

Copy it to your `Unturned\Modules` or `U3DS\Modules` folder as `DevkitServer.Launcher.zip` and click **Extract Here**.

Ensure the folder structure looks like this:
`Modules\DevkitServer.Launcher\Bin\DevkitServer.Launcher.dll`

Remember that for client-side, you must launch without BattlEye enabled for modules to run.
Make sure you have an internet connection when launching, at least for the first time.

DevkitServer will be downloaded from the following three NuGet packages if it's missing or if it has an available update:
* [DevkitServer.Resources](https://www.nuget.org/packages/DevkitServer.Resources) - Both Platforms (< 5 MB)
* [DevkitServer.Server](https://www.nuget.org/packages/DevkitServer.Server) - Dedicated Server (< 1 MB)
* [DevkitServer.Client](https://www.nuget.org/packages/DevkitServer.Client) - Client (< 1 MB)

The launcher will keep DevkitServer up to date.
It supports the following launch options.

* `-DontUpdateDevkitServer` - Will install DevkitServer if it's missing, but won't try to keep it updated.
* `-ForceDevkitServerReinstall` - Reinstalls DevkitServer each launch. Good for fixing corrupted module files or forcing an update. You will not lose any savedata.
* `-DontCheckForDevkitServerUpdates` - Stops the automatic checking for updates after startup. (checking does not mean restarting, it will just log a message)
* `-AutoRestartDevkitServerUpdates` - Automatically restarts the server after an update is pushed. Yes this works for the client, although not really recommended.
* `-DevkitServerCheckUpdateInterval <seconds>` - Amount of seconds between update checks. *Default is 2 minutes*
* `-DevkitServerCheckUpdateShutdownDelay <seconds>` - Amount of seconds between when an update is found and when the shutdown occurs. *Default is 2 minutes*

Plugins can override the auto-restart behavior by referencing the Launcher and listening to the `DevkitServerAutoUpdateComponent.OnUpdateReady` event.
This event will not be called if the `DontCheckForDevkitServerUpdates` flag is present in the launch options.

# Manual Installation (Without Launcher)

## Download Module Zip
Download the latest release of the module from GitHub: `DevkitServer.ManualInstallation.zip`.

Copy it to your `Unturned\Modules` or `U3DS\Modules` folder as `DevkitServer.zip` and click **Extract Here**.

Ensure the folder structure looks like this:
`Modules\DevkitServer\Bin\DevkitServer.dll`

Remove the `Dependencies` section from the `Modules\DevkitServer\DevkitServer.module` file:
```jsonc
"Dependencies":
[
  {
    "Name": "DevkitServer.Launcher",
    "Version": "1.0.0.0"
  }
],
```

It's been installed. After first launch you can configure client-side settings at `Unturned\DevkitServer\client_config.json`, or server-side settings at `U3DS\Servers\<server id>\DevkitServer`.

Remember that for client-side, you must launch without BattlEye enabled for modules to run.

# Server Setup
Create a normal vanilla server using U3DS and SteamCMD. There are numerous tutorials online for this.

Install DevkitServer with either the launcher or manually by following the steps above.

Add any necessary workshop items in the `WorkshopDownloadConfig.json` file. Don't add a map into this for editing, instead download it by subscribing to it on Steam client and copying the map from the downloaded map folder (the named one) into `U3DS\Maps`.

Set the map name in Commands.dat like a vanilla server (i.e. `Map PEI`). If you are creating a new map, ensure its a unique name across everything in `U3DS\Maps` and your selected Workshop Items.

Make sure you disable the BattlEye requirement in the config file.

## Configure DevkitServer Settings
Create the folder `U3DS\Servers\<server id>\DevkitServer` if it doesn't already exist.

Copy `server_config.json` from `U3DS\Modules\Defaults` into the new folder and open it with your favorite IDE (like Visual Studio Code).
(this will happen automatically after the first launch)

If available use `JSONC` or **Json with Comments** as the file type.

Configure the `new_level_info` section:
* `gamemode_type`: Survival (default), Horde, Arena
* `start_size`: Tiny, Small, Medium (default), Large, Insane
* `map_owner`: Steam ID of the owner of the map. This defaults to the first admin on the server if it's not defined.
  + This only sets the owner in the map metadata, which decides who can edit the map without a `.unlocker` file.

## Further Configuring New Map
After running the server at least once, navigate to `U3DS\Maps\<Map Name>` to see the map template files.

Open up `English.dat` to configure localized name, description, and tips. You can also add keys for locations here.

The template comes with one tip, which you'll probably want to change.

Next open `Config.json` to configure map contributors (feel free to remove DevkitServer from the Thanks section).
This is also where you should set `Tips` to the number of tips you have in `English.dat`.

## TCP Server Setup
If possible, it is highly recommended to set up the TCP (sometimes called high-speed) server port.

You will need access to port-forwarding on your server machine, and some hosts may not offer this.
You can attempt to contact support but it's possible they won't agree to port-forward it for you.

In the `server_config.json` file, the `high_speed` section can be configured to enable a much faster data transfer protocol than possible with Steam servers.

This allows the map to be downloaded on join much quicker, which is much more noticeable with bigger maps.

To set it up, select a port and port-forward it with protocol set to `TCP`. The default port will work fine for most setups (`31905`).

Follow the information here to port-forward: https://unturned.fandom.com/wiki/Hosting_a_Dedicated_Server#Port_Forwarding.

Then set `enable_high_speed_support` to `true` in the `server_config.json` file to enable the feature.


# Compiling

## Target
Ensure you have .NET Framework 4.8.1 installed.

## Strong Naming

This assembly is strongly signed (when built in Release mode). The public key is located at `~\devkitserver.dll.publickey`.
To compile it, you will need to either build in debug mode, disable strong naming in the **Build -> Strong Naming** settings, or generate your own key.

### Generating a Strong Name Key/Pair

Create a .snk file. This file or its contents should not be shared.

```
Developer Powershell > cd "Location outside of repository"
Developer Powershell > sn -k devkitserver.dll.snk
Developer Powershell > sn -p devkitserver.dll.snk devkitserver.dll.publickey
```

Copy the public key to your fork and replace the existing one if you want.

Set the `AssemblyOriginatorKeyFile` property to the path of the .snk file (or set it in the project settings).

## Reference Assemblies

Expected installations:

Unturned Client - `C:\Program Files (x86)\Steam\steamapps\common\Unturned`<br>
U3DS - `C:\SteamCMD\steamapps\common\U3DS`

Locations can be changed in `~\Unturned.targets`, which will apply to all 5 projects:
```xml
<!-- Installations | CONFIGURE YOUR INSTALLATION PATHS HERE -->
<PropertyGroup>
    <ServerPath>C:\SteamCMD\steamapps\common\U3DS</ServerPath>
    <ClientPath>C:\Program Files (x86)\Steam\steamapps\common\Unturned</ClientPath>
</PropertyGroup>
```

## Build Output

The assemblies, symbol files, and documentation files will be copied to the following locations post-build:

ClientDebug, ClientRelease: `C:\Program Files (x86)\Steam\steamapps\common\Unturned\Modules\DevkitServer\Bin\`<br>
ServerDebug, ServerRelease: `C:\SteamCMD\steamapps\common\U3DS\Modules\DevkitServer\Bin\`<br>

These can be changed in the `~\Unturned.targets` the same way described above.

I suggest setting up Batch Build to build both ClientDebug and ServerDebug (or the release variants if desired).

# Licensing Info
    DevkitServer - Module for Unturned that enables multi-user map editing.
    Copyright (C) 2023  Daniel Willett

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
