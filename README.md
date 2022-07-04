# ![Logo Light]![Logo Dark]

custom hud for [BallisticNG], made to streamline your racing process

## Video Showcase (1.0.0) *<sub>please open in a new tab/window</sub>*

[![Video Link Thumbnail]][Video Link]

## [Download][Download Link]

- - -

## Features

- **Full support for the base game**,  
  including all base game modes and related base game options as of 1.3. It also works on the native Linux build as the hud uses Unity's default text component.

- **Big numbers, lines, and centred message display**  
  will help you maintain your focus by reducing the time you spend looking away to read the hud. The components also stay in the 4:3 area, so they are closer to the centre of the screen and keep the same look in 4:3 screen resolutions.
- **Text colour options**:  
  white, ship trail colour, evenly spaced 12 hues. There's also an option for the Survival palette to override it.
- **Color-shifting speedometer and energymeter**  
  indicate any speed losses and energy changes. Energymeter can also display the regained amount.
- **Proximity position tracker**  
  shows the distance between you and other ships over the laps.
- **Scrolling lap time display**  
  will let you actually see your 5th lap time in a longer race.
- **Target time display for Time Trial and Speed Lap**  
  shows you how you perform compared to your best time. In Time Trial, it will count down from your best average lap time and add up any differences you made at every new lap.
- **Shorter time difference format** (`-4.22s`).

- **Motion effect**  
  reacts to wall collisions, scraping, landings, bottom-outs, and any big speed losses. If it's shaking, you might need to be improvising your racing line.
- **Support for splitscreen**,  
  or the only way to play other game modes with one friend of yours.
- **Customization with mod options**:  
  you can adjust the intensity of each motion effect kinds and toggle most of the additional features.

## Limitations

Mods of the game can't overcome these at the moment.

- Upsurge game mode will fail to properly pause the game.
- Weapon warning is, even in the vanilla hud, not properly available in multiplayer races. It only works when a bot gets a pickup.
- Steamworks features are blocked when code mods are running. This includes submitting records to the leaderboards and Steam based multiplayer races.

- - -

## How to Install

The hud is made for version 1.3 of the game, and it's available in its beta branch on Steam at the moment.  
Please switch the game to the beta version to use this hud.

Click [here][Download Link] or *Releases* at the right side of the page to get the **7z** archive file.  
The archive file contains a folder for weapon sprites, an `nga`, a readme text `md`, and a `dll`.

1. Extract the entire folder into the game's code mods folder.  
    `<Steam Library>\steamapps\common\BallisticNG\User\Mods\Code Mods\`  
The dll should be accessible as `Streamliner\Streamliner.dll` from the folder.
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

This is one of the big projects I did in my life, and I have a donation link for supporting my creative activities, so I decided to put the link in this repository. But I am not asking you to donate for this hud, nor will any parts of the hud in-game. The primary purpose of putting the link is to have a way to display people who supported me as a person, to show them my gratitude.

- - -

## Source Code

There are not much available custom huds for this game, so it was hopeless to get any helps on making one. I managed to finish it, and I want to make the source code public in hope that anyone else who wants to dare into doing the same can get a reference to look at when they are stuck.

### Details

- `Assets` are Unity assets of the hud. You can open `Assets/Streamliner.unity` Scene to see each hud components. I excluded font files to avoid violating any rights of the font authors.
- `WeaponSprites` are the sprite image files the game will load.
- The cs files are the scripts running the hud.
  - `Initiate.cs` handles registering the hud and the mod options.
  - `Component.cs` contains scripts for the hud components.
  - `Panel.cs` contains classes used by several hud component scripts.
  - `Shifter.cs` contains a static class and a corresponding hud class for the motion effect.

Assets are made with **Unity 2018.3.8f1**, and scripts are built with **.NET Framework v4.7**.[^version_difference]  
The scripts use libraries from **version 1.3-d38 of the game**.

I separated `Shifter.cs` because it can be used outside of Streamliner. If there's anyone who wants to add a motion effect to a custom hud they are making, and find mine useful, I want to be a help by sharing this file. The main condition for this -- or deriving from any of my code for your custom hud -- is that you should also make your source code publicly available. Check the license for details.

[^version_difference]: The framework version should've been v4.6, but when I was trying to create a project in Visual Studio I couldn't choose that version. 
[The C# version for .NET Framework is 7.3][Compiler Defaults for Target Framework], but the scripts also use features from up to 10.0 yet the project builds successfully. I think the language version got changed when I switched to and used JetBrains Rider, which wouldn't have happened if Visual Studio didn't wipe out a huge chunk of code I wrote for several hours and SAVED….[^fyou_vs_honestly]

    So it's C# 10.0 the scripts were built with, for .NET Framework v4.7 as the target framework, for a game that works with .NET Framework v4.6. I am in awe that the mod works in the game. When I finally figured out how to make the scripts work on a different project, Streamliner was at 1.2. It's too late, by a release and two minor updates worth of time, to go back and get the code to work on .NET Framework v4.6. Again, I couldn't get any kinds of help at all the whole time I was working on the hud. And I doubt it will get any better in the future.

[^fyou_vs_honestly]: Because I clicked 'Undo Changes', after deliberately highlighting the block I wanted to undo because it's one of the new features in the preview version I really needed, and confirmed when it asked me if I want to undo changes for 1 ***item(s)***. Item can mean several things in that context, so I made sure I saved the file and reopened the dialog to confirm it. Ctrl+Z didn't work, and I suffered a strong headache.

[Compiler Defaults for Target Framework]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version#defaults

### Getting Started

To learn how to make an environment to properly see what's happening, first check out [Install / Update] and [Getting Started] page on the documentation. But make the project with target framework of v4.7 instead,[^version_difference] and add `<LangVersion>default</LangVersion>` to the `.csproj` file. The project properties file on my side, in the first `<PropertyGroup>`, looks like the one below. I still don't know how "default" can allow the code to use "latest" features though.

```xml
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    ...
    <RootNamespace>Streamliner</RootNamespace>
    <AssemblyName>Streamliner</AssemblyName>
    <TargetFrameworkVersion>v4.7</TargetFrameworkVersion>
    ...
    <LangVersion>default</LangVersion>
  </PropertyGroup>
```

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
