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
			CustomHudRegistry.RegisterSceneManager(
				"Speed Lap", Id, new SpeedLapHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Time Trial", Id, new TrialHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Eliminator", Id, new CombatHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Team Race", Id, new TeamRaceHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Survival", Id, new ZoneHudManager());
			// CustomHudRegistry.RegisterSceneManager(
			// 	"Upsurge", Id, new UpsurgeHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Knockout", Id, new KnockoutHudManager());
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
			ini.WriteValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);
			ini.WriteValue("AdditionalInformation", "EnergyChange", OptionEnergyChange);
			ini.WriteValue("AdditionalInformation", "LowEnergyTransition", OptionLowEnergy);
			ini.WriteValue("AdditionalInformation", "RechargeAmount", OptionRechargeAmount);

			ini.Close();
		}
	}

	public class RaceHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Timer>(HudRegister.Assets.GetComponent<HudComponents>("Timer", false));
			if (Hud.GetPositionBoardEnabled())
				RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
		}
	}

	public class SpeedLapHudManager : SceneHudManager
	{

	}

	public class TrialHudManager : SceneHudManager
	{

	}

	public class CombatHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
		}
	}

	public class TeamRaceHudManager : SceneHudManager
	{

	}

	public class ZoneHudManager : SceneHudManager
	{

	}

	public class UpsurgeHudManager : SceneHudManager
	{

	}

	public class KnockoutHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			if (Hud.GetPositionBoardEnabled())
				RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
		}
	}

	public class RushHourHudManager : SceneHudManager
	{
		public override void OnCreateHuds()
		{
			RegisterHud<Speedometer>(HudRegister.Assets.GetComponent<HudComponents>("Speed", false));
			RegisterHud<EnergyMeter>(HudRegister.Assets.GetComponent<HudComponents>("Energy", false));
			RegisterHud<Leaderboard>(HudRegister.Assets.GetComponent<HudComponents>("Leaderboard", false));
		}
	}
}