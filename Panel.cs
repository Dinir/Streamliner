// Copyright © 2022 Dinir Nertan
// Licensed under the Open Software License version 3.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using BallisticUnityTools.Placeholders;
using NgLib;
using NgData;
using NgModding.Huds;
using NgModes;
using NgTrackData;
using NgShips;
using NgUi.RaceUi.HUD;
using NgVirtual;
using static Streamliner.HudRegister;
using static Streamliner.PresetColorPicker;
using static UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Streamliner
{
	internal static class Shifter
	{
		internal const int MaxPanelCount = 20;
		internal const int MaxPlayer = 2;

		internal const float DampTime = 0.2f;
		internal const float BaseShiftFactor = 3f;
		internal const float VerticalShiftEmphasis = -2f;
		internal const float MaxVerticalShiftAmount = 127.5f;
		internal const float BaseMaxLandingShakeAmount = 60f;
		internal const float BaseWallBounceShakeAmount = 30f;
		internal const float BaseScrapingShakeAmount = 6f;

		internal const float MinVerticalSpeedDiff = 10f;
		internal const float MaxVerticalSpeedDiff = 20f;
		internal static readonly float VerticalSpeedDiffRange =
			MaxVerticalSpeedDiff - MinVerticalSpeedDiff;

		// single cannon hit roughly decreases the speed by 9.5%
		internal const float SpeedChangeIntensityThreshold = 1 / 0.095f;
		internal const float MinSpeedChangeIntensity = 1f;

		// duration / decay speed == total lasting time in seconds
		internal const float ShakeDuration = 60f;
		internal const float ShakeDurationDecaySpeed = 240f;

		internal static float ShiftFactor = 3f;
		internal static float MaxLandingShakeAmount = 60f;
		internal static float WallBounceShakeAmount = 30f;
		internal static float ScrapingShakeAmount = 6f;

		private class AmountData
		{
			internal Vector2 ShiftTarget;
			internal float ShakeAmount;
			internal Vector2 ShakeVector;
			internal float ShakeDuration;

			internal Vector3 CurrentVelocity;
			internal Vector3 PreviousVelocity;
			internal float SpeedChangeIntensity;
		}

		internal static readonly List<ShipController> TargetShips = new(MaxPlayer);
		private static readonly List<AmountData> _amountData = new(MaxPlayer);
		internal static readonly List<List<Panel>> Panels = new(MaxPlayer);
		
		internal static void Flush()
		{
			for (int i = 0; i < MaxPlayer; i++)
			{
				TargetShips.Clear();
				_amountData[i] = new AmountData();
				Panels[i].Clear();
			}
		}

		static Shifter()
		{
			for (int i = 0; i < MaxPlayer; i++)
			{
				_amountData.Add(new AmountData());
				Panels.Add(new(MaxPanelCount));
			}
		}

		internal class Panel
		{
			private readonly RectTransform _rt;
			internal readonly string HudName;
			internal readonly Vector2 OriginPosition;
			internal Vector2 TargetPosition;
			internal Vector2 CurrentSpeed;
			internal Vector2 ShiftedPosition;
			internal Vector2 ShakingPosition;

			internal Vector2 Position => _rt.anchoredPosition;

			internal void SetTargetPosition(Vector2 position) =>
				TargetPosition = OriginPosition + position;
			internal void SetShiftedPosition(Vector2 position) =>
				ShiftedPosition = position;
			internal void SetShakingPosition(Vector2 position) =>
				ShakingPosition = ShiftedPosition + position;
			internal void ResetShakingPosition() =>
				ShakingPosition = ShiftedPosition;

			internal void SetPositionToShifted() =>
				_rt.anchoredPosition = ShiftedPosition;
			internal void SetPositionToShaking() =>
				_rt.anchoredPosition = ShakingPosition;
			internal void ResetPosition() =>
				_rt.anchoredPosition = OriginPosition;

			internal void Hide() => _rt.gameObject.SetActive(false);
			internal void Show() => _rt.gameObject.SetActive(true);

			internal Panel(RectTransform rt, string name)
			{
				_rt = rt;
				HudName = name;
				Vector2 anchoredPosition = rt.anchoredPosition;
				OriginPosition = anchoredPosition;
				ShiftedPosition = anchoredPosition;
			}
		}

		internal static void ApplySettings()
		{
			ShiftFactor = BaseShiftFactor * OptionShiftMultiplier;
			MaxLandingShakeAmount = BaseMaxLandingShakeAmount * OptionShakeMultiplier;
			WallBounceShakeAmount = BaseWallBounceShakeAmount * OptionShakeMultiplier;
			ScrapingShakeAmount = BaseScrapingShakeAmount * OptionScrapeMultiplier;
		}

		internal static void Add(RectTransform panel, int playerIndex, string name) =>
			Panels[playerIndex].Add(new Panel(panel, name));

		private static Vector2 GetUpdatedShiftAmount(Vector2 currentVelocity, bool inVacuum)
		{
			// apply shift
			Vector2 shiftTarget = currentVelocity * ShiftFactor;
			// apply vertical emphasis
			float verticalShiftAmount = shiftTarget.y * (!inVacuum ? VerticalShiftEmphasis : -1f);
			// limit vertical amount from getting too big
			verticalShiftAmount = verticalShiftAmount > MaxVerticalShiftAmount ?
				MaxVerticalShiftAmount : verticalShiftAmount < -MaxVerticalShiftAmount ?
					-MaxVerticalShiftAmount : verticalShiftAmount;

			return shiftTarget with { y = verticalShiftAmount };
		}

		private static void UpdateSpeedChangeIntensity(AmountData amountData)
		{
			float currentSpeed = amountData.CurrentVelocity.z;
			float previousSpeed = amountData.PreviousVelocity.z;

			amountData.SpeedChangeIntensity = 
				(
					previousSpeed > currentSpeed ?
					previousSpeed - currentSpeed : currentSpeed - previousSpeed
				) / (previousSpeed < 1f ? 1f : previousSpeed)
				* SpeedChangeIntensityThreshold;
		}

		internal static void UpdateAmount(ShipController ship)
		{
			if (ship.T is null)
				return;

			AmountData amountData = _amountData[ship.playerIndex];
			ShipSim sim = ship.PysSim;
			amountData.CurrentVelocity = ship.T.InverseTransformDirection(ship.RBody.velocity);
			UpdateSpeedChangeIntensity(amountData);

			// update shift amount

			amountData.ShiftTarget =
				GetUpdatedShiftAmount((Vector2) amountData.CurrentVelocity, ship.InVacuum);

			// update shake amount

			float verticalSpeedDiff = amountData.CurrentVelocity.y - amountData.PreviousVelocity.y;

			// nullify shock on maglock landing
			if (ship.OnMaglock)
			{
				amountData.ShakeAmount = 0;
				amountData.ShakeDuration = 0;
			}
			// big landing
			else if (verticalSpeedDiff > MinVerticalSpeedDiff)
			{
				verticalSpeedDiff =
					(verticalSpeedDiff - MinVerticalSpeedDiff) / VerticalSpeedDiffRange;
				verticalSpeedDiff = verticalSpeedDiff < 0 ?
					0 : verticalSpeedDiff > 1f ?
						1f : verticalSpeedDiff;
				amountData.ShakeAmount = verticalSpeedDiff * MaxLandingShakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}
			// wall crash
			else if (sim.touchingWall && amountData.ShakeAmount < WallBounceShakeAmount)
			{
				amountData.ShakeAmount = WallBounceShakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}
			// scraping
			else if (
				(sim.isShipScraping || sim.ScrapingShip) &&
				amountData.ShakeAmount < ScrapingShakeAmount
			)
			{
				amountData.ShakeAmount = ScrapingShakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}
			// speed loss
			else if (
				amountData.SpeedChangeIntensity >= MinSpeedChangeIntensity &&
				amountData.PreviousVelocity.z > amountData.CurrentVelocity.z &&
				amountData.ShakeAmount < WallBounceShakeAmount
			)
			{
				float intensity = amountData.SpeedChangeIntensity - MinSpeedChangeIntensity;
				amountData.ShakeAmount = (intensity >= 1f ? 1f : intensity) * WallBounceShakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}

			if (amountData.ShakeDuration > 0)
			{
				amountData.ShakeDuration -= Time.deltaTime * ShakeDurationDecaySpeed;
			}
			else
			{
				amountData.ShakeAmount = 0;
				amountData.ShakeDuration = 0;
			}

			amountData.PreviousVelocity = amountData.CurrentVelocity;
		}

		internal static IEnumerator Shift(int playerIndex)
		{
			AmountData amountData = _amountData[playerIndex];
			List<Panel> panels = Panels[playerIndex];

			while (true)
			{
				if (amountData.ShakeDuration > 0)
					amountData.ShakeVector = Random.insideUnitCircle.normalized * amountData.ShakeAmount;
				foreach (Panel p in panels)
				{
					p.SetTargetPosition(amountData.ShiftTarget);
					if (p.Position == p.TargetPosition && amountData.ShakeDuration == 0)
						continue;

					// always update position that's only affected by shifting
					p.SetShiftedPosition(Vector2.SmoothDamp(
						p.ShiftedPosition, p.TargetPosition, ref p.CurrentSpeed, DampTime
					));

					// if shake is in effect, force apply the shake to the shifted position,
					// but don't actually change the stored variable.
					if (amountData.ShakeDuration > 0)
					{
						p.SetShakingPosition(amountData.ShakeVector);
						p.SetPositionToShaking();
					}
					else
					{
						p.ResetShakingPosition();
						p.SetPositionToShifted();
					}
				}

				yield return null;
			}
		}

		internal static void HideHud(ShipController ship)
		{
			if (!TargetShips.Contains(ship)) return;
			foreach (Panel p in Panels[ship.playerIndex]) p.Hide();
		}

		internal static void ShowHud(ShipController ship)
		{
			if (!TargetShips.Contains(ship)) return;
			foreach (Panel p in Panels[ship.playerIndex]) p.Show();
		}

		/*internal static string Dump()
		{
			StringBuilder sb = new();
			sb.AppendLine("# Shifter.Dump()");
			sb.Append("## TargetShips: ");
			for (int i = 0; i < MaxPlayer; i++)
			{
				if (i != 0)
					sb.Append(", ");
				sb.Append(TargetShips[i]?.GetType().Name ?? "null");
				sb.Append(" ");
				sb.Append(TargetShips[i]?.ShipName ?? "null");
			}
			sb.AppendLine();

			sb.Append("## AmountData: ");
			for (int i = 0; i < MaxPlayer; i++)
			{
				if (i != 0)
					sb.Append(", ");
				sb.Append(_amountData[i]?.GetType().Name ?? "null");
				sb.Append(" ");
				sb.Append(_amountData[i]?.ShiftTarget.ToString() ?? "null");
			}
			sb.AppendLine();

			sb.AppendLine("## Panels: ");
			for (int i = 0; i < MaxPlayer; i++)
			{
				sb.Append("Player ");
				sb.Append(i);
				sb.Append(": ");
				sb.AppendLine(Panels[i].Count.ToString());
			}

			return sb.ToString();
		}*/
	}

	internal static class PresetColorPicker
	{
		private static readonly float[] StandardH = {
			// for Grey
			0f,
			// Red, Orange, Yellow, Lime, Green, Mint
			0.9944f, 0.0778f, 0.1611f, 0.2444f, 0.3278f, 0.4111f,
			// Cyan, Azure, Blue, Violet, Magenta, Rose
			0.4944f, 0.5778f, 0.6611f, 0.7444f, 0.8278f, 0.9111f
		};
		private static readonly float[][] StandardSV =
		{
			new[] { 0.32f, 0.40f }, // S4   V.40
			new[] { 0.32f, 0.73f }, // S4   V4
			new[] { 0.26f, 0.81f }, // S3   V3
			new[] { 0.18f, 0.86f }, // S2   V2
			new[] { 0.40f, 0.89f }, // S6   V1
			new[] { 0.60f, 0.95f }  // S.60 V.95
		};
		private static readonly float[] TintAlphaList = {
			1f, 0.9f, 0.750f, 0.500f, 0.375f, 0.250f, 0.000f
		};
		internal enum TextAlpha
		{
			Full, NineTenths, ThreeQuarters, Half, ThreeEighths, Quarter, Zero
		}
		internal static ZonePalleteSettings PalleteSettings;

		/*
		 * about `initiatedTintIndex` on ship engine tint index
		 * 
		 * When `OptionValueTint` is set to the ship engine index,
		 * the components still have to call the methods depending on tintIndex,
		 * before they can call the methods that can accept custom colors.
		 * 
		 * `GetTintColor()` and `GetPanelColor()` therefore need to
		 * be able to handle the case where
		 * tintIndex is `OptionValueTintShipEngineIndexForGame`.
		 */

		internal static Color GetTintColor(
			TextAlpha transparencyIndex = TextAlpha.Full,
			int tintIndex = -1,
			int clarity = 3
		)
		{
			int initiatedTintIndex = tintIndex switch
			{
				< 0 when OptionValueTint == OptionValueTintShipEngineIndexForGame => 0,
				< 0 => OptionValueTint,
				_ => tintIndex
			};

			return Color.HSVToRGB(
					StandardH[initiatedTintIndex],
					initiatedTintIndex == 0 ? 0 : StandardSV[clarity][0],
					StandardSV[clarity][1]
				) with
				{
					a = TintAlphaList[(int) transparencyIndex]
				};
		}

		internal static Color GetTintFromColor(
			TextAlpha transparencyIndex = TextAlpha.Full,
			Color? color = null,
			int clarity = 3
		)
		{
			Color.RGBToHSV(color ?? Color.white, out float hue, out float saturation, out _);

			return Color.HSVToRGB(
				hue,
				Mathf.Approximately(saturation, 0f) ? 0 : StandardSV[clarity][0],
				StandardSV[clarity][1]
			) with
			{
				a = TintAlphaList[(int) transparencyIndex]
			};
		}

		internal static Color GetPanelColor(
			int tintIndex = 0
		)
		{
			int initiatedTintIndex = tintIndex switch
			{
				< 0 => 0,
				_ => tintIndex
			};

			return Color.HSVToRGB(
					StandardH[initiatedTintIndex],
					initiatedTintIndex == 0 ? 0 : StandardSV[0][0],
					initiatedTintIndex == 0 ? 0.16f : 0.30f
				) with
				{
					a = TintAlphaList[(int) TextAlpha.ThreeQuarters]
				};
		}

		internal static Color GetPanelColorFromColor(
			Color? color = null
		)
		{
			Color.RGBToHSV(color ?? Color.white, out float hue, out float saturation, out _);
			bool isGreyscale = Mathf.Approximately(saturation, 0f);

			return Color.HSVToRGB(
					hue,
					isGreyscale ? 0 : StandardSV[0][0],
					isGreyscale ? 0.16f : 0.30f
				) with
			{
				a = TintAlphaList[(int) TextAlpha.ThreeQuarters]
			};
		}

		internal static float GetTransparency(TextAlpha transparencyIndex) =>
			TintAlphaList[(int) transparencyIndex];

		internal static void UpdateZonePalleteSettings() =>
			PalleteSettings = ZonePalleteSettings.LoadPaletteSettingsFinal();

		internal static void UpdateZonePalleteSettings(GmUpsurge gamemode) =>
			PalleteSettings = gamemode.LoadZonePallete();

		internal static void FlushZonePalleteSettings() =>
			PalleteSettings = null;

		internal static bool PalleteSettingsLoaded() =>
			PalleteSettings is not null;

		internal static Color GetZoneColor(int zoneNumber)
		{
			if (!OptionZoneTintOverride || PalleteSettings is null)
				return GetTintColor();

			int zoneColorIndex = zoneNumber / 5;
			zoneColorIndex = zoneColorIndex > PalleteSettings.Pallete.Length - 1 ?
				0 : zoneColorIndex;

			return
				PalleteSettings.Pallete[zoneColorIndex].GetColor(EZoneColorTarget.EnvironmentDetail);
		}

		internal static Color GetShipRepresentativeColor(ShipController ship)
		{
			Color.RGBToHSV(
				Color.Lerp(
					ship.Settings.REF_ENGINECOL_BRIGHT, ship.Settings.REF_ENGINECOL, 0.5f
				) with { a = 1f },
				out float h, out float s, out float v
			);
			s *= 1.1f;

			return Color.HSVToRGB(h, s, v);
		}
	}

	internal class BigTimeTextBuilder
	{
		private readonly StringBuilder _sb;
		// 0:00.<size=20> </size><size=150>00</size>
		// Default font size in the component is 300.
		private const string StringAfterMinute =
			":";
		private const string StringAfterSecond =
			".<size=20> </size><size=150>";
		private const string StringAfterHundredth =
			"</size>";
		private static readonly string EmptyTime =
			"-" + StringAfterMinute +
			"--" + StringAfterSecond +
			"--" + StringAfterHundredth;

		private const string StringAfterSecondNoDecimal =
			".";
		private static readonly string EmptyTimeNoDecimal =
			"-" + StringAfterMinute +
			"--" + StringAfterSecondNoDecimal;

		internal string ToString(float value)
		{
			if (value < 0f)
				return EmptyTime;

			_sb.Clear();
			string minutes = IntStrDb.GetNumber(
				Mathf.FloorToInt(value / 60f));
			string seconds = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value) % 60);
			string hundredths = value switch
			{
				< 600f  => IntStrDb.GetNoSingleCharNumber(Mathf.FloorToInt(value * 100f % 100f)),
				< 1200f => IntStrDb.GetNumber            (Mathf.FloorToInt(value *  10f %  10f)),
				_       => ""
			};

			_sb.Append(minutes);
			_sb.Append(StringAfterMinute);
			_sb.Append(seconds);
			_sb.Append(StringAfterSecond);
			_sb.Append(hundredths);
			_sb.Append(StringAfterHundredth);

			return _sb.ToString();
		}

		internal string ToStringNoDecimal(float value)
		{
			if (value < 0f)
				return EmptyTimeNoDecimal;

			_sb.Clear();
			string minutes = IntStrDb.GetNumber(
				Mathf.FloorToInt(value / 60f));
			string seconds = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value) % 60);

			_sb.Append(minutes);
			_sb.Append(StringAfterMinute);
			_sb.Append(seconds);
			_sb.Append(StringAfterSecondNoDecimal);

			return _sb.ToString();
		}

		public BigTimeTextBuilder(StringBuilder sb) => _sb = sb;
	}

	internal class PickupTextBuilder
	{
		private readonly StringBuilder _sb;
		private const string FormatAutopilotDisengaging =
			"Disengaging in x";
		private const string FormatHellstorm =
			"x rear locks";
		private static readonly int AutopilotDisengagingStringLength =
			FormatAutopilotDisengaging.Length;
		private static readonly int HellstormStringLength =
			FormatHellstorm.Length;

		private const string StringAutopilot3 =  ">  3  <" ;
		private const string StringAutopilot2 =  ">> 2 <<" ;
		private const string StringAutopilot1 = ">>> 1 <<<";
		// <size=150><color=#0000>a</color>rear </size>0<size=150> locks</size>
		// Default font size in the component is 300.
		private const string StringHellstormPrefix =
			"<size=150><color=#0000>a</color>rear </size>";
		private const string StringHellstormSuffix =
			"<size=150> locks</size>";
		private const string StringHellstormSuffixSingle =
			"<size=150> lock<color=#0000>s</color></size>";

		internal string ToAutoPilotText(string text)
		{
			if (
				text.Length < AutopilotDisengagingStringLength ||
				!char.IsDigit(text[text.Length - 1])
			)
				return text;

			return text[text.Length - 1] switch
			{
				'3' => StringAutopilot3,
				'2' => StringAutopilot2,
				'1' => StringAutopilot1,
				_ => text
			};
		}

		internal string ToHellstormText(string text)
		{
			/*
			 * There is also a method called `IsDigit` which only returns
			 * true for radix-10 digits. I don't know if I want to use
			 * that instead.
			 */
			if (
				text.Length < HellstormStringLength ||
				!char.IsNumber(text[0])
			)
				return text;

			_sb.Clear();
			_sb.Append(StringHellstormPrefix);
			_sb.Append(text[0]);
			_sb.Append(text[0] != '1' ?
				StringHellstormSuffix : StringHellstormSuffixSingle);

			return _sb.ToString();
		}

		public PickupTextBuilder(StringBuilder sb) => _sb = sb;
	}

	internal static class PickupIcon
	{
		internal static readonly Dictionary<string, Sprite> Get = new()
		{
			{ "rockets", CustomHudRegistry.GetWeaponSprite("rockets", false) },
			{ "missile", CustomHudRegistry.GetWeaponSprite("missile", false) },
			{ "mines", CustomHudRegistry.GetWeaponSprite("mines", false) },
			{ "plasma", CustomHudRegistry.GetWeaponSprite("plasma", false) },
			{ "energywall", CustomHudRegistry.GetWeaponSprite("energywall", false) },
			{ "cannon", CustomHudRegistry.GetWeaponSprite("cannon", false) },
			{ "shield", CustomHudRegistry.GetWeaponSprite("shield", false) },
			{ "autopilot", CustomHudRegistry.GetWeaponSprite("autopilot", false) },
			{ "emergencypack", CustomHudRegistry.GetWeaponSprite("emergencypack", false) },
			{ "tremor", CustomHudRegistry.GetWeaponSprite("tremor", false) },
			{ "turbo", CustomHudRegistry.GetWeaponSprite("turbo", false) },
			{ "hunter", CustomHudRegistry.GetWeaponSprite("hunter", false) },
			{ "hellstorm", CustomHudRegistry.GetWeaponSprite("hellstorm", false) }
		};
	}

	internal static class SectionManager
	{
		internal static int GetTotalSectionCount()
		{
			/*
			 * So the issue is that `sections.Count` has more sections
			 * than what you pass in a lap.
			 * I wonder if that's because the sections on the pit lane were added.
			 */
			Section section = TrackManager.Instance.data.sections[TrackManager.Instance.data.sectionStart];
			Section next = section.next;
			int count = 1;
			while (count < TrackManager.Instance.data.sections.Count && (bool) (UnityEngine.Object) next.next)
			{
				next = next.next;
				++count;
				if (next == section)
					break;
			}

			return count;
		}

		internal static int GetPassingSectionIndex(ShipController ship, int currentLap, int totalTrackSections = -1)
		{
			if (totalTrackSections == -1)
				totalTrackSections = GetTotalSectionCount();
			int lapOffset = (currentLap - 1) * totalTrackSections;
			/*
			 * A lap starts somewhere in the first section. Section index is base-0.
			 * If I just get the index and add the lap offsets,
			 * the result goes down by about a lap for last two sections in a lap.
			 * So I shift the index forward by one to make the section a lap starts the 0th.
			 *
			 * But even with this adjustment,
			 * there is still a sub-section length at the start of the new 0th one,
			 * where the section index is reset to 0 but the lap counter is not yet updated.
			 *
			 * Example of a track with 100 sections, at the end of a lap 3:
			 * -----startlane-----      ┌Lap Boundary
			 *  94  95  96  97  99   0   1   2   3 <- raw
			 *  93  94  95  96  98  99   0   1   2 <- adjusted
			 *   current lap:    3   3 3|4   4   4
			 * total section:  298 299 | | 301 302
			 *                      200┘ └300
			 *
			 * I will skip updating any corresponding hud components at this section,
			 * but that will be applied on each components.
			 */
			int passingSection = ship.CurrentSection.index - 1;
			if (passingSection < 0)
				passingSection += totalTrackSections;
			passingSection += lapOffset;

			return passingSection;
		}

		internal static float GetLapCompletionRate(ShipController ship, int totalTrackSections = -1)
		{
			if (totalTrackSections == -1)
				totalTrackSections = GetTotalSectionCount();

			int passingSection = ship.CurrentSection.index - 1;
			if (passingSection < 0)
				passingSection += totalTrackSections;

			return (float) passingSection / totalTrackSections;
		}

		internal static float GetRaceCompletionRate(ShipController ship, int currentLap, int totalTrackSections = -1)
		{
			if (currentLap == 0)
				return 0f;
			if (totalTrackSections == -1)
				totalTrackSections = GetTotalSectionCount();

			int totalRaceSections = totalTrackSections * Race.MaxLaps;
			int passingSection = GetPassingSectionIndex(ship, currentLap, totalTrackSections);

			return (float) passingSection / totalRaceSections;
		}
	}

	/// <summary>
	/// Makes a panel that has following RectTransform hierarchy defined on Unity:
	/// - Panel (has Image. a background area)
	///		- Label (has Text. stays around the background)
	///		- Value (has Text. the main dynamic element)
	///		- Gauge Background (has Image. an overlay for the gauge)
	///			- Gauge (has Image, set to maximum width. the gauge sprite)
	/// The string arguments in <c>Find()</c> are the names I set to components on Unity.
	/// </summary>
	internal class BasicPanel
	{
		internal readonly RectTransform Base;
		internal readonly Text Label;
		internal readonly Text Value;
		protected readonly RectTransform GaugeBackground;
		protected readonly Image GaugeBackgroundImage;
		protected readonly RectTransform Gauge;
		protected readonly Image GaugeImage;
		internal Color GaugeColor;
		internal readonly Vector2 MaxSize;
		internal Vector2 CurrentSize;

		public BasicPanel(RectTransform panelElement)
		{
			Base = panelElement;
			Label = panelElement.Find("Label").GetComponent<Text>();
			Value = panelElement.Find("Value").GetComponent<Text>();
			GaugeBackground = panelElement.Find("GaugeBackground").GetComponent<RectTransform>();
			GaugeBackgroundImage = GaugeBackground.GetComponent<Image>();
			Gauge = (RectTransform)GaugeBackground.Find("Gauge");
			GaugeImage = Gauge.GetComponent<Image>();
			MaxSize = Gauge.sizeDelta;
			GaugeColor = GetTintColor();

			UpdateColor();
			Fill(0f);
		}

		private void UpdateColor()
		{
			Label.color = GaugeColor;
			Value.color = GaugeColor;
			GaugeImage.color = GaugeColor;
			GaugeBackgroundImage.color = GaugeColor with { a = GetTransparency(TextAlpha.ThreeEighths) };
		}

		// Identical to the method with no parameters,
		// but you don't want to make that one virtual since
		// it will be used in the constructor.
		internal virtual void UpdateColor(Color color)
		{
			GaugeColor = color;
			UpdateColor();
		}

		public virtual void ChangeDataPartColor(Color color)
		{
			Value.color = color;
			GaugeImage.color = color;
		}

		public void Fill(float amount)
		{
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
			CurrentSize.x = amount * MaxSize.x;
			// This is only useful because the bar border is
			// a 45deg slanted straight line.
			CurrentSize.y = CurrentSize.x >= MaxSize.y ?
				MaxSize.y : CurrentSize.x;
			Gauge.sizeDelta = CurrentSize;
		}
	}

	/// <summary>
	/// Inheritance of <c>BasicPanel</c>.
	/// This adds a set for the second gauge for acceleration.
	///
	/// The component hierarchy on Unity has one more child under GaugeBackground.
	///	- Gauge Background (has Image. an overlay for the gauge)
	///		- AccelGauge (has Image, set to maximum width. the gauge sprite)
	///	  - Gauge (has Image, set to maximum width. the gauge sprite)
	/// </summary>
	internal class SpeedPanel : BasicPanel
	{
		protected readonly RectTransform AccelGauge;
		protected readonly Image AccelGaugeImage;
		internal Vector2 CurrentAccelSize;

		public SpeedPanel(RectTransform panelElement) : base(panelElement)
		{
			AccelGauge = (RectTransform)GaugeBackground.Find("AccelGauge");
			AccelGaugeImage = AccelGauge.GetComponent<Image>();

			UpdateAccelColor();
			FillAccel(0f);
		}

		private void UpdateAccelColor() =>
			AccelGaugeImage.color = GaugeColor with { a = GetTransparency(TextAlpha.ThreeEighths) };

		internal override void UpdateColor(Color color)
		{
			base.UpdateColor(color);
			UpdateAccelColor();
		}

		public void FillAccel(float amount)
		{
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
			CurrentAccelSize.x = amount * MaxSize.x;
			CurrentAccelSize.y = CurrentAccelSize.x >= MaxSize.y ?
				MaxSize.y : CurrentAccelSize.x;
			AccelGauge.sizeDelta = CurrentAccelSize;
		}
	}

	internal class FractionPanel : BasicPanel
	{
		internal readonly Text MaxValue;
		internal readonly Text Separator;

		public FractionPanel(RectTransform panelElement) : base(panelElement)
		{
			MaxValue = panelElement.Find("MaxValue").GetComponent<Text>();
			Separator = Base.Find("Separator").GetComponent<Text>();

			UpdateFractionPartColor();
		}

		private void UpdateFractionPartColor()
		{
			MaxValue.color = GaugeColor;
			Separator.color = GaugeColor;
		}

		internal override void UpdateColor(Color color)
		{
			base.UpdateColor(color);
			UpdateFractionPartColor();
		}

		public override void ChangeDataPartColor(Color color)
		{
			base.ChangeDataPartColor(color);
			MaxValue.color = color;
			Separator.color = color;
		}
	}

	internal class DoubleGaugePanel : BasicPanel
	{
		protected readonly RectTransform RightGauge;
		protected readonly Image RightGaugeImage;
		protected internal readonly Text SmallValue;
		protected readonly bool UsingSmallValue;

		internal enum StartingPoint
		{
			Edge, Center
		}

		public DoubleGaugePanel(RectTransform panelElement, bool usingSmallValue = false) : base(panelElement)
		{
			RightGauge = (RectTransform) GaugeBackground.Find("RightGauge");
			RightGaugeImage = RightGauge.GetComponent<Image>();
			// the bar borders are perpendicular straight lines
			CurrentSize.y = MaxSize.y;
			UsingSmallValue = usingSmallValue;
			if (usingSmallValue)
				SmallValue = panelElement.Find("SmallValue").GetComponent<Text>();

			UpdateAdditionalPartsColor();
			FillBoth(0f);
		}

		private void UpdateAdditionalPartsColor()
		{
			RightGaugeImage.color = GaugeColor;
			if (UsingSmallValue) SmallValue.color = GaugeColor;
		}

		private void ChangeAdditionalPartsColor(Color color)
		{
			RightGaugeImage.color = color;
			if (UsingSmallValue) SmallValue.color = color;
		}

		internal override void UpdateColor(Color color)
		{
			base.UpdateColor(color);
			UpdateAdditionalPartsColor();
		}

		public override void ChangeDataPartColor(Color color)
		{
			base.ChangeDataPartColor(color);
			ChangeAdditionalPartsColor(color);
		}

		public void FillBoth(float amount)
		{
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
			CurrentSize.x = amount * MaxSize.x;
			Gauge.sizeDelta = CurrentSize;
			RightGauge.sizeDelta = CurrentSize;
		}

		public virtual void SetFillStartingSide(StartingPoint sp)
		{
			switch (sp)
			{
				case StartingPoint.Edge:
					Gauge.pivot = new Vector2(0, 1);
					RightGauge.pivot = new Vector2(1, 1);
					Gauge.localPosition = Vector3.zero;
					RightGauge.localPosition = Vector3.zero;
					break;
				case StartingPoint.Center:
					Gauge.pivot = new Vector2(1, 1);
					RightGauge.pivot = new Vector2(0, 1);
					Gauge.localPosition = Vector3.right * Gauge.sizeDelta.x;
					RightGauge.localPosition = Vector3.left * RightGauge.sizeDelta.x;
					break;
			}
		}
	}

	internal class LayeredDoubleGaugePanel : DoubleGaugePanel
	{
		protected readonly RectTransform SecondGauge;
		protected readonly Image SecondGaugeImage;
		protected readonly RectTransform SecondRightGauge;
		protected readonly Image SecondRightGaugeImage;
		protected readonly RectTransform SmallGauge;
		protected readonly Image SmallGaugeImage;
		protected readonly RectTransform SmallRightGauge;
		protected readonly Image SmallRightGaugeImage;
		protected readonly bool UsingSmallGauges;
		internal Color SecondGaugeColor;
		internal Vector2 CurrentSecondSize;
		internal Color SmallGaugeColor;
		internal readonly Vector2 SmallMaxSize;
		internal Vector2 CurrentSmallSize;

		public LayeredDoubleGaugePanel
			(RectTransform panelElement, bool useSmallGauges = false) : base(panelElement)
		{
			UsingSmallGauges = useSmallGauges;

			SecondGauge = (RectTransform) GaugeBackground.Find("SecondGauge");
			SecondGaugeImage = SecondGauge.GetComponent<Image>();
			SecondRightGauge = (RectTransform) GaugeBackground.Find("SecondRightGauge");
			SecondRightGaugeImage = SecondRightGauge.GetComponent<Image>();
			SecondGauge.gameObject.SetActive(true);
			SecondRightGauge.gameObject.SetActive(true);
			CurrentSecondSize.y = MaxSize.y;

			SecondGaugeColor = GetTintColor(TextAlpha.ThreeEighths);
			SmallGaugeColor = GetTintColor(clarity: 1);

			UpdateSecondGaugesColor();
			FillSecondGauges(0f);

			if (!UsingSmallGauges) return;

			SmallGauge = (RectTransform) GaugeBackground.Find("SmallGauge");
			SmallGaugeImage = SmallGauge.GetComponent<Image>();
			SmallRightGauge = (RectTransform) GaugeBackground.Find("SmallRightGauge");
			SmallRightGaugeImage = SmallRightGauge.GetComponent<Image>();
			SmallGauge.gameObject.SetActive(true);
			SmallRightGauge.gameObject.SetActive(true);
			SmallMaxSize = SmallGauge.sizeDelta;
			CurrentSmallSize.y = SmallMaxSize.y;

			UpdateSmallGaugesColor();
			FillSmallGauges(0f);
		}

		private void UpdateSecondGaugesColor()
		{
			SecondGaugeColor = GaugeColor with { a = GetTransparency(TextAlpha.ThreeEighths) };
			SecondGaugeImage.color = SecondGaugeColor;
			SecondRightGaugeImage.color = SecondGaugeColor;
		}

		internal void UpdateSmallGaugesColor()
		{
			SmallGaugeColor = GetTintFromColor(color: GaugeColor, clarity: 1);
			SmallGaugeImage.color = SmallGaugeColor;
			SmallRightGaugeImage.color = SmallGaugeColor;
		}

		internal void ChangeSmallGaugesColor(Color color)
		{
			SmallGaugeImage.color = color;
			SmallRightGaugeImage.color = color;
		}

		internal override void UpdateColor(Color color)
		{
			base.UpdateColor(color);
			UpdateSecondGaugesColor();
		}

		public override void ChangeDataPartColor(Color color)
		{
			base.ChangeDataPartColor(color);
			color.a = GetTransparency(TextAlpha.ThreeEighths);
			SecondGaugeImage.color = color;
			SecondRightGaugeImage.color = color;
		}

		public void FillSecondGauges(float amount)
		{
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
			CurrentSecondSize.x = amount * MaxSize.x;
			SecondGauge.sizeDelta = CurrentSecondSize;
			SecondRightGauge.sizeDelta = CurrentSecondSize;
		}

		public void FillSmallGauges(float amount)
		{
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
			CurrentSmallSize.x = amount * SmallMaxSize.x;
			CurrentSmallSize.y = CurrentSmallSize.x >= SmallMaxSize.y ?
				SmallMaxSize.y : CurrentSmallSize.x;
			SmallGauge.sizeDelta = CurrentSmallSize;
			SmallRightGauge.sizeDelta = CurrentSmallSize;
		}

		public override void SetFillStartingSide(StartingPoint sp)
		{
			base.SetFillStartingSide(sp);
			switch (sp)
			{
				case StartingPoint.Edge:
					SecondGauge.pivot = new Vector2(0, 1);
					SecondRightGauge.pivot = new Vector2(1, 1);
					SecondGauge.localPosition = Vector3.zero;
					SecondRightGauge.localPosition = Vector3.zero;
					if (UsingSmallGauges)
					{
						SmallGauge.pivot = new Vector2(0, 1);
						SmallRightGauge.pivot = new Vector2(1, 1);
						SmallGauge.localPosition = Vector3.zero;
						SmallRightGauge.localPosition = Vector3.zero;
					}
					break;
				case StartingPoint.Center:
					SecondGauge.pivot = new Vector2(1, 1);
					SecondRightGauge.pivot = new Vector2(0, 1);
					SecondGauge.localPosition = Vector3.right * SecondGauge.sizeDelta.x;
					SecondRightGauge.localPosition = Vector3.left * SecondRightGauge.sizeDelta.x;
					if (UsingSmallGauges)
					{
						SmallGauge.pivot = new Vector2(1, 1);
						SmallRightGauge.pivot = new Vector2(0, 1);
						SmallGauge.localPosition =
							Vector3.right * SmallGauge.sizeDelta.x * SmallGauge.localScale.x;
						// localScale.x here is negative, so I am using Vector3.right instead of left.
						SmallRightGauge.localPosition =
							Vector3.right * SmallRightGauge.sizeDelta.x * SmallRightGauge.localScale.x;
					}
					break;
			}
		}
	}

	internal class PickupPanel
	{
		private readonly CanvasGroup _panelGroup;
		private readonly Image _bracketsImage;
		private readonly Image _iconImage;
		private readonly Text _info;
		private readonly PickupTextBuilder _pickupTextBuilder = new(new StringBuilder());
		private string _weaponId;
		private const float AnimationLength = 0.1f;
		private const float AnimationSpeed = 1 / AnimationLength;

		public Coroutine CurrentTransition;

		private static Color _offensiveColor;
		private static Color _defensiveColor;
		private Color _hudDefaultColor;

		public IEnumerator ColorFade(
			bool enableIcon, bool offensive = false, bool usePickupColor = true
		)
		{
			Color startColor = enableIcon ?
				Color.clear : _iconImage.color;
			Color endColor = enableIcon ?
				usePickupColor ?
					offensive ? _offensiveColor : _defensiveColor :
					_hudDefaultColor :
				Color.clear;
			Color transitionColor;
			float t = enableIcon ? 0 : AnimationLength;

			if (enableIcon && !_iconImage.enabled)
			{
				_bracketsImage.enabled = true;
				_iconImage.enabled = true;
				if (_info is not null)
					_info.enabled = true;
			}

			if (enableIcon)
			{
				_panelGroup.alpha = 1f;
				_bracketsImage.color = startColor;
				_iconImage.color = startColor;
				while (t <= AnimationLength)
				{
					t += Time.deltaTime;
					transitionColor = Color.Lerp(startColor, _hudDefaultColor, t * AnimationSpeed);
					_bracketsImage.color = transitionColor;
					_iconImage.color = transitionColor;
					yield return null;
				}
			}
			/*
			 * when enabling, only change to pickup color when usePickupColor is on
			 * when disabling, usePickupColor doesn't matter
			 */
			if (!enableIcon || usePickupColor)
			{
				while (t <= AnimationLength * 2)
				{
					t += Time.deltaTime;
					float secondFadeProgress = (t - AnimationLength) * AnimationSpeed;
					if (!enableIcon)
					{
						_panelGroup.alpha = 1f - secondFadeProgress;
					}
					else
					{
						transitionColor = Color.Lerp(_iconImage.color, endColor, secondFadeProgress);
						_bracketsImage.color = transitionColor;
						_iconImage.color = transitionColor;
					}
					yield return null;
				}
			}
			_bracketsImage.color = endColor;
			_iconImage.color = endColor;

			if (!enableIcon && _iconImage.enabled)
			{
				_panelGroup.alpha = 0f;
				_bracketsImage.enabled = false;
				_iconImage.enabled = false;
				if (_info is not null)
					_info.enabled = false;
			}

			CurrentTransition = null;
		}

		public void UpdateSprite(string weaponId)
		{
			_iconImage.sprite = PickupIcon.Get[weaponId];
			_weaponId = weaponId;
		}

		public void UpdateText(string text)
		{
			if (_info is null || text is null)
				return;

			_info.text = _weaponId switch
			{
				"autopilot" => _pickupTextBuilder.ToAutoPilotText(text),
				"hellstorm" => _pickupTextBuilder.ToHellstormText(text),
				_ => text
			};
		}

		public bool IconEnabled => _iconImage.enabled;

		public void ShowInstant(bool offensive = false)
		{
			_panelGroup.alpha = 1f;
			_bracketsImage.enabled = true;
			_iconImage.enabled = true;
			_bracketsImage.color = offensive ?
				_offensiveColor : _defensiveColor;
			_iconImage.color = offensive ?
				_offensiveColor : _defensiveColor;
		}

		public void ShowWarning()
		{
			_panelGroup.alpha = 1f;
			_bracketsImage.enabled = true;
			_iconImage.enabled = true;
			_bracketsImage.color = _offensiveColor;
			_iconImage.color = _offensiveColor;
		}

		private void Initiate()
		{
			_panelGroup.alpha = 0f;
			_bracketsImage.enabled = false;
			_iconImage.enabled = false;
			if (_info is not null)
			{
				_info.enabled = false;
				_info.text = "";
			}

			_offensiveColor = GetTintColor(tintIndex: 2, clarity: 2);
			_defensiveColor = GetTintColor(tintIndex: 8, clarity: 2);

			UpdateColor();
		}

		public void UpdateColor()
		{
			_hudDefaultColor = GetTintColor();
			if (_info is not null)
				_info.color = GetTintColor();
		}

		public void UpdateColor(Color color)
		{
			_hudDefaultColor = GetTintFromColor(color: color);
			if (_info is not null)
				_info.color = GetTintFromColor(color: color);
		}

		public PickupPanel(RectTransform basePanel)
		{
			_panelGroup = basePanel.GetComponent<CanvasGroup>();
			_bracketsImage = basePanel.Find("Brackets").GetComponent<Image>();
			_iconImage = basePanel.Find("Icon").GetComponent<Image>();

			Initiate();
		}

		public PickupPanel(RectTransform basePanel, Text infoText)
		{
			_panelGroup = basePanel.GetComponent<CanvasGroup>();
			_bracketsImage = basePanel.Find("Brackets").GetComponent<Image>();
			_iconImage = basePanel.Find("Icon").GetComponent<Image>();
			_info = infoText;

			Initiate();
		}
	}

	internal class Playerboard
	{
		private enum ValueType
		{
			Position, Score, IntScore, Energy
		}
		private ValueType _valueType;
		private void SetValueType(string name)
		{
			_valueType = name switch
			{
				"Eliminator" => ValueType.Score,
				"Upsurge" => ValueType.IntScore,
				"Rush Hour" => ValueType.Energy,
				_ => ValueType.Position
			};
		}

		private const float OneLetterWidth = 20f;
		private const float OneDotWidth = 4f;
		internal readonly RectTransform Base;
		protected readonly RectTransform TargetRow;
		protected readonly RectTransform EntrySlotTemplate;
		protected readonly RectTransform TemplateGaugeBackground;
		protected readonly RectTransform TemplateGauge;
		private List<EntrySlot> _visibleList;
		private List<RawValuePair> _rawValueList;

		private class EntrySlot
		{
			private readonly GameObject _templateInstance;
			private readonly CanvasGroup _baseCanvasGroup;
			private readonly Text _name;
			private readonly Text _value;
			private readonly RectTransform _gauge;
			private readonly Image _gaugeImage;
			internal int RefId;
			private readonly Vector2 _maxSize;
			private Vector2 _currentSize;
			private Color _slotColor;

			public EntrySlot(RectTransform template)
			{
				_templateInstance = template.gameObject;
				_templateInstance.SetActive(true);
				_baseCanvasGroup = template.GetComponent<CanvasGroup>();
				_name = template.Find("Name").GetComponent<Text>();
				_value = template.Find("Plate").Find("Value").GetComponent<Text>();
				_gauge = (RectTransform)template.Find("GaugeBackground").Find("Gauge");
				_gaugeImage = _gauge.GetComponent<Image>();
				_maxSize = _gauge.sizeDelta;

				UpdateColor();
				SetName("");
				SetDisplayValue(ValueType.Position, 0);
				FillByPercentage(100f);
			}

			public void Hide() => _templateInstance.SetActive(false);

			public void UpdateColor()
			{
				_slotColor = GetTintColor(TextAlpha.ThreeQuarters);
				_name.color = _slotColor;
				_gaugeImage.color = _slotColor;
				_value.color = _slotColor with { a = GetTransparency(TextAlpha.NineTenths) };
			}
			public void UpdateColor(Color color)
			{
				_slotColor = color with { a = GetTransparency(TextAlpha.ThreeQuarters) };
				_name.color = _slotColor;
				_gaugeImage.color = _slotColor;
				_value.color = _slotColor with { a = GetTransparency(TextAlpha.NineTenths) };
			}

			public void ChangeOverallAlpha(TextAlpha transparencyIndex) =>
				_baseCanvasGroup.alpha = GetTransparency(transparencyIndex);

			public void SetRefId(int id) => RefId = id;

			public void SetName(string name) => _name.text = name;

			public void SetDisplayValue(ValueType valueType, float value)
			{
				switch (valueType)
				{
					case ValueType.Energy:
						int valueInt = Mathf.CeilToInt(value);
						_value.text = IntStrDb.GetNumber(
							valueInt < 0 ? 0 : valueInt > 100 ? 100 : valueInt);
						break;
					case ValueType.Score:
						_value.text = value switch
						{
							< 2000f => (Math.Round(value * 100f) / 100.0).ToString("F2", CultureInfo.InvariantCulture),
							_       => (Math.Round(value *  10f) /  10.0).ToString("F1", CultureInfo.InvariantCulture)
						};
						break;
					case ValueType.IntScore:
					case ValueType.Position:
					default:
						_value.text = IntStrDb.GetNumber(Mathf.FloorToInt(value));
						break;
				}
			}

			public void SetDisplayValue() =>
				_value.text = "-";

			public void FillByPercentage(float value)
			{
				value = value < 0f ? 0f : value > 100f ? 100f : value;
				_currentSize.x = ( value / 100f ) * _maxSize.x;
				_currentSize.y = _currentSize.x >= _maxSize.y ?
					_maxSize.y : _currentSize.x;
				_gauge.sizeDelta = _currentSize;
			}
		}

		private class RawValuePair
		{
			public readonly int Id;
			public readonly string Name;
			public float Value;

			public RawValuePair(int id, string name, float value)
			{
				Id = id;
				Name = name;
				Value = value;
			}
		}

		public Playerboard(RectTransform panelElement, string gamemodeName)
		{
			SetValueType(gamemodeName);

			Base = panelElement;
			TargetRow = panelElement.Find("TargetRow").GetComponent<RectTransform>();
			EntrySlotTemplate = panelElement.Find("EntrySlot").GetComponent<RectTransform>();
			TemplateGaugeBackground =
				EntrySlotTemplate.Find("GaugeBackground").GetComponent<RectTransform>();
			TemplateGauge = (RectTransform) TemplateGaugeBackground.Find("Gauge");
			EntrySlotTemplate.gameObject.SetActive(false);
		}

		public void ScaleSlot(float rate) => EntrySlotTemplate.sizeDelta =
				Vector2.Scale(EntrySlotTemplate.sizeDelta, new Vector2(1f, rate));

		public void InitiateLayout(float targetScore)
		{
			// Disable components that are irrelevant to the game mode.
			if (targetScore == 0f)
			{
				TargetRow.gameObject
					.SetActive(false);
				EntrySlotTemplate.localPosition = Vector3.zero;
			}
			TemplateGaugeBackground.gameObject
				.SetActive(_valueType == ValueType.Energy);

			/*
			 * The reason the thresholds are staying at 2*10^n instead of 10^n is
			 * just because the font has a very narrow width for number 1.
			 * Energy type is included because it starts with 100, which is bigger than 2*10^1.
			 */
			float additionalWidth =
				(_valueType == ValueType.Energy ? 1f :
					targetScore < 20f ? 0f : targetScore < 200f ? 1f : 2f
				) * OneLetterWidth
				+ (_valueType == ValueType.Score ? OneDotWidth + OneLetterWidth * 2f : 0f);

			if (additionalWidth > 0f)
			{
				Vector2 widthAdjustmentVector2 = new Vector2(additionalWidth, 0);
				Vector3 widthAdjustmentVector3 = new Vector3(additionalWidth, 0, 0);

				TargetRow.Find("Target").GetComponent<RectTransform>()
					.localPosition -= widthAdjustmentVector3;
				TargetRow.Find("TargetValue").GetComponent<RectTransform>()
					.sizeDelta += widthAdjustmentVector2;

				/*
				 * I don't know why I can't do this with
				 * either `localScale` or `lossyScale` of
				 * the directly related component `_templateGauge`.
				 *
				 * Since I know gauge scale is 1, and it's a child of the bg,
				 * I'll just use the bg scale that's not 1.
				 */
				Vector2 gaugeAdjustmentVector =
					widthAdjustmentVector2 / TemplateGaugeBackground.localScale;
				RectTransform name = EntrySlotTemplate.Find("Name").GetComponent<RectTransform>();

				TemplateGaugeBackground.sizeDelta -= gaugeAdjustmentVector;
				TemplateGauge.sizeDelta -= gaugeAdjustmentVector;
				name.localPosition -= widthAdjustmentVector3;
				name.sizeDelta -= widthAdjustmentVector2 / name.localScale;
				EntrySlotTemplate.Find("Plate").GetComponent<RectTransform>()
					.sizeDelta += widthAdjustmentVector2;
			}

			TargetRow.Find("TargetValue").Find("Value").GetComponent<Text>()
				.text = _valueType == ValueType.Score ?
				IntStrDb.GetNumber((int) targetScore) + ".00" :
				IntStrDb.GetNumber((int) targetScore);
		}

		public void InitiateSlots(List<ShipController> loadedShips)
		{
			_visibleList = new List<EntrySlot>(loadedShips.Count);
			_rawValueList = new List<RawValuePair>(loadedShips.Count);
			for (int i = 0; i < loadedShips.Count; i++)
			{
				RectTransform slot =
					Instantiate(EntrySlotTemplate.gameObject).GetComponent<RectTransform>();
				slot.SetParent(EntrySlotTemplate.parent);
				slot.localScale = EntrySlotTemplate.localScale;
				slot.anchoredPosition = EntrySlotTemplate.anchoredPosition;

				slot.localPosition += Vector3.down * slot.sizeDelta.y * i;

				ShipController loadedShip = loadedShips[i];
				_visibleList.Add(new EntrySlot(slot));
				_visibleList[i].SetRefId(loadedShip.ShipId);
				_visibleList[i].SetName(loadedShip.ShipName);

				_rawValueList.Add(new RawValuePair(
					loadedShip.ShipId, loadedShip.ShipName, float.NegativeInfinity));
			}
		}

		/// <summary>
		///		Locate the last `EntrySlot` in the list and hide its game object.
		/// </summary>
		/// <param name="ship">
		///		The ship that just exploded. Keeps data even in gamemodes that don't allow respawn.
		/// </param>
		public void HideLastSlot(ShipController ship)
		{
			if (ship is null)
				return;
			_visibleList[_visibleList.Count - 1].Hide();
		}

		public void UpdateColor(Color color)
		{
			foreach (EntrySlot slot in _visibleList)
				slot.UpdateColor(color);
		}

		public IEnumerator Update(List<ShipController> loadedShips)
		{
			while (true)
			{
				UpdateData(loadedShips);
				UpdateRefIdByOrder();
				UpdateSlots();

				yield return new WaitForSeconds(Position.UpdateTime);
			}
		}

		/// <summary>
		/// Update stored data in <c>_rawValueList</c> with corresponding ones in the loaded ships.
		/// </summary>
		public void UpdateData(List<ShipController> loadedShips)
		{
			foreach (RawValuePair rawValuePair in _rawValueList)
			{
				ShipController ship = loadedShips[rawValuePair.Id];
				switch (_valueType)
				{
					case ValueType.Energy:
						rawValuePair.Value = ship.ShieldIntegrity;
						break;
					case ValueType.Score:
					case ValueType.IntScore:
						rawValuePair.Value = ship.Score;
						break;
					case ValueType.Position:
					default:
						rawValuePair.Value = ship.ShieldIntegrity < 0f ?
							_rawValueList.Count + 1 : ship.CurrentPlace;
						break;
				}
			}
		}

		/// <summary>
		/// Get a sorted copy of <c>_rawValueList</c>, and update reference ship id in <c>_visibleList</c>. Next time the slots update, they will fetch data from <c>_rawValueList</c> with the new ids.
		/// </summary>
		public void UpdateRefIdByOrder()
		{
			List<RawValuePair> orderedValueList;
			switch (_valueType)
			{
				case ValueType.Energy:
				case ValueType.Score:
				case ValueType.IntScore:
					orderedValueList = _rawValueList.OrderByDescending(p => p.Value).ToList();
					break;
				case ValueType.Position:
				default:
					orderedValueList = _rawValueList.OrderBy(p => p.Value).ToList();
					break;
			}

			for (int i = 0; i < _visibleList.Count; i++)
			{
				EntrySlot slot = _visibleList[i];
				slot.SetRefId(orderedValueList[i].Id);
			}
		}

		/// <summary>
		/// Update the slots of <c>_visibleList</c> with data found with the given reference id in <c>_rawValueList</c>.
		/// </summary>
		public void UpdateSlots()
		{
			switch (_valueType)
			{
				case ValueType.Energy:
					UpdateSlotsEnergy();
					break;
				case ValueType.Score:
				case ValueType.IntScore:
					UpdateSlotsScore();
					break;
				case ValueType.Position:
				default:
					UpdateSlotsPosition();
					break;
			}
		}

		private void UpdateSlotsScore()
		{
			foreach (EntrySlot slot in _visibleList)
			{
				int refId = slot.RefId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				slot.SetDisplayValue(_valueType, rawValuePair.Value);
			}
		}

		private void UpdateSlotsEnergy()
		{
			foreach (EntrySlot slot in _visibleList)
			{
				int refId = slot.RefId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				slot.SetDisplayValue(_valueType, rawValuePair.Value);
				slot.FillByPercentage(rawValuePair.Value);
				slot.ChangeOverallAlpha(
					rawValuePair.Value < 0f ?
						TextAlpha.Quarter : TextAlpha.Full
				);
			}
		}

		private void UpdateSlotsPosition()
		{
			foreach (EntrySlot slot in _visibleList)
			{
				int refId = slot.RefId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				if (rawValuePair.Value <= _rawValueList.Count)
				{
					slot.SetDisplayValue(_valueType, rawValuePair.Value);
					slot.ChangeOverallAlpha(TextAlpha.Full);
				}
				else
				{
					slot.SetDisplayValue();
					slot.ChangeOverallAlpha(TextAlpha.Zero);
				}
			}
		}
	}
}