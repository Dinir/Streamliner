# ![Logo Light]![Logo Dark]

custom hud for [BallisticNG], made to streamline your racing process

## Video Showcase *<sub>please open in a new tab/window</sub>*

[![Video Link Thumbnail]][Video Link]

## [Download][Download Link]

- - -

## Features

- Big numbers and lines, and message display at the top centre of the screen will help you catch up what's happening, and minimize the time you spend looking away reading the hud while driving.
- Speedometer and energymeter will change their colour to help you notice the changes. Check when you are losing speed, and how much energy you got from the pit lane.
- Position tracker shows the proximity of other ships.
- Timer counting down from the average lap time of your best record, adding up any saves and loses at every new lap, to help you keep the pace on Time Trial.
- Lap time slots roll over by one lap at a time, so you can actually see your 5th lap time in a longer race.
- Motion effect to improve the *ＩＭＭＥＲＳＩＯＮ*.
- Text colour can be set as one of 12 available hues, or greyscale.
- All of the features above can be adjusted in the mod options.
- Texts are rendered with Unity's default Text component, so hopefully it will display well on the native Linux version.
- The hud is designed to be inside a central 4:3 area of the screen. It will have the same look in 4:3 screen resolutions.

## Limitations

Mods of the game can't overcome these at the moment.

- Upsurge game mode will fail to properly pause the game.
- Weapon warning is, even in the vanilla hud, not properly available in multiplayer races. It only works when a bot gets a pickup.
- The shake effect can't be triggered when the ship hits the track surface hard enough without falling from a high enough height.
- Splitscreen mode is not supported.
- Steamworks features are blocked when Code Mods are running. This includes submitting records to the leaderboards and Steam based multiplayer races.

- - -

## How to Install

The hud is made for version 1.3 of the game, and it's available in its beta branch on Steam at the moment. Please switch the game to the beta version before trying this hud.

Click [here][Download Link] or *Releases* at the right side of the page to get the archive file. Then extract and copy the entire folder to the game's Code Mods folder:

```
<Steam Library>\steamapps\common\BallisticNG\User\Mods\Code Mods\
```

Run the game and go to *mod - manage mods* to *activate* Streamliner. Keep *always recompile* off. Restart the game to get it properly loaded.

- - -

## Credits

- Font [**Sector Gamma** by Lemon_orenoyome][Sector Gamma]  
  The whole journey of making the hud was begun only because I discovered this font. And the advertisement image for it on the booth page. I needed to make it happen.

- Font [**Spire NBP** by total FontGeek][Spire NBP]

- +Revenant+ for **Making a Custom Hud for BallisticNG**  
  It's the only tutorial that has ever existed for this. He also helped me adapting to the 1.3 code base changes to make something finally appear on the game screen for the first time.

And thank you for trying Streamliner.

- - -

## Source Code

There are not much available custom huds for this game, so it was hopeless to get any helps on making one. I managed to finish it, and I want to make the source code public in hope that anyone else who wants to dare into doing the same can get a reference to look at when they are stuck.

### Details

- `Assets` are Unity assets of the hud. You can open `Assets/Streamliner.unity` Scene to see each hud components. I excluded font files to avoid violating any rights of the font authors.
- `WeaponSprites` are the sprites image files the game will load.
- The three cs files are the scripts running the hud.
  - `Initiate.cs` handles the process loading the hud and the mod options.
  - `Component.cs` contains scripts for the hud components.
  - `Panel.cs` contains classes used by several hud component scripts.

Assets are made with **Unity 2018.3.8f1**, and scripts are built with **.NET Framework v4.7**.  
The scripts use libraries from **version 1.2.5 of the game**, which is in the stable branch at the moment.

### Getting Started

To learn how to make an environment to properly see what's happening, check out [Install / Update] and [Getting Started] page on the documentation.

You can also check out +Revenant+'s tutorial to kickstart the progress. It's available in [the official discord server][Discord Server]. The tutorial will take you through making a speedometer and a pickup display. Just be aware that there was a code base refactoring at a later time, so the initiation process explained on the tutorial is not applied anymore.

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
[Discord Server]: https://discord.gg/ballisticng
