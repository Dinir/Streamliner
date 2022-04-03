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

		// options
		public static Color[,] TintColorList = new Color[13, 5] {
				{
					new Color32(0xe4, 0xe4, 0xe4, 0xff),
					new Color32(0xe4, 0xe4, 0xe4, 0xbf),
					new Color32(0xe4, 0xe4, 0xe4, 0x7f),
					new Color32(0xe4, 0xe4, 0xe4, 0x5f),
					new Color32(0xe4, 0xe4, 0xe4, 0x3f)
				},
				{
					new Color32(0xe4, 0xcf, 0xcf, 0xff),
					new Color32(0xe4, 0xcf, 0xcf, 0xbf),
					new Color32(0xe4, 0xcf, 0xcf, 0x7f),
					new Color32(0xe4, 0xcf, 0xcf, 0x5f),
					new Color32(0xe4, 0xcf, 0xcf, 0x3f)
				},
				{
					new Color32(0xe4, 0xd9, 0xcf, 0xff),
					new Color32(0xe4, 0xd9, 0xcf, 0xbf),
					new Color32(0xe4, 0xd9, 0xcf, 0x7f),
					new Color32(0xe4, 0xd9, 0xcf, 0x5f),
					new Color32(0xe4, 0xd9, 0xcf, 0x3f)
				},
				{
					new Color32(0xe4, 0xe3, 0xcf, 0xff),
					new Color32(0xe4, 0xe3, 0xcf, 0xbf),
					new Color32(0xe4, 0xe3, 0xcf, 0x7f),
					new Color32(0xe4, 0xe3, 0xcf, 0x5f),
					new Color32(0xe4, 0xe3, 0xcf, 0x3f)
				},
				{
					new Color32(0xda, 0xe4, 0xcf, 0xff),
					new Color32(0xda, 0xe4, 0xcf, 0xbf),
					new Color32(0xda, 0xe4, 0xcf, 0x7f),
					new Color32(0xda, 0xe4, 0xcf, 0x5f),
					new Color32(0xda, 0xe4, 0xcf, 0x3f)
				},
				{
					new Color32(0xcf, 0xe4, 0xcf, 0xff),
					new Color32(0xcf, 0xe4, 0xcf, 0xbf),
					new Color32(0xcf, 0xe4, 0xcf, 0x7f),
					new Color32(0xcf, 0xe4, 0xcf, 0x5f),
					new Color32(0xcf, 0xe4, 0xcf, 0x3f)
				},
				{
					new Color32(0xcf, 0xe4, 0xd9, 0xff),
					new Color32(0xcf, 0xe4, 0xd9, 0xbf),
					new Color32(0xcf, 0xe4, 0xd9, 0x7f),
					new Color32(0xcf, 0xe4, 0xd9, 0x5f),
					new Color32(0xcf, 0xe4, 0xd9, 0x3f)
				},
				{
					new Color32(0xcf, 0xe4, 0xe3, 0xff),
					new Color32(0xcf, 0xe4, 0xe3, 0xbf),
					new Color32(0xcf, 0xe4, 0xe3, 0x7f),
					new Color32(0xcf, 0xe4, 0xe3, 0x5f),
					new Color32(0xcf, 0xe4, 0xe3, 0x3f)
				},
				{
					new Color32(0xcf, 0xda, 0xe4, 0xff),
					new Color32(0xcf, 0xda, 0xe4, 0xbf),
					new Color32(0xcf, 0xda, 0xe4, 0x7f),
					new Color32(0xcf, 0xda, 0xe4, 0x5f),
					new Color32(0xcf, 0xda, 0xe4, 0x3f)
				},
				{
					new Color32(0xcf, 0xcf, 0xe4, 0xff),
					new Color32(0xcf, 0xcf, 0xe4, 0xbf),
					new Color32(0xcf, 0xcf, 0xe4, 0x7f),
					new Color32(0xcf, 0xcf, 0xe4, 0x5f),
					new Color32(0xcf, 0xcf, 0xe4, 0x3f)
				},
				{
					new Color32(0xd9, 0xcf, 0xe4, 0xff),
					new Color32(0xd9, 0xcf, 0xe4, 0xbf),
					new Color32(0xd9, 0xcf, 0xe4, 0x7f),
					new Color32(0xd9, 0xcf, 0xe4, 0x5f),
					new Color32(0xd9, 0xcf, 0xe4, 0x3f)
				},
				{
					new Color32(0xe3, 0xcf, 0xe4, 0xff),
					new Color32(0xe3, 0xcf, 0xe4, 0xbf),
					new Color32(0xe3, 0xcf, 0xe4, 0x7f),
					new Color32(0xe3, 0xcf, 0xe4, 0x5f),
					new Color32(0xe3, 0xcf, 0xe4, 0x3f)
				},
				{
					new Color32(0xe4, 0xcf, 0xda, 0xff),
					new Color32(0xe4, 0xcf, 0xda, 0xbf),
					new Color32(0xe4, 0xcf, 0xda, 0x7f),
					new Color32(0xe4, 0xcf, 0xda, 0x5f),
					new Color32(0xe4, 0xcf, 0xda, 0x3f)
				}
			};
		private string SettingsPath;
		public static int OptionValueTint = 0;
		public static bool OptionSpeedHighlight = true;

		public enum TextAlpha
		{
			Full, ThreeQuarters, Half, ThreeEighths, Quarter
		}

		public static Color GetTintColor(TextAlpha transparencyIndex = TextAlpha.Full)
		{
			return TintColorList[OptionValueTint, (int)transparencyIndex];
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
		}

		private void ModUiToCode(ModOptionsUiContext ctx)
		{
			OptionValueTint = ctx.GetSelectorValue("TextTint");
			OptionSpeedHighlight = ctx.GetSelectorValue("SpeedHighlight") == 1;
		}

		private void OnLoadSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(SettingsPath);

			OptionValueTint = ini.ReadValue("Display", "TextTint", OptionValueTint);
			OptionSpeedHighlight = ini.ReadValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);

			ini.Close();
		}

		private void OnSaveSettings()
		{
			INIParser ini = new INIParser();
			ini.Open(SettingsPath);

			ini.WriteValue("Display", "TextTint", OptionValueTint);
			ini.WriteValue("AdditionalInformation", "SpeedHighlight", OptionSpeedHighlight);

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