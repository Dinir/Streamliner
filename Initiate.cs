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

		// color options
		private static readonly Color[] TintColorList = new Color[13] {
			// S2 V2
			Color.HSVToRGB(0.9944f, 0.00f, 0.86f), // Grey
			Color.HSVToRGB(0.9944f, 0.18f, 0.86f), // Red
			Color.HSVToRGB(0.0778f, 0.18f, 0.86f), // Orange
			Color.HSVToRGB(0.1611f, 0.18f, 0.86f), // Yellow
			Color.HSVToRGB(0.2444f, 0.18f, 0.86f), // Lime
			Color.HSVToRGB(0.3278f, 0.18f, 0.86f), // Green
			Color.HSVToRGB(0.4111f, 0.18f, 0.86f), // Mint
			Color.HSVToRGB(0.4944f, 0.18f, 0.86f), // Cyan
			Color.HSVToRGB(0.5778f, 0.18f, 0.86f), // Azure
			Color.HSVToRGB(0.6611f, 0.18f, 0.86f), // Blue
			Color.HSVToRGB(0.7444f, 0.18f, 0.86f), // Violet
			Color.HSVToRGB(0.8278f, 0.18f, 0.86f), // Magenta
			Color.HSVToRGB(0.9111f, 0.18f, 0.86f)  // Rose
		};
		private static readonly float[] TintAlphaList = new float[] {
			1f, 0.9f, 0.750f, 0.500f, 0.375f, 0.250f
		};
		internal enum TextAlpha
		{
			Full, NineTenths, ThreeQuarters, Half, ThreeEighths, Quarter
		}
		private static Color _tintColorBuffer;

		// options
		private string _settingsPath;
		public static int OptionValueTint = 0;
		public static bool OptionSpeedHighlight = true;
		public static int OptionLowEnergy = 2;

		internal static Color GetTintColor(TextAlpha transparencyIndex = TextAlpha.Full)
		{
			_tintColorBuffer = TintColorList[OptionValueTint];
			_tintColorBuffer.a = TintAlphaList[(int)transparencyIndex];
			return _tintColorBuffer;
		}

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
			CustomHudRegistry.RegisterSceneManager(
				"Time Trial", Id, new TrialHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Eliminator", Id, new CombatHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Team Race", Id, new TeamRaceHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Survival", Id, new ZoneHudManager());
			CustomHudRegistry.RegisterSceneManager(
				"Upsurge", Id, new UpsurgeHudManager());
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

			ctx.GenerateHeader("Additional Information");

			ctx.GenerateSelector(
				"SpeedHighlight", "speed reduction highlight",
				"Highlight speedometer text when speed decreases.",
				OptionSpeedHighlight ? 1 : 0,
				"off", "on"
			);
			ctx.GenerateSelector(
				"LowEnergyIndicator", "low energy indicator",
				"Change when the low energy indicator turns on.",
				OptionLowEnergy,
				"off", "follow audio setting", "on"
			);
		}

		private void ModUiToCode(ModOptionsUiContext ctx)
		{
			OptionValueTint = ctx.GetSelectorValue("TextTint");
			OptionSpeedHighlight = ctx.GetSelectorValue("SpeedHighlight") == 1;
			OptionLowEnergy = ctx.GetSelectorValue("LowEnergyIndicator");
		}

		private void OnLoadSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(_settingsPath);

			OptionValueTint = ini.ReadValue("Display", "TextTint", OptionValueTint);
			OptionSpeedHighlight = ini.ReadValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);
			OptionLowEnergy = ini.ReadValue("AdditionalInformation", "LowEnergyIndicator", OptionLowEnergy);

			ini.Close();
		}

		private void OnSaveSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(_settingsPath);

			ini.WriteValue("Display", "TextTint", OptionValueTint);
			ini.WriteValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);
			ini.WriteValue("AdditionalInformation", "LowEnergyIndicator", OptionLowEnergy);

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

	}

	public class RushHourHudManager : SceneHudManager
	{

	}
}