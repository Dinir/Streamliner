# ![Logo Light]![Logo Dark]

custom hud for [BallisticNG], made to streamline your racing process

## Video Showcase (1.0.0) *<sub>please open in a new tab/window</sub>*

[![Video Link Thumbnail]][Video Link]

## [Download][Download Link]

- - -

## Features

- ### Big numbers, lines, centred message display,
  to help you maintain your focus by reducing the time you spend looking away to read the hud.
- ### Dynamic color-shifting speedometer and energymeter,
  to indicate speed losses and energy changes.
- ### 4:3 area hud position,
  so everything stays closer to the centre of the screen, and the hud will have the same look in 4:3 screen resolutions. Also applies to splitscreen.
- ### Proximity position tracker.
- ### Shorter time difference format.
  `-0:04.22` => `-4.22s`
- ### Target time display,
  which shows you the average lap time of your best time, and starts counting down as you start a new lap. It adds up any saves and loses at every new lap, to help you keep the pace on Time Trial (activate it with the mod option).
- ### Scrolling lap time display,
  so you can actually see your 5th lap time in a longer race.
- ### Motion effect,
  of which the intensity can be adjusted.
- ### Text colour options.
  White, evenly spaced 12 hues, ship trail colour, and survival palette override.
- ### Splitscreen support.
- ### Texts are rendered with Unity's default Text component,
  so they will display well on the native Linux version.

## Limitations

Mods of the game can't overcome these at the moment.

- Upsurge game mode will fail to properly pause the game.
- Weapon warning is, even in the vanilla hud, not properly available in multiplayer races. It only works when a bot gets a pickup.
- Steamworks features are blocked when Code Mods are running. This includes submitting records to the leaderboards and Steam based multiplayer races.

- - -

## How to Install

The hud is made for version 1.3 of the game, and it's available in its beta branch on Steam at the moment.  
Please switch the game to the beta version to use this hud.

Click [here][Download Link] or *Releases* at the right side of the page to get the **7z** archive file.  
The archive file contains a folder for weapon sprites, an `nga`, a readme text `md`, and a `dll`.

1. Extract the entire folder into the game's Code Modes folder.  
    `<Steam Library>\steamapps\common\BallisticNG\User\Mods\Code Mods\`
2. Activate the hud.  
Run the game and go to *mod - manage mods*, then set as follow:
    > activated: **On**  
    > always recompile: **Off**
3. Restart the game to load the hud.
4. Change the hud style.  
Go to *config - game - hud - style - hud style* and set it to Streamliner.
5. Optionally, adjust the hud in the mod options.  
Go to *config - game - mods - mod select* and select Streamliner
to access its mod options.  
Most options require restarting the race if changed in-game.
Options that can be applied immediately are noted in their tooltips.

### How to Update

This hud is occasionally updated, but I don't have a decent place I can announce the updates on.  
If you have a GitHub account, consider *Watch* and customize it to only notify you on new releases. Otherwise, please check this repository once every while.

You can overwrite the older files with ones from the newer release.  
If something is broken in the newer version, go back to the older version and please notify me. Older versions are available on *Releases*.

- - -

## Credits

- Font [**Sector Gamma** by Lemon_orenoyome][Sector Gamma]  
  The whole journey of making the hud was begun only because I discovered this font. And the advertisement image for it on the booth page. I needed to make it happen.

- Font [**Spire NBP** by total FontGeek][Spire NBP]

- +Revenant+ for **Making a Custom Hud for BallisticNG**  
  It's the only tutorial that has ever existed for this. He also helped me adapting to the 1.3 code base changes to make something finally appear on the game screen for the first time.

And thank you for trying Streamliner.

- - -

- - -

## Source Code

There are not much available custom huds for this game, so it was hopeless to get any helps on making one. I managed to finish it, and I want to make the source code public in hope that anyone else who wants to dare into doing the same can get a reference to look at when they are stuck.

### Details

- `Assets` are Unity assets of the hud. You can open `Assets/Streamliner.unity` Scene to see each hud components. I excluded font files to avoid violating any rights of the font authors.
- `WeaponSprites` are the sprite image files the game will load.
- The three cs files are the scripts running the hud.
  - `Initiate.cs` handles registering the hud and the mod options.
  - `Component.cs` contains scripts for the hud components.
  - `Panel.cs` contains classes used by several hud component scripts.
  - `Shifter.cs` contains a static class and a corresponding hud class for the motion effect.

Assets are made with **Unity 2018.3.8f1**, and scripts are built with **.NET Framework v4.7**.  
The scripts use libraries from **version 1.3-d38 of the game**.

I separated `Shifter.cs` because it can be used outside of Streamliner. If there's anyone who wants to add a motion effect to a custom hud they are making, and find mine useful, I want to be a help by sharing this file. The main condition for this -- or deriving from any of my code for your custom hud -- is that you should also make your source code publicly available. Check the license for details.

### Getting Started

To learn how to make an environment to properly see what's happening, check out [Install / Update] and [Getting Started] page on the documentation.

You can also check out +Revenant+'s tutorial to kickstart the progress. It's available in the official discord server. The tutorial will take you through making a speedometer and a pickup display. Just be aware that there was a code base refactoring at a later time, so the initiation process explained on the tutorial is not applied anymore.

#### About Initiation on Unity

You don't need to Add Component *<BallisticNG/Custom HUDs/Custom Hud Builder>* to Main Camera, and instead you will make the Asset Bundle after making enough hud components, by right-clicking at Project tab then going to *BallisticNG - Create New Mod Assets Container*. You can add hud components to the container by clicking *Add* on its Inspector tab, then selecting prefabs of the components as *Object* of each added Assets.

When building the container you made with some components in it, the file name will be lowercased even when you gave a different name with capital letters. I lost several hours and days at first because of this, so let's not do that.



[BallisticNG]: https://neognosis.games/ballisticng/

[Logo Light]: https://user-images.githubusercontent.com/9097044/166197733-4496aa2b-60dd-41f1-9159-be6825104804.png#gh-dark-mode-only "Streamliner"
[Logo Dark]: https://user-images.githubusercontent.com/9097044/166199831-a0aa4715-c40e-4e87-bbde-23e2b8641dd0.png#gh-light-mode-only "Streamliner"
[Video Link]: https://youtu.be/Wec8Eni6N9M
[Video Link Thumbnail]: https://user-images.githubusercontent.com/9097044/166496210-d628d058-d590-4cb0-a42c-51d0ff7663db.png "Click to watch"
[Download Link]: ../../releases/latest

[Sector Gamma]: https://zipangcomplex.booth.pm/items/3307757
[Spire NBP]: https://sites.google.com/site/totalfontgeek/nbp-fonts/spire-nbp

[Install / Update]: https://ballisticng-documentation.readthedocs.io/en/latest/unity_tools/install_update.html
[Getting Started]: https://ballisticng-documentation.readthedocs.io/en/latest/code_mods/getting_started.html
