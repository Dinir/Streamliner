using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using BallisticUnityTools.AssetApi;
using BallisticUnityTools.Placeholders;
using NgModding;
using NgModding.Huds;
using NgData;
using NgSettings;
using NgUi.MenuUi;
using NgUi.RaceUi;
using NgEvents;
using NgPickups;
using NgLib;
using NgShips;
using NgModes;
using NgGame;
using NgSp;
using static Streamliner.HudRegister;

namespace Streamliner
{
	public class HudRegister : CodeMod
	{
		public readonly string Id = "Streamliner";
		public static ModAssets Assets;
		private static string _modPathOnClassScope;

		// options
		private string _settingsPath;
		public static int OptionValueTint;
		public static bool OptionMotion = true;
		public static bool OptionSpeedLapEmphasise = true;
		public static bool OptionSpeedHighlight = true;
		public static bool OptionEnergyChange = true;
		public static int OptionLowEnergy = 1;
		public static bool OptionRechargeAmount = true;

		public override void OnRegistered(string modPath)
		{
			_modPathOnClassScope = modPath;

			// load from files
			_settingsPath = Path.Combine(modPath, "settings.ini");

			Assets = ModAssets.Load(
				Path.Combine(modPath, "streamliner.nga"));

			// register the mod
			CustomHudRegistry.RegisterMod(Id);

			RegisterManagers();

			// options
			ModOptions.OnLoadSettings += OnLoadSettings;
			ModOptions.OnSaveSettings += OnSaveSettings;

			ModOptions.RegisterMod("Streamliner", GenerateModUi, ModUiToCode);
		}

		private void RegisterManagers()
		{
			CustomHudRegistry.RegisterSceneManager(
				"Race", Id, new RaceHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Team Race", Id, new TeamRaceHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Time Trial", Id, new TrialHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Speed Lap", Id, new SpeedLapHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Survival", Id, new ZoneHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Knockout", Id, new KnockoutHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Eliminator", Id, new CombatHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Upsurge", Id, new UpsurgeHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Rush Hour", Id, new RushHourHudManager());
		}

		private void GenerateModUi(ModOptionsUiContext ctx)
		{
			ctx.GenerateHeader("Display");

			ctx.GenerateSelector(
				"TextTint", "Text Tint",
				"Change the colour of the texts.",
				OptionValueTint,
				"white", "red", "orange", "yellow", "lime", "green", "mint", "cyan", "azure", "blue", "violet", "magenta", "rose"
			);

			ctx.GenerateSelector(
				"Motion", "motion effect",
				"Loosen the hud a bit.",
				OptionMotion ? 1 : 0,
				"off", "on"
			);

			ctx.GenerateSelector(
				"SpeedLapEmphasise", "speed lap best time emphasise",
				"Put the timer on top middle or bottom left of the screen.",
				OptionSpeedLapEmphasise ? 1 : 0,
				"off", "on"
			);

			ctx.GenerateHeader("Additional Information");

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
				"Change the color of energy meter when the ship's energy is low.",
				OptionLowEnergy,
				"off", "follow audio setting", "on"
			);

			ctx.GenerateSelector(
				"RechargeAmount", "recharge amount",
				"Show the amount the ship recharged on a pit lane.",
				OptionRechargeAmount ? 1 : 0,
				"off", "on"
			);
		}

		private void ModUiToCode(ModOptionsUiContext ctx)
		{
			OptionValueTint = ctx.GetSelectorValue("TextTint");
			OptionMotion = ctx.GetSelectorValue("Motion") == 1;
			OptionSpeedLapEmphasise = ctx.GetSelectorValue("SpeedLapEmphasise") == 1;
			OptionSpeedHighlight = ctx.GetSelectorValue("SpeedHighlight") == 1;
			OptionEnergyChange = ctx.GetSelectorValue("EnergyChange") == 1;
			OptionLowEnergy = ctx.GetSelectorValue("LowEnergyTransition");
			OptionRechargeAmount = ctx.GetSelectorValue("RechargeAmount") == 1;
		}

		private void OnLoadSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(_settingsPath);

			OptionValueTint = ini.ReadValue("Display", "TextTint", OptionValueTint);
			OptionMotion = ini.ReadValue("Display", "Motion", OptionMotion);
			OptionSpeedLapEmphasise = ini.ReadValue("Display", "SpeedLapEmphasise", OptionMotion);
			OptionSpeedHighlight = ini.ReadValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);
			OptionEnergyChange = ini.ReadValue("AdditionalInformation", "EnergyChange", OptionEnergyChange);
			OptionLowEnergy = ini.ReadValue("AdditionalInformation", "LowEnergyTransition", OptionLowEnergy);
			OptionRechargeAmount = ini.ReadValue("AdditionalInformation", "RechargeAmount", OptionRechargeAmount);

			ini.Close();
		}

		private void OnSaveSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(_settingsPath);

			ini.WriteValue("Display", "TextTint", OptionValueTint);
			ini.WriteValue("Display", "Motion", OptionMotion);
			ini.WriteValue("Display", "SpeedLapEmphasise", OptionMotion);
			ini.WriteValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);
			ini.WriteValue("AdditionalInformation", "EnergyChange", OptionEnergyChange);
			ini.WriteValue("AdditionalInformation", "LowEnergyTransition", OptionLowEnergy);
			ini.WriteValue("AdditionalInformation", "RechargeAmount", OptionRechargeAmount);

			ini.Close();
		}
	}

	/*
	 * Brief Table of Components for Gamemodes
	 * For an accurate list, you gotta check where each classes of `NgUi.RaceUi.HUD` are loaded.
	 * But just looking at the screen already gives me enough to start off,
	 * so making an accurate list was not worth to invest more time.
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
	 * Message    V  V  V  V  V  V  V  ?  V
	 * Pickup     V  V  .  V  .  .  V  .  V
	 * Board      V1 V2 .  .  .  V1 V  V2 V
	 * Awards     .  .  V3 .  V3 .  .  .  .
	 * 1: when `Hud.GetPositionBoardEnabled()` is `true`
	 * 2: replace the default one with a variation
	 * 3: when `NgCampaign.Enabled` is `true`
	 */

	public class RaceHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Timer>(HudRegister.Assets.GetComponent<HudComponents>("Timer", false));
			RegisterHud<BestTime>(HudRegister.Assets.GetComponent<HudComponents>("BestTime", false));
			RegisterHud<Placement>(HudRegister.Assets.GetComponent<HudComponents>("Placement", false));
			RegisterHud<LapCounter>(HudRegister.Assets.GetComponent<HudComponents>("Laps", false));
			RegisterHud<PositionTracker>(HudRegister.Assets.GetComponent<HudComponents>("Position", false));
			RegisterHud<Pitlane>(HudRegister.Assets.GetComponent<HudComponents>("Pitlane", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<PickupDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Pickup", false));
			if (Hud.GetPositionBoardEnabled())
				RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
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
			// RegisterHud<TeamScoreboard>(HudRegister.Assets.GetComponent<HudComponents>("Teamboard", false));
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
			RegisterHud<BestTime>(HudRegister.Assets.GetComponent<HudComponents>("BestTime", false));
			RegisterHud<LapCounter>(HudRegister.Assets.GetComponent<HudComponents>("Laps", false));
			RegisterHud<Pitlane>(HudRegister.Assets.GetComponent<HudComponents>("Pitlane", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			if (NgCampaign.Enabled)
				RegisterHud<Awards>(HudRegister.Assets.GetComponent<HudComponents>("Awards", false));
		}
	}

	public class SpeedLapHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<LapTimer>(HudRegister.Assets.GetComponent<HudComponents>("Timer", false));
			RegisterHud<BestTime>(HudRegister.Assets.GetComponent<HudComponents>("BestTime", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<TurboDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Turbo", false));
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
			if (Hud.GetPositionBoardEnabled())
				RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
			RegisterInternalHud("RespawnDarkener");
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
			RegisterInternalHud("RespawnDarkener");
		}
	}

	public class UpsurgeHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<ZoneEnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("ZoneEnergy", false));
			RegisterHud<UpsurgeTracker>(HudRegister.Assets.GetComponent<HudComponents>("Zone", false));
			RegisterHud<MessageLogger>(HudRegister.Assets.GetComponent<HudComponents>("Messages", false));
			RegisterHud<UpsurgeScoreboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
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
			RegisterHud<PickupDisplay>(HudRegister.Assets.GetComponent<HudComponents>("Pickup", false));
			RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
			RegisterInternalHud("RespawnDarkener");
		}
	}
}