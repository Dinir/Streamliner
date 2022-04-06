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
	/// <summary>
	/// Makes a panel that has following RectTransform hierarchy defined on Unity:
	/// - Panel (has Image. a background area)
	///		- Label (has Text. stays around the background)
	///		- Value (has Text. the main dynamic element)
	///		- Gauge Background (has Image. an overlay for the gauge)
	///			- Gauge (has Image, set to maximum width. the gauge sprite)
	/// The string arguments in <c>Find()</c> are the names I set to components on Unity.
	/// </summary>
	public class BasicPanel
	{
		private readonly Text _label;
		internal readonly Text Value;
		protected readonly RectTransform GaugeBackground;
		private readonly RectTransform _gauge;
		internal Color GaugeColor;
		private readonly float _gaugeMaxWidth;
		internal Vector2 CurrentSize;

		public BasicPanel(RectTransform panelElement)
		{
			_label = panelElement.Find("Label").GetComponent<Text>();
			Value = panelElement.Find("Value").GetComponent<Text>();
			GaugeBackground = panelElement.Find("GaugeBackground").GetComponent<RectTransform>();
			_gauge = (RectTransform)GaugeBackground.Find("Gauge");
			Vector2 sizeDelta = _gauge.sizeDelta;
			_gaugeMaxWidth = sizeDelta.x;
			CurrentSize.y = sizeDelta.y;

			ChangeColor();
			Fill(0f);
		}

		// NEVER MAKE A METHOD BEING CALLED IN BASE CONSTRUCTOR VIRTUAL
		// NEVER OVERRIDE THIS SHIT BELOW
		private void ChangeColor()
		{
			GaugeColor = GetTintColor();
			_label.color = GaugeColor;
			Value.color = GaugeColor;
			_gauge.GetComponent<Image>().color = GaugeColor;
			GaugeBackground.GetComponent<Image>().color = 
				GetTintColor(TextAlpha.ThreeEighths);
		}

		public void ChangeDataPartColor(Color color)
		{
			Value.color = color;
			_gauge.GetComponent<Image>().color = color;
		}

		public void Fill(float amount)
		{
			CurrentSize.x = amount * _gaugeMaxWidth;
			_gauge.sizeDelta = CurrentSize;
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
	public class SpeedPanel : BasicPanel
	{
		private readonly RectTransform _accelGauge;
		private readonly float _accelGaugeMaxWidth;
		private Vector2 _currentAccelSize;

		public SpeedPanel(RectTransform panelElement) : base(panelElement)
		{
			_accelGauge = (RectTransform)GaugeBackground.Find("AccelGauge");
			Vector2 sizeDelta = _accelGauge.sizeDelta;
			_accelGaugeMaxWidth = sizeDelta.x;
			_currentAccelSize.y = sizeDelta.y;

			ChangeAccelColor();
			FillAccel(0f);
		}

		private void ChangeAccelColor() => 
			_accelGauge.GetComponent<Image>().color = GetTintColor(TextAlpha.Quarter);

		public void FillAccel(float amount)
		{
			_currentAccelSize.x = amount * _accelGaugeMaxWidth;
			_accelGauge.sizeDelta = _currentAccelSize;
		}
	}

	public class Speedometer : ScriptableHud
	{
		internal SpeedPanel Panel;
		private float _computedValue;

		internal readonly Color HighlightColor = new Color32(0xf2, 0x61, 0x6b, 0xff); // Red S.60 V.95

		private float _currentSpeed;
		private float _previousSpeed;

		private readonly float _animationSpeed = 8f;
		private readonly float _animationTimerMax = 1.5f;
		private float _speedDecreaseAnimationTimer;
		private float _speedIncreaseAnimationTimer;

		public override void Start()
		{
			base.Start();

			Panel = new SpeedPanel(CustomComponents.GetById<RectTransform>("Panel"));
		}

		public override void Update()
		{
			base.Update();

			Panel.FillAccel(GetHudAccelWidth());
			Panel.Fill(GetHudSpeedWidth());
			Panel.Value.text = GetSpeedValueString();

			_currentSpeed = Panel.CurrentSize.x;
			ColorSpeedComponent();
			_previousSpeed = _currentSpeed;
		}

		private float GetHudAccelWidth() =>
			Mathf.Clamp(TargetShip.PysSim.enginePower, 0f, 1f);

		private float GetHudSpeedWidth()
		{
			_computedValue =
				TargetShip.T.InverseTransformDirection(TargetShip.RBody.velocity).z /
				TargetShip.Settings.ENGINE_MAXSPEED_SPECTRE;
			_computedValue = Mathf.Clamp(
				Mathf.Clamp(_computedValue * 3f, 0f, 1f), 0f, 1f);

			return _computedValue;
		}
		private string GetSpeedValueString() =>
			IntStrDb.GetNumber(Mathf.Abs(Mathf.RoundToInt(TargetShip.HudSpeed)));

		private void ColorSpeedComponent()
		{
			if (!OptionSpeedHighlight) return;

			if (_currentSpeed < _previousSpeed)
			{
				_speedDecreaseAnimationTimer = _animationTimerMax;
				_speedIncreaseAnimationTimer = 0f;
			}
			else
			{
				_speedIncreaseAnimationTimer = _animationTimerMax;
				_speedDecreaseAnimationTimer = 0f;
			}

			Color color = Panel.Value.color;

			if (_speedDecreaseAnimationTimer > 0f)
			{
				color = Color.Lerp(color, HighlightColor, Time.deltaTime * _animationSpeed);
				_speedDecreaseAnimationTimer -= Time.deltaTime;
			}
			else
				_speedDecreaseAnimationTimer = 0f;

			if (_speedIncreaseAnimationTimer > 0f)
			{
				color = Color.Lerp(color, Panel.GaugeColor, Time.deltaTime * _animationSpeed);
				_speedIncreaseAnimationTimer -= Time.deltaTime;
			}
			else
				_speedIncreaseAnimationTimer = 0f;

			Panel.ChangeDataPartColor(color);
		}
	}

	public class EnergyMeter : ScriptableHud
	{
		public Text Value;
		public Image GaugeBackground;
		public RectTransform Gauge;
		public float MaxWidth;
		private Vector2 _currentSize;
		private float _computedValue;
		private float _adjustedDamageMult;

		private float _currentEnergy;
		private float _previousEnergy;

		private readonly Color _rechargeColor = new Color32(0x88, 0xe3, 0xe0, 0xbf); // Cyan S6 V1
		private readonly Color _lowColor = new Color32(0xe3, 0xb3, 0x88, 0xbf); // Orange S6 V1
		private readonly Color _criticalColor = new Color32(0xe3, 0x88, 0x8e, 0xbf); // Red S6 V1
		private readonly Color _damageColor = new Color32(0xf2, 0x61, 0x6b, 0xff); // Red S.60 V.95
		private readonly Color _damageLowColor = new Color32(0xf2, 0xed, 0x61, 0xff); // Yellow S.60 V.95

		private Color _defaultColor;
		private Color _currentColor;
		private Color _currentDamageColor;

		/*
		 * When would the progress reach 0.99999 (1 - 10^-5) with the speed?
		 * That's log(10^-5) / 60log(1-(s/60)) seconds.
		 * When would the progress reach 0.75 (1 - 0.25) which can be considered gone on eyes?
		 * That's log(0.25) / 60log(1-(s/60)) seconds.
		 * 
		 * Speed is decided on the 0.75 time, and Timer is decided on 0.99999 time of that speed.
		 * https://www.desmos.com/calculator/nip7pyehxl
		 */
		private readonly float _damageAnimationSpeed = 8f;
		private readonly float _transitionAnimationSpeed = 5f;
		private readonly float _damageAnimationTimerMax = 1.5f;
		private readonly float _transitionAnimationTimerMax = 2.2f;
		private float _damageAnimationTimer;
		private float _transitionAnimationTimer;

		public override void Start()
		{
			base.Start();

			Value = CustomComponents.GetById<Text>("Value");
			GaugeBackground = CustomComponents.GetById<Image>("GaugeBackground");
			Gauge = (RectTransform)GaugeBackground.GetComponent<RectTransform>()
				.Find("Gauge");

			// Gauge is stored in its maximum size, store the max width here.
			Vector2 sizeDelta = Gauge.sizeDelta;
			MaxWidth = sizeDelta.x;

			// Initiate the gauge size.
			_currentSize.x = MaxWidth;
			_currentSize.y = sizeDelta.y;
			sizeDelta = _currentSize;
			Gauge.sizeDelta = sizeDelta;

			// Coloring
			_defaultColor = GetTintColor(TextAlpha.ThreeQuarters);
			GaugeBackground.color = GetTintColor(TextAlpha.ThreeEighths);
			_currentColor = _defaultColor;
			_currentDamageColor = _damageColor;
			Value.color = _defaultColor;
			Gauge.GetComponent<Image>().color = _defaultColor;
		}

		public override void Update()
		{
			base.Update();
			
			_currentSize.x = GetHudShieldWidth();
			Gauge.sizeDelta = _currentSize;
			Value.text = GetShieldValueString();

			_currentEnergy = TargetShip.ShieldIntegrity;
			ColorEnergyComponent();
			_previousEnergy = _currentEnergy;
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
					_adjustedDamageMult *= 1.1f;
				else if (_adjustedDamageMult < 0f)
					_adjustedDamageMult *= 0.9f;

				_computedValue = Mathf.Ceil(
					TargetShip.ShieldIntegrity / _adjustedDamageMult);
				_computedValue = Mathf.Max(0f, _computedValue);
			}
			else
				_computedValue = Mathf.Ceil(_computedValue * 100f) / 100f * 100f;

			return TargetShip.Settings.DAMAGE_MULT <= 0f ? 
				"" : 
				IntStrDb.GetNoSingleCharNumber(Mathf.RoundToInt(_computedValue));
		}

		private void ColorEnergyComponent()
		{
			// Set timer during which the coloring transition can run
			// damage flash
			if (_currentEnergy < _previousEnergy)
				_damageAnimationTimer = _damageAnimationTimerMax;
			// transition
			if (
				OptionLowEnergy != 0 && (
					(_currentEnergy <= 25f && _previousEnergy > 25f) ||
					(_currentEnergy <= 10f && _previousEnergy > 10f) ||
					(_currentEnergy > 25f && _previousEnergy <= 25f) ||
					(_currentEnergy > 10f && _previousEnergy <= 10f)
				) ||
				TargetShip.IsRecharging
			)
			{
				_transitionAnimationTimer = _transitionAnimationTimerMax;
				// Charging takes over damage flash and stops the flash timer
				if (TargetShip.IsRecharging)
				{
					_damageAnimationTimer = 0f;
				}
			}

			Color color = Value.color;

			// Set target color for the transition to take between
			// transition
			if (_transitionAnimationTimer > 0f)
			{
				if (TargetShip.IsRecharging)
					_currentColor = _rechargeColor;
				else if (_currentEnergy <= 25f)
				{
					if (_currentEnergy > 10f)
					{
						_currentColor =
							(OptionLowEnergy == 2 || Audio.WarnOfLowEnergy) ?
							_lowColor : _defaultColor;
					}
					else
					{
						_currentColor =
							(OptionLowEnergy == 2 || Audio.WarnOfCriticalEnergy) ?
							_criticalColor : _defaultColor;
					}
				}
				else
					_currentColor = _defaultColor;

				color = Color.Lerp(color, _currentColor, Time.deltaTime * _transitionAnimationSpeed);
				_transitionAnimationTimer -= Time.deltaTime;
			}
			else
				_transitionAnimationTimer = 0f;

			// damage flash (process after getting `_currentColor` set)
			if (_damageAnimationTimer > 0f)
			{
				_currentDamageColor =
					_currentEnergy > 10f || 
					OptionLowEnergy == 0 || 
					(OptionLowEnergy == 1 && !Audio.WarnOfCriticalEnergy) ?
					_damageColor : _damageLowColor;

				// ReSharper disable once CompareOfFloatsByEqualityOperator
				if (_damageAnimationTimer == _damageAnimationTimerMax) 
					color = _currentDamageColor;
				color = Color.Lerp(color, _currentColor, Time.deltaTime * _damageAnimationSpeed);
				_damageAnimationTimer -= Time.deltaTime;
			}
			else
				_damageAnimationTimer = 0f;

			// Apply the final color
			Value.color = color;
			Gauge.GetComponent<Image>().color = color;
		}
	}

	public class Timer : ScriptableHud
	{
		public BasicPanel Panel;

	}

	public class SpeedLapTimer : ScriptableHud
	{}

	public class Placement : ScriptableHud
	{}

	public class LapCounter : ScriptableHud
	{}

	public class MessageLogger : ScriptableHud
	{}

	public class PositionTracker : ScriptableHud
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
