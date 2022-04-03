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
	public class Speedometer : ScriptableHud
	{
		private Image Panel;
		private RectTransform PanelTransform;
		private Text Value;
		private Image GaugeBackground;
		private RectTransform AccelGauge;
		private float AccelMaxWidth;
		private Vector2 _currentAccelSize;
		private RectTransform SpeedGauge;
		private float SpeedMaxWidth;
		private Vector2 _currentSpeedSize;
		private float _computedValue;

		private float _currentSpeed;
		private float _previousSpeed;
		private Color _defaultColor;
		private Color _highlightColor;
		private float _colorTransitionTimer = 0.13f;

		public override void Start()
		{
			base.Start();

			Panel = CustomComponents.GetById<Image>("Panel");
			PanelTransform = CustomComponents.GetById<RectTransform>("Panel");
			Value = PanelTransform.GetChild(1).GetComponent<Text>();
			GaugeBackground = CustomComponents.GetById<Image>("GaugeBackground");
			AccelGauge = CustomComponents.GetById<RectTransform>("AccelGauge");
			SpeedGauge = CustomComponents.GetById<RectTransform>("SpeedGauge");

			// Gauges are stored in their maximum size, store the max width here.
			AccelMaxWidth = AccelGauge.sizeDelta.x;
			SpeedMaxWidth = SpeedGauge.sizeDelta.x;

			// Initiate the gauges to have 0 width.
			_currentAccelSize.x = 0;
			_currentAccelSize.y = AccelGauge.sizeDelta.y;
			_currentSpeedSize.x = 0;
			_currentSpeedSize.y = SpeedGauge.sizeDelta.y;
			AccelGauge.sizeDelta = _currentAccelSize;
			SpeedGauge.sizeDelta = _currentSpeedSize;

			// Colorizing
			Value.color = GetTintColor();
			GaugeBackground.color = GetTintColor(TextAlpha.ThreeEighths);
			AccelGauge.GetComponent<Image>().color = GetTintColor(TextAlpha.Quarter);
			SpeedGauge.GetComponent<Image>().color = GetTintColor();
			_defaultColor = GetTintColor();
			_highlightColor = new Color32(0x9f, 0x65, 0x67, 0xff);
		}

		public override void Update()
		{
			base.Update();

			_currentAccelSize.x = GetHudAccelWidth();
			_currentSpeedSize.x = GetHudSpeedWidth();
			AccelGauge.sizeDelta = _currentAccelSize;
			SpeedGauge.sizeDelta = _currentSpeedSize;
			Value.text = GetSpeedValueString();

			// Colorize the text on speed change
			_currentSpeed = _currentSpeedSize.x;
			ColorSpeedValue();
			_previousSpeed = _currentSpeed;
		}

		private float GetHudAccelWidth() => 
			Mathf.Clamp(TargetShip.PysSim.enginePower, 0f, 1f) * AccelMaxWidth;

		private float GetHudSpeedWidth()
		{
			_computedValue = 
				TargetShip.T.InverseTransformDirection(TargetShip.RBody.velocity).z /
				TargetShip.Settings.ENGINE_MAXSPEED_SPECTRE;
			_computedValue = Mathf.Clamp(
				Mathf.Clamp(_computedValue * 3f, 0f, 1f), 0f, 1f);

			return _computedValue * SpeedMaxWidth;
		}

		private string GetSpeedValueString() => 
			IntStrDb.GetNumber(Mathf.Abs(Mathf.RoundToInt(TargetShip.HudSpeed)));

		private void ColorSpeedValue()
		{
			if (!OptionSpeedHighlight)
			{
				return;
			}
			Color color = Value.color;
			if (
				_currentSpeed < _previousSpeed &&
				TargetShip.BoostLevel == 0
				)
			{
				// 4-frame transition in 60 fps => 60/4 = 15
				color = Color.Lerp(color, _highlightColor, Time.deltaTime * 15f);
			} 
			else
			{
				color = Color.Lerp(color, _defaultColor, Time.deltaTime * 15f);
			}
			Value.color = color;
		}
	}

	public class EnergyMeter : ScriptableHud
	{
		private Text Number;
		private Image GaugeBackground;
		private RectTransform Gauge;
		private float MaxWidth;
		private Vector2 _currentSize;
		private float _computedValue;
		private float _adjustedDamageMult;

		public override void Start()
		{
			base.Start();

			Number = CustomComponents.GetById<Text>("Number");
			GaugeBackground = CustomComponents.GetById<Image>("GaugeBackground");
			Gauge = CustomComponents.GetById<RectTransform>("Gauge");

			// Gauge is stored in its maximum size, store the max width here.
			MaxWidth = Gauge.sizeDelta.x;

			// Initiate the gauge size.
			_currentSize.x = MaxWidth;
			_currentSize.y = Gauge.sizeDelta.y;
			Gauge.sizeDelta = _currentSize;

			// Coloring
			Number.color = GetTintColor();
			GaugeBackground.color = GetTintColor(TextAlpha.ThreeEighths);
			Gauge.GetComponent<Image>().color = GetTintColor(TextAlpha.ThreeQuarters);
		}

		public override void Update()
		{
			base.Update();
			
			_currentSize.x = GetHudShieldWidth();
			Gauge.sizeDelta = _currentSize;
			Number.text = GetShieldValueString();

		}

		/*
		 * # NgShips.ShipSettings
		 * - DAMAGE_SHIELD = Shield Integrity
		 * - DAMAGE_MULT = Shield Effectiveness
		 * - DAMAGE_PWR = Weapon Effectiveness
		 */

		private float GetHudShieldWidth()
		{
			_computedValue =
				TargetShip.ShieldIntegrity / TargetShip.Settings.DAMAGE_SHIELD;
			_computedValue = Mathf.Clamp(_computedValue, 0f, 1f);

			return _computedValue * MaxWidth;
		}

		private string GetShieldValueString()
		{
			if (Hud.ShieldDisplayMode == EShieldDisplayMode.Absolute)
			{
				_adjustedDamageMult = TargetShip.Settings.DAMAGE_MULT + 0.07f;
				if (_adjustedDamageMult > 1f)
				{
					_adjustedDamageMult *= 1.1f;
				}
				else if (_adjustedDamageMult < 0f)
				{
					_adjustedDamageMult *= 0.9f;
				}

				_computedValue = Mathf.Ceil(
					TargetShip.ShieldIntegrity / _adjustedDamageMult);
				_computedValue = Mathf.Max(0f, _computedValue);
			}
			else
			{
				_computedValue = Mathf.Ceil(_computedValue * 100f) / 100f * 100f;
			}

			return TargetShip.Settings.DAMAGE_MULT <= 0f ? 
				"" : 
				IntStrDb.GetNoSingleCharNumber(Mathf.RoundToInt(_computedValue));
		}
	}

	public class Timer : ScriptableHud
	{}

	public class SpeedLapTimer : ScriptableHud
	{}

	public class Placement : ScriptableHud
	{}

	public class LapCounter : ScriptableHud
	{}

	public class MessageLogger : ScriptableHud
	{}

	public class NowPlaying : ScriptableHud
	{}

	public class PickupDisplay : ScriptableHud
	{}

	public class ZoneTracker : ScriptableHud
	{}

	public class ZoneEnergyMeter : ScriptableHud
	{}

	public class TargetTimeDisplay : ScriptableHud
	{}

	public class TargetZoneDisplay : ScriptableHud
	{}

	public class Leaderboard : ScriptableHud
	{}

	public class CombatScoreboard : ScriptableHud
	{}

	public class TeamScoreboard : ScriptableHud
	{}

	public class UpsurgeScoreboard : ScriptableHud
	{}
}
