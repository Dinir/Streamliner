using System.IO;
using BallisticUnityTools.AssetApi;
using BallisticUnityTools.Placeholders;
using NgModding;
using NgModding.Huds;
using NgMp;
using NgSettings;
using NgSp;
using NgUi.MenuUi;

namespace Streamliner
{
	public class HudRegister : CodeMod
	{
		// This name appears at "config - game - hud - style - hud style".
		private const string ID = "Streamliner 1.1.0-beta";
		public static ModAssets Assets;
		private static string _modPathOnClassScope;

		// options
		private string _settingsPath;
		private const string OptionSectionDisplay = "Display";
		private const string OptionSectionMotion = "Motion Effect";
		private const string OptionSectionAddition = "Additional Information";
		public static int OptionValueTint;
		public static bool OptionSurvivalTintOverride = true;
		public static int OptionTimeDiffColour;
		public static bool OptionPositionBoard = true;
		public static bool OptionMotion = true;
		public static float OptionShiftMultiplier = 1f;
		public static float OptionShakeMultiplier = 1f;
		public static float OptionScrapeMultiplier = 1f;
		public static bool OptionSpeedHighlight = true;
		public static bool OptionEnergyChange = true;
		public static int OptionLowEnergy = 1;
		public static bool OptionRechargeAmount = true;
		public static bool OptionTargetTimer = true;
		public static int OptionBestTime = 1;

		public override void OnRegistered(string modPath)
		{
			_modPathOnClassScope = modPath;

			_settingsPath = Path.Combine(modPath, "settings.ini");

			Assets = ModAssets.Load(
				Path.Combine(modPath, "mod assets.nga"));

			CustomHudRegistry.RegisterMod(ID);

			RegisterManagers();
			RegisterSprites();

			ModOptions.OnLoadSettings += OnLoadSettings;
			ModOptions.OnSaveSettings += OnSaveSettings;

			// This name appears at "config - game - mods - mod select - mod".
			ModOptions.RegisterMod("Streamliner", GenerateModUi, ModUiToCode);
		}

		private static void RegisterManagers()
		{
			CustomHudRegistry.RegisterSceneManager(
				"Race", ID, new RaceHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Team Race", ID, new TeamRaceHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Time Trial", ID, new TrialHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Speed Lap", ID, new SpeedLapHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Survival", ID, new ZoneHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Knockout", ID, new KnockoutHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Eliminator", ID, new CombatHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Upsurge", ID, new UpsurgeHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Rush Hour", ID, new RushHourHudManager());
		}

		private static void RegisterSprites()
		{
			CustomHudRegistry.RegisterWeaponSprite(
				"rockets", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/rockets.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"missile", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/missile.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"mines", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/mines.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"plasma", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/plasma.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"energywall", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/energywall.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"cannon", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/cannon.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"shield", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/shield.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"autopilot", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/autopilot.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"emergencypack", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/epack.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"tremor", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/tremor.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"turbo", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/turbo.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"hunter", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/hunter.png")));
			CustomHudRegistry.RegisterWeaponSprite(
				"hellstorm", ID, CustomHudRegistry.LoadSpriteFromDisk(
					Path.Combine(_modPathOnClassScope, "WeaponSprites/hellstorm.png")));
		}

		/*
		 * Settings Handling Flow
		 *
		 * Starting, restarting or quitting a race:
		 *	 OnSavePreferences -> OnLoadSettings -> OnLoadPreferences
		 *
		 * User enters any menus in "config":
		 *   OnLoadSettings -> GenerateModUi -> OnLoadSettings
		 *
		 * User selects "save" and exits from any menus in "config":
		 *   ModUiToCode -> OnSaveSettings -> OnSaveSettings
		 *
		 * Any value changes made by the user are applied in ModUiToCode.
		 */

		private void GenerateModUi(ModOptionsUiContext ctx)
		{
			ctx.GenerateHeader(OptionSectionDisplay);

			ctx.GenerateSelector(
				"TextTint", "text tint",
				"Change the colour of the texts.",
				OptionValueTint,
				"white", "red", "orange", "yellow", "lime", "green", "mint", "cyan", "azure", "blue", "violet", "magenta", "rose"
			);

			ctx.GenerateSelector(
				"SurvivalTintOverride", "survival mode tint",
				"Set Survival mode text tint.",
				OptionSurvivalTintOverride ? 1 : 0,
				"text tint", "tint from survival palette"
			);

			ctx.GenerateSelector(
				"TimeDiffColour", "time text colour",
				"Set the colour pair to use for the time difference text.",
				OptionTimeDiffColour,
				"red & green", "magenta & green", "red & cyan"
			);

			ctx.GenerateSpace();

			ctx.GenerateSelector(
				"PositionBoard", "position board",
				"Show the position listing HUD. Disabling this option will turn it off even in multiplayer races.",
				OptionPositionBoard ? 1 : 0,
				"off", "follow game setting"
			);

			ctx.GenerateHeader(OptionSectionMotion);

			ctx.GenerateSelector(
				"Motion", "motion effect",
				"Loosen the hud a bit.",
				OptionMotion ? 1 : 0,
				"off", "on"
			);

			ctx.GenerateSlider(
				"ShiftMultiplier", "shift intensity",
				"Set how intense smooth shifting of the hud should be.",
				0.0f, 2.0f, OptionShiftMultiplier, 0.1f,
				10, NgSlider.RoundMode.Round,
				10, NgSlider.RoundMode.Round
			);

			ctx.GenerateSlider(
				"ShakeMultiplier", "shake intensity",
				"Set how intense shake of the hud should be.",
				0.0f, 2.0f, OptionShakeMultiplier, 0.1f,
				10, NgSlider.RoundMode.Round,
				10, NgSlider.RoundMode.Round
			);

			ctx.GenerateSlider(
				"ScrapeMultiplier", "scrape intensity",
				"Set how intense shake of the hud should be when scraping.",
				0.0f, 2.0f, OptionScrapeMultiplier, 0.1f,
				10, NgSlider.RoundMode.Round,
				10, NgSlider.RoundMode.Round
			);

			ctx.GenerateHeader(OptionSectionAddition);

			ctx.GenerateSelector(
				"SpeedHighlight", "speed reduction",
				"Highlight speedometer when speed decreases.",
				OptionSpeedHighlight ? 1 : 0,
				"off", "on"
			);

			ctx.GenerateSpace();

			ctx.GenerateSelector(
				"EnergyChange", "energy change",
				"Highlight energy meter on damage or recharging.",
				OptionEnergyChange ? 1 : 0,
				"off", "on"
			);

			ctx.GenerateSelector(
				"LowEnergyTransition", "low energy",
				"Change the colour of energy meter when the ship's energy is low.",
				OptionLowEnergy,
				"off", "follow audio setting", "on"
			);

			ctx.GenerateSelector(
				"RechargeAmount", "recharge amount",
				"Show the amount the ship recharged on a pit lane.",
				OptionRechargeAmount ? 1 : 0,
				"off", "on"
			);

			ctx.GenerateSpace();

			ctx.GenerateSelector(
				"TargetTimer", "target timer",
				"Put a countdown timer on top of the screen on Time Trial and Speed Lap mode.",
				OptionTargetTimer ? 1 : 0,
				"off", "on"
			);

			ctx.GenerateSelector(
				"BestTime", "best time",
				"Set which time to show as the best time on Race and Time Trial mode.",
				OptionBestTime,
				"off", "total time", "lap time"
			);
		}

		private void ModUiToCode(ModOptionsUiContext ctx)
		{
			OptionValueTint = ctx.GetSelectorValue("TextTint");
			OptionSurvivalTintOverride = ctx.GetSelectorValue("SurvivalTintOverride") == 1;
			OptionTimeDiffColour = ctx.GetSelectorValue("TimeDiffColour");
			OptionPositionBoard = ctx.GetSelectorValue("PositionBoard") == 1;
			OptionMotion = ctx.GetSelectorValue("Motion") == 1;
			OptionShiftMultiplier = ctx.GetSliderValue("ShiftMultiplier");
			OptionShakeMultiplier = ctx.GetSliderValue("ShakeMultiplier");
			OptionScrapeMultiplier = ctx.GetSliderValue("ScrapeMultiplier");
			OptionSpeedHighlight = ctx.GetSelectorValue("SpeedHighlight") == 1;
			OptionEnergyChange = ctx.GetSelectorValue("EnergyChange") == 1;
			OptionLowEnergy = ctx.GetSelectorValue("LowEnergyTransition");
			OptionRechargeAmount = ctx.GetSelectorValue("RechargeAmount") == 1;
			OptionTargetTimer = ctx.GetSelectorValue("TargetTimer") == 1;
			OptionBestTime = ctx.GetSelectorValue("BestTime");

			Shifter.ApplySettings();
		}

		private void OnLoadSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(_settingsPath);

			OptionValueTint = ini.ReadValue(OptionSectionDisplay, "TextTint", OptionValueTint);
			OptionSurvivalTintOverride = ini.ReadValue(OptionSectionDisplay, "SurvivalTintOverride", OptionSurvivalTintOverride);
			OptionTimeDiffColour = ini.ReadValue(OptionSectionDisplay, "TimeDiffColour", OptionTimeDiffColour);
			OptionPositionBoard = ini.ReadValue(OptionSectionDisplay, "PositionBoard", OptionPositionBoard);
			OptionMotion = ini.ReadValue(OptionSectionMotion, "Motion", OptionMotion);
			OptionShiftMultiplier = (float) ini.ReadValue(OptionSectionMotion, "ShiftMultiplier", OptionShiftMultiplier);
			OptionShakeMultiplier = (float) ini.ReadValue(OptionSectionMotion, "ShakeMultiplier", OptionShakeMultiplier);
			OptionScrapeMultiplier = (float) ini.ReadValue(OptionSectionMotion, "ScrapeMultiplier", OptionScrapeMultiplier);
			OptionSpeedHighlight = ini.ReadValue(OptionSectionAddition, "SpeedHighlight", OptionSpeedHighlight);
			OptionEnergyChange = ini.ReadValue(OptionSectionAddition, "EnergyChange", OptionEnergyChange);
			OptionLowEnergy = ini.ReadValue(OptionSectionAddition, "LowEnergyTransition", OptionLowEnergy);
			OptionRechargeAmount = ini.ReadValue(OptionSectionAddition, "RechargeAmount", OptionRechargeAmount);
			OptionTargetTimer = ini.ReadValue(OptionSectionAddition, "TargetTimer", OptionTargetTimer);
			OptionBestTime = ini.ReadValue(OptionSectionAddition, "BestTime", OptionBestTime);

			ini.Close();

			Shifter.ApplySettings();
		}

		private void OnSaveSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(_settingsPath);

			ini.WriteValue(OptionSectionDisplay, "TextTint", OptionValueTint);
			ini.WriteValue(OptionSectionDisplay, "SurvivalTintOverride", OptionSurvivalTintOverride);
			ini.WriteValue(OptionSectionDisplay, "TimeDiffColour", OptionTimeDiffColour);
			ini.WriteValue(OptionSectionDisplay, "PositionBoard", OptionPositionBoard);
			ini.WriteValue(OptionSectionMotion, "Motion", OptionMotion);
			ini.WriteValue(OptionSectionMotion, "ShiftMultiplier", OptionShiftMultiplier);
			ini.WriteValue(OptionSectionMotion, "ShakeMultiplier", OptionShakeMultiplier);
			ini.WriteValue(OptionSectionMotion, "ScrapeMultiplier", OptionScrapeMultiplier);
			ini.WriteValue(OptionSectionAddition, "SpeedHighlight", OptionSpeedHighlight);
			ini.WriteValue(OptionSectionAddition, "EnergyChange", OptionEnergyChange);
			ini.WriteValue(OptionSectionAddition, "LowEnergyTransition", OptionLowEnergy);
			ini.WriteValue(OptionSectionAddition, "RechargeAmount", OptionRechargeAmount);
			ini.WriteValue(OptionSectionAddition, "TargetTimer", OptionTargetTimer);
			ini.WriteValue(OptionSectionAddition, "BestTime", OptionBestTime);

			ini.Close();
		}
	}

	/*
	 * Brief Table of Vanilla Hud Components for Gamemodes
	 * For an accurate list, you gotta check where each classes of `NgUi.RaceUi.HUD` are loaded.
	 *
	 *            R  TR TT SL SU KN EL UP RH
	 * Speed      V  V  V  V  V  V  V  V  V
	 * Energy     V  V  V  V  V  V  V  V  V
	 * Timer      V  .  V  V2 .  .  .  .  .
	 * ZoneEnergy .  .  .  .  .  .  .  V  .
	 * Zone       .  .  .  .  V  .  .  V2 .
	 * Placement  V  V  .  .  .  V  .  .  V
	 * Lap        V  V  V  .  .  .  .  .  .
	 * Position   V  V  .  .  .  V  V  .  V
	 * Pitlane    V  V  V  .  .  V  .  .  .
	 * Message    V  V  V  V  V  V  V  V  V
	 * Pickup     V  V  .  V  .  V  V  .  V
	 * Board      V1 V2 .  .  .  V1 V  V2 V
	 * Awards     .  .  V3 .  V3 .  .  .  .
	 *
	 * 1: when `Hud.GetPositionBoardEnabled()` is `true`
	 * 2: replaces the default one with a variation
	 * 3: when `NgCampaign.Enabled` is `true`
	 */

	public class RaceHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Timer>(HudRegister.Assets.GetComponent<HudComponents>("Timer", false));
			RegisterHud<TargetTime>(HudRegister.Assets.GetComponent<HudComponents>("TargetTime", false));
			RegisterHud<Placement>(HudRegister.Assets.GetComponent<HudComponents>("Placement", false));
			RegisterHud<LapCounter>(HudRegister.Assets.GetComponent<HudComponents>("Laps", false));
			RegisterHud<PositionTracker>(HudRegister.Assets.GetComponent<HudComponents>("Position", false));
			RegisterHud<Pitlane>(HudRegister.Assets.GetComponent<HudComponents>("Pitlane", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<PickupDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Pickup", false));
			if (HudRegister.OptionPositionBoard && Hud.GetPositionBoardEnabled())
				RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
			if (NgNetworkBase.CurrentNetwork)
			{
				RegisterHud<RaceFinishCountdown>(HudRegister.Assets.GetComponent<HudComponents>("RaceFinishTimer", false));
				RegisterInternalHud("NetworkWaitingList");
			}

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}

	public class TeamRaceHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Placement>(HudRegister.Assets.GetComponent<HudComponents>("Placement", false));
			RegisterHud<LapCounter>(HudRegister.Assets.GetComponent<HudComponents>("Laps", false));
			RegisterHud<PositionTracker>(HudRegister.Assets.GetComponent<HudComponents>("Position", false));
			RegisterHud<Pitlane>(HudRegister.Assets.GetComponent<HudComponents>("Pitlane", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<PickupDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Pickup", false));
			RegisterHud<TeamScoreboard>(HudRegister.Assets.GetComponent<HudComponents>("Teamboard", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}

	public class TrialHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Timer>(HudRegister.Assets.GetComponent<HudComponents>("Timer", false));
			RegisterHud<TargetTime>(HudRegister.Assets.GetComponent<HudComponents>("TargetTime", false));
			RegisterHud<LapCounter>(HudRegister.Assets.GetComponent<HudComponents>("Laps", false));
			RegisterHud<Pitlane>(HudRegister.Assets.GetComponent<HudComponents>("Pitlane", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			if (NgCampaign.Enabled)
				RegisterHud<Awards>(HudRegister.Assets.GetComponent<HudComponents>("Awards", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
		}
	}

	public class SpeedLapHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<LapTimer>(HudRegister.Assets.GetComponent<HudComponents>("Timer", false));
			RegisterHud<TargetTime>(HudRegister.Assets.GetComponent<HudComponents>("TargetTime", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<TurboDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Turbo", false));
			if (NgCampaign.Enabled)
				RegisterHud<Awards>(HudRegister.Assets.GetComponent<HudComponents>("Awards", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}

	public class ZoneHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<ZoneTracker>(HudRegister.Assets.GetComponent<HudComponents>("Zone", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			if (NgCampaign.Enabled)
				RegisterHud<Awards>(HudRegister.Assets.GetComponent<HudComponents>("Awards", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}

	public class KnockoutHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Placement>(HudRegister.Assets.GetComponent<HudComponents>("Placement", false));
			RegisterHud<PositionTracker>(HudRegister.Assets.GetComponent<HudComponents>("Position", false));
			RegisterHud<Pitlane>(HudRegister.Assets.GetComponent<HudComponents>("Pitlane", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<PickupDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Pickup", false));
			if (HudRegister.OptionPositionBoard && Hud.GetPositionBoardEnabled())
				RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
		}
	}

	public class CombatHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<PositionTracker>(HudRegister.Assets.GetComponent<HudComponents>("Position", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<PickupDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Pickup", false));
			RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}

	public class UpsurgeHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<UpsurgeTracker>(HudRegister.Assets.GetComponent<HudComponents>("Zone", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}

	public class RushHourHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Placement>(HudRegister.Assets.GetComponent<HudComponents>("Placement", false));
			RegisterHud<PositionTracker>(HudRegister.Assets.GetComponent<HudComponents>("Position", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<TurboDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Turbo", false));
			RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));

			RegisterHud<ShifterHud>(HudRegister.Assets.GetComponent<HudComponents>("Shifter", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}
}