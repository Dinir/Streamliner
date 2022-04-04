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
		readonly string Id = "Streamliner";
		public static ModAssets Assets;
		private static string ModPathOnClassScope;

        // color options
        public static readonly Color[] TintColorList = new Color[13] {
			Color.HSVToRGB(0.9944f, 0.00f, 0.86f),
			Color.HSVToRGB(0.9944f, 0.18f, 0.86f),
			Color.HSVToRGB(0.0778f, 0.18f, 0.86f),
			Color.HSVToRGB(0.1611f, 0.18f, 0.86f),
			Color.HSVToRGB(0.2444f, 0.18f, 0.86f),
			Color.HSVToRGB(0.3278f, 0.18f, 0.86f),
			Color.HSVToRGB(0.4111f, 0.18f, 0.86f),
			Color.HSVToRGB(0.4944f, 0.18f, 0.86f),
			Color.HSVToRGB(0.5778f, 0.18f, 0.86f),
			Color.HSVToRGB(0.6611f, 0.18f, 0.86f),
			Color.HSVToRGB(0.7444f, 0.18f, 0.86f),
			Color.HSVToRGB(0.8278f, 0.18f, 0.86f),
			Color.HSVToRGB(0.9111f, 0.18f, 0.86f)
		};
		public static readonly float[] TintAlphaList = new float[] {
			1f, 0.750f, 0.500f, 0.375f, 0.250f
		};
		public enum TextAlpha
				{
			Full, ThreeQuarters, Half, ThreeEighths, Quarter
				}
		private static Color _tintColorBuffer;

		// options
		private string SettingsPath;
		public static int OptionValueTint = 0;
		public static bool OptionSpeedHighlight = true;
		public static int OptionLowEnergy = 2;

		public static Color GetTintColor(TextAlpha transparencyIndex = TextAlpha.Full)
		{
			_tintColorBuffer = TintColorList[OptionValueTint];
			_tintColorBuffer.a = TintAlphaList[(int)transparencyIndex];
			return _tintColorBuffer;
		}

		public override void OnRegistered(string modPath)
		{
			ModPathOnClassScope = modPath;

			// load from files
			SettingsPath = Path.Combine(modPath, "settings.ini");

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
				"White", "Red", "Orange", "Yellow", "Lime", "Green", "Mint", "Cyan", "Azure", "Blue", "Violet", "Magenta", "Rose"
			);

			ctx.GenerateHeader("Additional Information");

			ctx.GenerateSelector(
				"SpeedHighlight", "Speed Reduction Highlight",
				"Highlight speedometer text when speed decreases.",
				OptionSpeedHighlight ? 1 : 0,
				"Off", "On"
			);
			ctx.GenerateSelector(
				"LowEnergyIndicator", "Low Energy Indicator",
				"Change when the low energy indicator turns on.",
				OptionLowEnergy,
				"Off", "Follow Audio Setting", "On"
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
			ini.Open(SettingsPath);

			OptionValueTint = ini.ReadValue("Display", "TextTint", OptionValueTint);
			OptionSpeedHighlight = ini.ReadValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);
			OptionLowEnergy = ini.ReadValue("AdditionalInformation", "LowEnergyIndicator", OptionLowEnergy);

			ini.Close();
		}

		private void OnSaveSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(SettingsPath);

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