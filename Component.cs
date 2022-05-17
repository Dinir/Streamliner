using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using NgAudio;
using NgData;
using NgEvents;
using NgGame;
using NgLib;
using NgModes;
using NgMp;
using NgMp.Packets;
using NgPickups;
using NgPickups.Physical;
using NgSettings;
using NgShips;
using NgSp;
using NgTrackData.Triggers;
using NgUi.RaceUi;
using NgUi.RaceUi.HUD;
using static Streamliner.HudRegister;
using static Streamliner.PresetColorPicker;
using static Streamliner.SectionManager;

namespace Streamliner
{
	public class CustomScaleScriptableHud : ScriptableHud
	{
		public override void ConfigureForSplitscreen(int playerIndex)
		{
			base.ConfigureForSplitscreen(playerIndex);

			if (!Gameplay.SplitscreenEnabled || !CanBeAdjustedForSplitscreen)
			{
				return;
			}
			if (playerIndex > 1)
			{
				Debug.LogWarning("Provided player index is above 1!");
				return;
			}
			CanvasScaler component2 = GetComponent<CanvasScaler>();
			if (!component2)
			{
				Debug.LogError("Cannot scale HUD!");
				return;
			}

			// base method multiplies it by 1.2.
			component2.referenceResolution *= 1.25f;

			/*
			 * HudContainer Offset Values
			 *
			 *            CanvasScaler.referenceResolution.x
			 * ├────────────────────┤
			 * ┏━━━━━━━━━━━━━━━━┯━━━┓┬
			 * ┃                ↑   ┃│
			 * ┃                4   ┃│
			 * ┃                ↑   ┃│
			 * ┃   ┌────────────┼→3→┨│
			 * ┃   │HudContainer│   ┃│CanvasScaler.referenceResolution.y
			 * ┞···└────────────┘   ┃│
			 * ↑   ·                ┃│
			 * 2   ·                ┃│
			 * ↑   ·                ┃│
			 * └→1→┶━━━━━━━━━━━━━━━━┛┴
			 * 1: HudContainer.offsetMin.x
			 * 2: HudContainer.offsetMin.y
			 * 3: HudContainer.offsetMax.x
			 * 4: HudContainer.offsetMax.y
			 * Arrows indicate the direction of a positive value.
			 *
			 * When all the offset values are set to zero,
			 * HudContainer fills up the entire CanvasScaler area.
			 */

			float verticalSplitscreenHeightGap =
				(component2.referenceResolution.y - component2.referenceResolution.x * 3f / 8f ) * 0.5f;

			// `NgSettings.Gameplay.InHorizontalSplitscreen` always returns false.
			if (playerIndex == 0)
			{
				if (Gameplay.InVerticalSplitscreen)
				{
					HudContainer.offsetMin = new Vector2(
						0f,
						verticalSplitscreenHeightGap
					);
					HudContainer.offsetMax = new Vector2(
						(0f - component2.referenceResolution.x) * 0.5f,
						-verticalSplitscreenHeightGap
					);
				}
				else
				{
					HudContainer.offsetMin = new Vector2(
						0f,
						component2.referenceResolution.y * 0.5f
					);
				}
			}

			if (playerIndex == 1)
			{
				if (Gameplay.InVerticalSplitscreen)
				{
					HudContainer.offsetMin = new Vector2(
						component2.referenceResolution.x * 0.5f,
						verticalSplitscreenHeightGap
					);
					HudContainer.offsetMax = new Vector2(
						0f,
						-verticalSplitscreenHeightGap
					);
				}
				else
				{
					HudContainer.offsetMax = new Vector2(
						0f,
						(0f - component2.referenceResolution.y) * 0.5f
					);
				}
			}

			// Apply Rect Mask 2D if available.
			RectMask2D mask = CustomComponents.GetById("Base").parent.GetComponent<RectMask2D>();
			if (mask is not null) mask.enabled = true;
		}
	}

	public class Speedometer : CustomScaleScriptableHud
	{
		private SpeedPanel _panel;
		private float _computedValue;

		private Color _highlightColor;

		private float _currentSpeed;
		private float _previousSpeed;
		private const float _minSpeedLossIntensity = 1f;
		private float _speedLossIntensity = _minSpeedLossIntensity;

		private const float _animationSpeed = 5f;
		private const float _animationTimerMax = 2.25f;
		private float _speedDecreaseAnimationTimer;
		private float _speedIncreaseAnimationTimer;

		private string _gamemodeName;
		private Color? _currentZoneColor = null;
		private bool _usingZoneColors;

		public override void Start()
		{
			base.Start();
			_panel = new SpeedPanel(CustomComponents.GetById("Base"));
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);

			_gamemodeName = RaceManager.CurrentGamemode.Name;
			_usingZoneColors = _gamemodeName switch
			{
				"Survival" when OptionZoneTintOverride => true,
				"Upsurge" when OptionZoneTintOverride => true,
				_ => false
			};

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame || _usingZoneColors)
				_highlightColor = GetTintColor(clarity: 0);
			else
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_panel.UpdateColor(GetTintFromColor(color: engineColor));
				_highlightColor = GetTintFromColor(color: engineColor, clarity: 0);
			}

			if (_usingZoneColors)
			{
				switch (_gamemodeName)
				{
					case "Survival": NgUiEvents.OnZoneNumberUpdate += UpdateToZoneColor; break;
					case "Upsurge": NgRaceEvents.OnShipScoreChanged += UpdateToZoneColor; break;
				}
			}
		}

		public override void Update()
		{
			base.Update();
			if (_usingZoneColors && _currentZoneColor is null && PalleteSettingsLoaded())
				UpdateToZoneColor(0);

			_panel.FillAccel(GetHudAccelWidth());
			_panel.Fill(GetHudSpeedWidth());
			_panel.Value.text = GetSpeedValueString();

			if (!OptionSpeedHighlight)
				return;

			_currentSpeed = _panel.CurrentSize.x;
			UpdateSpeedLossIntensity();
			ColorSpeedComponent();
			_previousSpeed = _currentSpeed;
		}

		private float GetHudAccelWidth() =>
			TargetShip.PysSim is null ? 0f :
				Mathf.Clamp(TargetShip.PysSim.enginePower, 0f, 1f);

		private float GetHudSpeedWidth()
		{
			if (TargetShip.T is null) return 0f;
			_computedValue =
				TargetShip.T.InverseTransformDirection(TargetShip.RBody.velocity).z /
				TargetShip.Settings.ENGINE_MAXSPEED_SPECTRE;
			_computedValue = Mathf.Clamp(
				Mathf.Clamp(_computedValue * 3f, 0f, 1f), 0f, 1f);

			return _computedValue;
		}
		private string GetSpeedValueString()
		{
			/* 
			 * `Mathf.RoundToInt(TargetShip.HudSpeed)` can hit `Int32.MinValue`,
			 * around the time the player ship is about to respawn.
			 * This value should be not passed to `Math.Abs()`.
			 */
			int value = Mathf.RoundToInt(TargetShip.HudSpeed);
			return IntStrDb.GetNumber(value < 0 ? 0 : value);
		}

		private void UpdateSpeedLossIntensity()
		{
			_speedLossIntensity =
				(_previousSpeed - _currentSpeed)
				/ (_previousSpeed < 1f ? 1f : _previousSpeed)
				* Shifter.SpeedChangeIntensityThreshold;

			_speedLossIntensity =
				_speedLossIntensity <= -_minSpeedLossIntensity ? -_minSpeedLossIntensity :
				_speedLossIntensity >= _minSpeedLossIntensity ? _minSpeedLossIntensity :
				_speedLossIntensity;
		}

		private void ColorSpeedComponent()
		{
			if (_speedLossIntensity > 0f)
			{
				_speedDecreaseAnimationTimer = _animationTimerMax;
				_speedIncreaseAnimationTimer = 0f;
			}
			else
			{
				_speedDecreaseAnimationTimer = 0f;
				_speedIncreaseAnimationTimer = _animationTimerMax;
			}

			Color color = _panel.Value.color;

			if (_speedDecreaseAnimationTimer > 0f)
			{
				if (color != _highlightColor)
					color = _speedLossIntensity == _minSpeedLossIntensity ?
						_highlightColor :
						Color.Lerp(color, _highlightColor, Time.deltaTime * _animationSpeed);

				_speedDecreaseAnimationTimer -= Time.deltaTime;
			}
			else
				_speedDecreaseAnimationTimer = 0f;

			if (_speedIncreaseAnimationTimer > 0f)
			{
				if (color != _panel.GaugeColor)
					color = 
						Color.Lerp(color, _panel.GaugeColor, Time.deltaTime * _animationSpeed);

				_speedIncreaseAnimationTimer -= Time.deltaTime;
			}
			else
				_speedIncreaseAnimationTimer = 0f;

			_panel.ChangeDataPartColor(color);
		}

		private void UpdateToZoneColor(int zoneNumber)
		{
			Color color = GetZoneColor(zoneNumber);
			if (_currentZoneColor == color)
				return;

			_currentZoneColor = color;
			_panel.UpdateColor(GetTintFromColor(color: color));
			_highlightColor = GetTintFromColor(color: color, clarity: 0);
		}
		private void UpdateToZoneColor(string number)
		{
			int zoneNumber = Convert.ToInt32(number);
			if (zoneNumber % 5 != 0)
				return;
			UpdateToZoneColor(zoneNumber);
		}
		private void UpdateToZoneColor(ShipController ship, float oldScore, float newScore)
		{
			if (ship != TargetShip)
				return;
			UpdateToZoneColor((int) newScore);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (_usingZoneColors)
			{
				switch (_gamemodeName)
				{
					case "Survival": NgUiEvents.OnZoneNumberUpdate -= UpdateToZoneColor; break;
					case "Upsurge": NgRaceEvents.OnShipScoreChanged -= UpdateToZoneColor; break;
				}
			}
		}
	}

	public class EnergyMeter : CustomScaleScriptableHud
	{
		private RectTransform _panel;
		private Text _value;
		private Text _delta;
		private Image _gaugeBackground;
		private RectTransform _gauge;
		private Image _gaugeImage;
		private Vector2 _maxSize;
		private Vector2 _currentSize;
		private float _computedValue;
		private float _adjustedDamageMult;
		private int _valueBeforeCharging;
		private string ValueBeforeCharging
		{
			set => _valueBeforeCharging = Convert.ToInt32(value);
		}
		private string ValueCharged()
		{
			int value = Convert.ToInt32(_value.text) - _valueBeforeCharging;
			return IntStrDb.GetNumber(value);
		}
		private string ValueGained()
		{
			int value = Convert.ToInt32(_value.text) - Convert.ToInt32(_previousValueString);
			return IntStrDb.GetNumber(value);
		}

		private float _currentEnergy;
		private float _previousEnergy;
		private string _previousValueString;
		private bool _isRecharging;
		private bool _wasRecharging;
		private bool _energyRegained;
		private bool _energyConstantlyDischarges;

		private readonly Color _rechargeColor = GetTintColor(TextAlpha.ThreeQuarters, 7, 4);
		private readonly Color _lowColor = GetTintColor(TextAlpha.ThreeQuarters, 2, 4);
		private readonly Color _criticalColor = GetTintColor(TextAlpha.ThreeQuarters, 1, 4);
		private readonly Color _damageColor = GetTintColor(tintIndex: 1, clarity: 5);
		private readonly Color _damageLowColor = GetTintColor(tintIndex: 3, clarity: 5);

		private Color _defaultColor;
		private Color _currentColor;
		private Color _currentDamageColor;
		private Color _deltaColor;
		private Color _deltaFinalColor;
		private Color _deltaInactiveColor;
		private const float DeltaFinalAlpha = 0.9f;
		private const float DeltaInactiveAlpha = 0f;

		/*
		 * When would the progress reach 0.99999 (1 - 10^-5) with the speed?
		 * That's log(10^-5) / 60log(1-(s/60)) seconds.
		 * When would the progress reach 0.75 (1 - 0.25) which can be considered gone on eyes?
		 * That's log(0.25) / 60log(1-(s/60)) seconds.
		 *
		 * Speed is decided on the 0.75 time, and Timer is decided on 0.99999 time of that speed.
		 * https://www.desmos.com/calculator/nip7pyehxl
		 */
		private const float FastTransitionSpeed = 8f;
		private const float SlowTransitionSpeed = 5f;
		private const float FastTransitionTimerMax = 1.5f;
		private const float SlowTransitionTimerMax = 2.25f;
		private const float RechargeDisplayTimerMax = 3.0f;
		private float _damageAnimationTimer;
		private float _transitionAnimationTimer;
		private float _deltaAnimationTimer;
		private float _rechargeDisplayTimer;

		private string _gamemodeName;
		private Color? _currentZoneColor = null;
		private bool _usingZoneColors;

		public override void Start()
		{
			base.Start();
			_energyConstantlyDischarges =
				RaceManager.CurrentGamemode.Name == "Rush Hour";

			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_value = _panel.Find("Value").GetComponent<Text>();
			_delta = _panel.Find("Delta").GetComponent<Text>();
			_gaugeBackground = _panel.Find("GaugeBackground").GetComponent<Image>();
			_gauge = (RectTransform)_gaugeBackground.GetComponent<RectTransform>()
				.Find("Gauge");
			_gaugeImage = _gauge.GetComponent<Image>();

			// Gauge is stored in its maximum size, store the max width here.
			_maxSize = _gauge.sizeDelta;

			// Initiate the gauge size.
			_currentSize = _maxSize;
			_gauge.sizeDelta = _currentSize;

			_gamemodeName = RaceManager.CurrentGamemode.Name;
			_usingZoneColors = _gamemodeName switch
			{
				"Survival" when OptionZoneTintOverride => true,
				"Upsurge" when OptionZoneTintOverride => true,
				_ => false
			};

			if (_usingZoneColors)
			{
				switch (_gamemodeName)
				{
					case "Survival": NgUiEvents.OnZoneNumberUpdate += UpdateToZoneColor; break;
					case "Upsurge": NgRaceEvents.OnShipScoreChanged += UpdateToZoneColor; break;
				}
			}

			// Coloring
			if (OptionValueTint != OptionValueTintShipEngineIndexForGame || _usingZoneColors)
				UpdateColor();
			else
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				UpdateColor(engineColor);
			}

			// assigning to `_defaultColor` here is to ensure the colors are set at `Start()`.
			_currentColor = _defaultColor;
			_currentDamageColor = _damageColor;
			_value.color = _defaultColor;
			_gaugeImage.color = _defaultColor;
			_deltaColor = _delta.color;
			_deltaFinalColor = _delta.color with { a = DeltaFinalAlpha };
			_deltaInactiveColor = _delta.color with { a = DeltaInactiveAlpha };
			_delta.color = _deltaInactiveColor;
		}

		private void UpdateColor()
		{
			_defaultColor = GetTintColor(TextAlpha.ThreeQuarters);
			_gaugeBackground.color = GetTintColor(TextAlpha.ThreeEighths);

			_currentColor = _defaultColor;

			if (_transitionAnimationTimer != 0 || _damageAnimationTimer != 0)
				return;

			_value.color = _defaultColor;
			_gaugeImage.color = _defaultColor;
		}
		private void UpdateColor(Color color)
		{
			_defaultColor = GetTintFromColor(TextAlpha.ThreeQuarters, color);
			_gaugeBackground.color = GetTintFromColor(TextAlpha.ThreeEighths, color);

			_currentColor = _defaultColor;

			if (_transitionAnimationTimer != 0 || _damageAnimationTimer != 0)
				return;

			_value.color = _defaultColor;
			_gaugeImage.color = _defaultColor;
		}

		public override void Update()
		{
			base.Update();
			if (_usingZoneColors && _currentZoneColor is null && PalleteSettingsLoaded())
				UpdateToZoneColor(0);

			_currentEnergy = TargetShip.ShieldIntegrity;
			_isRecharging = TargetShip.IsRecharging;
			_energyRegained =
				!(_isRecharging || _wasRecharging) &&
				_currentEnergy > _previousEnergy &&
				Race.HasCountdownFinished;

			_currentSize.x = GetHudShieldWidth();
			_currentSize.y = _currentSize.x >= _maxSize.y ?
				_maxSize.y : _currentSize.x;
			_gauge.sizeDelta = _currentSize;
			_value.text = GetShieldValueString();

			if (!OptionEnergyChange && OptionLowEnergy == 0 && !OptionRechargeAmount)
				return;

			if (_isRecharging)
			{
				/*
				 * `_wasRecharging` could be set to true the first time the routine enters here,
				 * and recharging time can be too short for it to update `_valueBeforeCharging`.
				 * The text need to be flushed before.
				 */
				if (_wasRecharging)
					_delta.text = ValueCharged();
				else
					ValueBeforeCharging = _previousValueString;
			}
			else if (_energyRegained)
				_delta.text = ValueGained();

			/*
			 * `_delta.text` will be flushed in this method
			 * when the whole sequence is done and the text is set to its inactive color.
			 */
			ColorEnergyComponent();

			_previousEnergy = _currentEnergy;
			_previousValueString = _value.text;
			_wasRecharging = _isRecharging;
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
				_currentEnergy / TargetShip.Settings.DAMAGE_SHIELD;
			_computedValue = Mathf.Clamp(_computedValue, 0f, 1f);

			return _computedValue * _maxSize.x;
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
					_currentEnergy / _adjustedDamageMult);
				_computedValue = Mathf.Max(0f, _computedValue);
			}
			else
				_computedValue = Mathf.Ceil(_computedValue * 100f) / 100f * 100f;

			return TargetShip.Settings.DAMAGE_MULT <= 0f ?
				"" :
				IntStrDb.GetNumber(Mathf.RoundToInt(_computedValue));
		}

		/// <summary>
		/// # Energymeter Color
		/// 
		/// Take `_value.color` and label it as just `color`,
		/// then set `_currentColor` and apply a color inbetween to `color`.
		/// When damage is received, set `_currentDamageColor` and
		/// apply a color inbetween to `color`.
		/// 
		/// `_currentColor` can be any of those:
		/// `_rechargeColor`, `_lowColor`, `_criticalColor`, `_defaultColor`
		/// `_currentDamageColor` can be any of those:
		/// `_damageColor`, `_damageLowColor`
		/// 
		/// After `color` is set, apply it to `_value` and `_gaugeImage`.
		/// 
		/// # Recharge Value Color
		/// 
		/// Take `_delta.color` and label it as `deltaColor`.
		/// When recharge is going, set it as a color between it and `_deltaColor`.
		/// When not recharging, set it as `_deltaInactiveColor`.
		/// When recharging is over, set it as `_deltaFinalColor` then finally as `_deltaInactiveColor`.
		/// 
		/// After `deltaColor` is set, apply it to `_delta`.
		/// </summary>
		private void ColorEnergyComponent()
		{
			// Set timer during which the coloring transition can run
			// damage flash
			if (
				OptionEnergyChange &&
				!_energyConstantlyDischarges &&
				_currentEnergy < _previousEnergy
			)
				_damageAnimationTimer = FastTransitionTimerMax;
			// transition
			if (
				OptionLowEnergy != 0 && (
					_currentEnergy <= 25f && _previousEnergy > 25f ||
					_currentEnergy <= 10f && _previousEnergy > 10f ||
					_currentEnergy > 25f && _previousEnergy <= 25f ||
					_currentEnergy > 10f && _previousEnergy <= 10f
				) ||
				_isRecharging || _energyRegained
			)
			{
				_transitionAnimationTimer = SlowTransitionTimerMax;
				// Charging takes over damage flash and recharge amount display
				if (_isRecharging || _energyRegained)
				{
					if (OptionRechargeAmount)
						_deltaAnimationTimer = SlowTransitionTimerMax;
					_rechargeDisplayTimer = 0f;
					_damageAnimationTimer = 0f;
				}
			}

			Color color = _value.color;

			// Set target color for the transition to take between
			// transition
			if (_transitionAnimationTimer > 0f)
			{
				if (OptionEnergyChange && _isRecharging)
					_currentColor = _rechargeColor;
				else if (OptionLowEnergy != 0 && _currentEnergy <= 25f)
				{
					if (_currentEnergy > 10f)
					{
						_currentColor =
							OptionLowEnergy == 2 || Audio.WarnOfLowEnergy ?
								_lowColor : _defaultColor;
					}
					else
					{
						_currentColor =
							OptionLowEnergy == 2 || Audio.WarnOfCriticalEnergy ?
								_criticalColor : Audio.WarnOfLowEnergy ?
									_lowColor : _defaultColor;
					}
				}
				else
					_currentColor = _defaultColor;

				color = Color.Lerp(
					color, _currentColor, Time.deltaTime * SlowTransitionSpeed);
				_transitionAnimationTimer -= Time.deltaTime;
			}
			else
				_transitionAnimationTimer = 0f;

			// recharging amount transition
			Color deltaColor = _delta.color;
			if (_deltaAnimationTimer > 0f)
			{
				if (_isRecharging || _energyRegained)
				{
					deltaColor = _wasRecharging ?
						Color.Lerp(
							deltaColor, _deltaColor, Time.deltaTime * SlowTransitionSpeed
						) :
						_deltaInactiveColor;
				}
				else
				{
					// Recharging is done here, as the timer is only set when recharging starts.
					// Stop this block and start the display block.
					_rechargeDisplayTimer = RechargeDisplayTimerMax;
					_deltaAnimationTimer = 0f;
				}

				_deltaAnimationTimer -= Time.deltaTime;
			}
			else
				_deltaAnimationTimer = 0f;

			// recharged amount display
			if (_rechargeDisplayTimer > 0f)
			{
				if (_rechargeDisplayTimer == RechargeDisplayTimerMax)
					deltaColor = _deltaFinalColor;

				_rechargeDisplayTimer -= Time.deltaTime;
				if (_rechargeDisplayTimer <= 0f)
				{
					deltaColor = _deltaInactiveColor;
					// the whole sequence displaying the recharged amount is done.
					// flush `_delta.text`.
					_delta.text = "0";
				}
			}
			else
				_rechargeDisplayTimer = 0f;

			// damage flash (process after getting `_currentColor` set)
			if (_damageAnimationTimer > 0f)
			{
				_currentDamageColor =
					_currentEnergy > 10f ||
					OptionLowEnergy == 0 ||
					OptionLowEnergy == 1 && !Audio.WarnOfCriticalEnergy ?
						_damageColor : _damageLowColor;

				if (_damageAnimationTimer == FastTransitionTimerMax)
					color = _currentDamageColor;
				color = Color.Lerp(
					color, _currentColor, Time.deltaTime * FastTransitionSpeed);
				_damageAnimationTimer -= Time.deltaTime;
			}
			else
				_damageAnimationTimer = 0f;

			// Apply the final color
			_value.color = color;
			_gaugeImage.color = color;
			_delta.color = deltaColor;
		}

		private void UpdateToZoneColor(int zoneNumber)
		{
			Color color = GetZoneColor(zoneNumber);
			if (_currentZoneColor == color)
				return;

			_currentZoneColor = color;
			UpdateColor(GetTintFromColor(color: color));
		}
		private void UpdateToZoneColor(string number)
		{
			int zoneNumber = Convert.ToInt32(number);
			if (zoneNumber % 5 != 0)
				return;
			UpdateToZoneColor(zoneNumber);
		}
		private void UpdateToZoneColor(ShipController ship, float oldScore, float newScore)
		{
			if (ship != TargetShip)
				return;
			UpdateToZoneColor((int)newScore);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (_usingZoneColors)
			{
				switch (_gamemodeName)
				{
					case "Survival": NgUiEvents.OnZoneNumberUpdate -= UpdateToZoneColor; break;
					case "Upsurge": NgRaceEvents.OnShipScoreChanged -= UpdateToZoneColor; break;
				}
			}
		}
	}

	public class Timer : CustomScaleScriptableHud
	{
		private BasicPanel _panel;
		private RectTransform _lapSlotTemplate;

		private readonly BigTimeTextBuilder _bigTimeTextBuilder = new(new StringBuilder());

		private readonly List<LapSlot> _slots = new(5);
		private LapSlot _currentSlot;
		private int _totalSlots;
		/// <summary>
		/// Contains the index of the current lap. It's base-1, but period for 0th exists.
		/// </summary>
		private int _currentLap;
		private int _totalLaps;
		private int _totalSections;

		private class LapSlot
		{
			internal readonly Text Value;
			private readonly Image _perfectLine;
			private bool _perfectLapStatus;

			internal bool PerfectLap
			{
				get => _perfectLapStatus;
				set
				{
					_perfectLapStatus = value;
					_perfectLine.gameObject.SetActive(value);
				}
			}

			public LapSlot(RectTransform template, Color? color)
			{
				Value = template.Find("Time").GetComponent<Text>();
				_perfectLine = template.Find("PerfectLine").GetComponent<Image>();

				Value.text = "";
				Value.gameObject.SetActive(true);
				_perfectLapStatus = false;

				if (color is null)
					ChangeColor(GetTintColor(TextAlpha.ThreeQuarters));
				else
					ChangeColor(GetTintFromColor(TextAlpha.ThreeQuarters, color));
			}

			internal void ChangeColor(Color color) => Value.color = color;
			internal float Alpha
			{
				set => Value.color = Value.color with { a = value };
			}
		}

		private void InitiateSlots()
		{
			_totalSlots = Mathf.Clamp(Race.MaxLaps, 1, 5);
			Color? initialColor = OptionValueTint != OptionValueTintShipEngineIndexForGame ?
				null : GetShipRepresentativeColor(TargetShip);

			for (int i = 0; i < _totalSlots; i++)
			{
				RectTransform slot =
					Instantiate(_lapSlotTemplate.gameObject).GetComponent<RectTransform>();
				slot.SetParent(_lapSlotTemplate.parent);
				slot.localScale = _lapSlotTemplate.localScale;
				slot.anchoredPosition = _lapSlotTemplate.anchoredPosition;

				slot.localPosition += Vector3.up * slot.sizeDelta.y * i;

				_slots.Add(new LapSlot(slot, initialColor));
			}

			// Emphasis the current lap slot by a bit.
			_slots[0].Alpha = GetTransparency(TextAlpha.NineTenths);
		}

		public override void Start()
		{
			base.Start();
			_totalLaps = Race.MaxLaps;
			_totalSections = GetTotalSectionCount();
			_panel = new BasicPanel(CustomComponents.GetById("Base"));
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);
			_lapSlotTemplate = CustomComponents.GetById("LapSlot");
			// I am hiding the components here, not on Unity,
			// because I want to keep them visible on Unity.
			_lapSlotTemplate.Find("Time").gameObject.SetActive(false);
			_lapSlotTemplate.Find("PerfectLine").gameObject.SetActive(false);
			InitiateSlots();
			_currentSlot = _slots[0];

			if (OptionValueTint == OptionValueTintShipEngineIndexForGame)
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_panel.UpdateColor(GetTintFromColor(color: engineColor));
			}

			NgRaceEvents.OnShipLapUpdate += OnLapUpdate;
		}

		public override void Update()
		{
			base.Update();
			if (!TargetShip)
				return;
			UpdateTotalTime();
			UpdateCurrentLapTime();
			UpdateProgressBar();
		}

		// This runs at the last moment of a lap.
		private void OnLapUpdate(ShipController ship)
		{
			if (ship != TargetShip)
				return;
			// I could have made it simpler by feeding ship.CurrentLap to it directly.
			ShiftSlotData();
			_currentLap++;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnShipLapUpdate -= OnLapUpdate;
		}

		/*
		 * This method is complicated because
		 * I used a local field to update current lap, and incremented it after this is finished.
		 *
		 * `ShipController.CurrentLap` gets updated properly before `NgRaceEvents.OnShipLapUpdate` call starts,
		 * so the struggle around finding the lap time for the actual current lap could be avoided,
		 * if I knew how to properly use the field before I started working on this method,
		 * and not a while after I finished it.
		 */
		private void ShiftSlotData()
		{
			// Don't execute when it's the end of the 0th lap or the last lap. Nothing to shift.
			if (_currentLap > _totalLaps || _currentLap == 0)
				return;

			for (int i = _totalSlots - 1; i > 0; i--)
			{
				// Nothing to copy from `_slots[i - 1]` at the moment.
				/*
				 * End of Lap:  0 1 2 3 4 5 6 7
				 * _currentLap: 0 1 2 3 4 5 6 7
				 * The index of the lap to write in to the `slot[i]`, `_currentLap - i + 1`:
				 * on loop i=4          1 2 3 4
				 * on loop i=3        1 2 3 4 5
				 * on loop i=2      1 2 3 4 5 6
				 * on loop i=1    1 2 3 4 5 6 7
				 */
				if (_currentLap - i + 1 <= 0)
					continue;
				/*
				 * This assignment here is why `LapSlot` can't be struct.
				 * Struct values are copied by value, and updates on this variable don't affect the struct in the list.
				*/
				LapSlot slot = _slots[i];
				LapSlot previousSlot = _slots[i - 1];
				/*
				 * For the slot right above the current lap slot (at i=0), fetch the stored values via
				 * `GetLapTime()` and `GetPerfectLap()` instead of the live values being updated at the slot.
				 * The live values can have differences of few hundredths at the moment this method is running.
				 */
				if (i == 1)
				{
					slot.Value.text = FloatToTime.Convert(TargetShip.GetLapTime(_currentLap), TimeFormat);
					slot.PerfectLap = TargetShip.GetPerfectLap(_currentLap);
					// Reset the current lap slot.
					previousSlot.Value.text = TimeFormat;
					previousSlot.PerfectLap = true;
				}
				// Otherwise, fetch the values from the slot below.
				else
				{
					slot.Value.text = previousSlot.Value.text;
					slot.PerfectLap = previousSlot.PerfectLap;
				}
			}
		}

		private void UpdateTotalTime() =>
			_panel.Value.text = _bigTimeTextBuilder.ToString(TargetShip.TotalRaceTime);

		private void UpdateCurrentLapTime()
		{
			// Let's not show the timer until the first lap starts.
			if (_currentLap > _totalLaps || _currentLap == 0)
				return;

			_currentSlot.Value.text =
				FloatToTime.Convert(TargetShip.CurrentLapTime, TimeFormat);
			_currentSlot.PerfectLap = TargetShip.IsPerfectLap;
		}

		private void UpdateProgressBar()
		{
			// The reason of skipping the 0th section is written in
			// `Streamliner.Panel.GetPassingSectionIndex`.
			if (
				TargetShip.CurrentSection is null ||
				TargetShip.CurrentSection.index - 1 == 0
				)
				return;
			_panel.Fill(GetRaceCompletionRate(TargetShip, _currentLap, _totalSections));
		}
	}

	public class LapTimer : CustomScaleScriptableHud
	{
		private BasicPanel _panel;
		private Text _bestTimeText;
		private int _totalSections;

		private readonly BigTimeTextBuilder _bigTimeTextBuilder = new(new StringBuilder());

		private bool _usingBestTimeDisplay;
		private bool _initiated;
		private float _currentTime;
		private float _bestTime;
		private bool _lapInvalidated;
		private bool _bestTimeIsUpAtLapUpdate;

		public override void Start()
		{
			base.Start();
			_totalSections = GetTotalSectionCount();
			_panel = new BasicPanel(CustomComponents.GetById("Base"));
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);
			RectTransform bestTimeSlot = CustomComponents.GetById("LapSlot");
			_bestTimeText = bestTimeSlot.Find("Time").GetComponent<Text>();
			bestTimeSlot.Find("PerfectLine").gameObject.SetActive(false);

			_usingBestTimeDisplay = OptionBestTime != 0;
			_bestTimeText.gameObject.SetActive(_usingBestTimeDisplay);

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame)
				_bestTimeText.color = GetTintColor(TextAlpha.NineTenths);
			else
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_panel.UpdateColor(GetTintFromColor(color: engineColor));
				_bestTimeText.color = GetTintFromColor(TextAlpha.NineTenths, engineColor);
			}

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			UpdateBestTime();
			NgRaceEvents.OnShipLapUpdate += UpdateBestTimeOnLapUpdate;
			if (_usingBestTimeDisplay)
			{
				SetBestTime(TargetShip);
				NgRaceEvents.OnShipLapUpdate += SetBestTime;
			}
			SetCurrentTime();
			NgUiEvents.OnGamemodeUpdateCurrentLapTime += UpdateCurrentTime;
			NgUiEvents.OnGamemodeInvalidatedLap += InvalidateLap;

			_initiated = true;
			NgRaceEvents.OnCountdownStart -= Initiate;
		}

		public override void Update()
		{
			base.Update();
			if (!_initiated || !TargetShip)
				return;

			UpdateBestTime();
			SetCurrentTime();
		}

		private void UpdateCurrentTime(float currentTime)
		{
			_currentTime = currentTime;
			_lapInvalidated = false;
		}

		private void InvalidateLap() =>
			_lapInvalidated = true;

		private void UpdateBestTimeOnLapUpdate(ShipController ship)
		{
			if (ship != TargetShip)
				return;

			UpdateBestTime();
			_bestTimeIsUpAtLapUpdate = true;
		}

		private void UpdateBestTime()
		{
			if (_bestTimeIsUpAtLapUpdate)
			{
				_bestTimeIsUpAtLapUpdate = false;
				return;
			}

			_bestTime = TargetShip.LoadedBestLapTime ?
				TargetShip.BestLapTime :
				TargetShip.HasBestLapTime ?
					TargetShip.BestLapTime :
					-1f;
		}

		private void SetBestTime(ShipController ship)
		{
			if (ship != TargetShip)
				return;

			_bestTimeText.text = _bestTime >= 0f ?
				FloatToTime.Convert(_bestTime, TimeFormat) : EmptyTime;
		}

		private void SetCurrentTime()
		{
			if (_lapInvalidated)
			{
				_panel.Value.text = _bigTimeTextBuilder.ToString(-1f);
				_panel.Fill(0f);
				return;
			}

			_panel.Value.text = _bigTimeTextBuilder.ToString(_currentTime);

			if (TargetShip.CurrentSection is null)
				return;

			_panel.Fill(GetLapCompletionRate(TargetShip, _totalSections));
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnShipLapUpdate -= UpdateBestTimeOnLapUpdate;
			if (_usingBestTimeDisplay)
				NgRaceEvents.OnShipLapUpdate -= SetBestTime;
			NgUiEvents.OnGamemodeUpdateCurrentLapTime -= UpdateCurrentTime;
			NgUiEvents.OnGamemodeInvalidatedLap -= InvalidateLap;
		}
	}

	public class TargetTime : CustomScaleScriptableHud
	{
		private const string StringTimeTrial = "Time Trial";
		private const string StringSpeedLap = "Speed Lap";
		private RectTransform _panel;
		private RectTransform _normalDisplay;
		private Text _normalDisplayLabel;
		private Text _normalDisplayValue;
		private DoubleGaugePanel _bigDisplay;

		private readonly BigTimeTextBuilder _bigTimeTextBuilder = new(new StringBuilder());

		private enum TimeType
		{
			Total, Lap
		}
		private TimeType _timeType;
		private void SetTimeType(string gamemodeName)
		{
			_timeType = gamemodeName switch
			{
				StringSpeedLap => TimeType.Lap,
				StringTimeTrial when _isCampaign => TimeType.Total,
				StringTimeTrial => OptionBestTime == 2 ? TimeType.Lap : TimeType.Total,
				_ => OptionBestTime == 2 ? TimeType.Lap : TimeType.Total
			};
		}
		private enum DisplayType
		{
			None, Normal, Big, Both
		}
		private DisplayType _displayType;
		private void SetDisplayType(string gamemodeName)
		{
			/*
			 * Display Setup
			 * TT   -> Normal(Total/Lap) / Both(Total/Lap, Total/Lap Left) / Both(Total, Target Left)
			 * SL   -> None / Big(Lap Left) / Big(Target Left)
			 * Race -> Normal(Total/Lap)
			 * Then if OptionBestTime is set to off, remove Normal.
			 */
			_displayType = gamemodeName switch
			{
				StringSpeedLap => _isCampaign || OptionTargetTimer ? DisplayType.Big : DisplayType.None,
				StringTimeTrial => _isCampaign || OptionTargetTimer ? DisplayType.Both : DisplayType.Normal,
				_ => DisplayType.Normal
			};

			if (OptionBestTime == 0)
			{
				_displayType = _displayType switch
				{
					DisplayType.Both => DisplayType.Big,
					DisplayType.Normal => DisplayType.None,
					_ => _displayType
				};
			}
		}

		private bool _usingBestTimeDisplay;
		private bool _usingLeftTimeDisplay;
		private bool _showingLapTimeAdvantage;
		private string _gamemodeName;
		private bool _isPointToPointTrack;
		private bool _isCampaign;
		private float _bestTime;
		private float _bronzeTarget;
		private float _silverTarget;
		private float _goldTarget;
		private float _platinumTarget;
		private float _targetTime;
		private float _awardTimeDifference;
		private float _currentTime;
		private float _averageLapTimeAdvantage;
		private bool _lapInvalidated;
		private bool _initiated;
		private bool _bestTimeIsUpAtLapUpdate;

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_normalDisplay = CustomComponents.GetById("Normal");
			_normalDisplayLabel = _normalDisplay.Find("Label").GetComponent<Text>();
			_normalDisplayValue = _normalDisplay.Find("Value").GetComponent<Text>();
			_bigDisplay = new DoubleGaugePanel(CustomComponents.GetById("Big"), true);
			_bigDisplay.SetFillStartingSide(DoubleGaugePanel.StartingPoint.Center);

			_gamemodeName = RaceManager.CurrentGamemode.Name;
			_isPointToPointTrack = RaceManager.Instance.PointToPointTrack;
			_isCampaign = NgCampaign.Enabled;
			SetTimeType(_gamemodeName);
			SetDisplayType(_gamemodeName);
			switch (_displayType)
			{
				case DisplayType.None:
					_panel.gameObject.SetActive(false);
					break;
				case DisplayType.Normal:
					_bigDisplay.Base.gameObject.SetActive(false);
					break;
				case DisplayType.Big:
					_normalDisplay.gameObject.SetActive(false);
					break;
				case DisplayType.Both:
				default:
					break;
			}

			if (_displayType == DisplayType.None)
				return;

			_usingBestTimeDisplay =
				OptionBestTime != 0 &&
				_displayType is DisplayType.Normal or DisplayType.Both;
			_usingLeftTimeDisplay =
				_displayType is DisplayType.Big or DisplayType.Both;
			_showingLapTimeAdvantage =
				_gamemodeName == StringTimeTrial && _timeType == TimeType.Lap;

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame)
			{
				_normalDisplayLabel.color = GetTintColor(TextAlpha.ThreeQuarters);
				_normalDisplayValue.color = GetTintColor();
			}
			else
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_bigDisplay.UpdateColor(GetTintFromColor(color: engineColor));
				_normalDisplayLabel.color = GetTintFromColor(TextAlpha.ThreeQuarters, engineColor);
				_normalDisplayValue.color = GetTintFromColor(color: engineColor);
			}

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			UpdateBestTime();
			if (_isCampaign)
			{
				_bronzeTarget = NgCampaign.CurrentEvent.EventProgress.BronzeValue;
				_silverTarget = NgCampaign.CurrentEvent.EventProgress.SilverValue;
				_goldTarget = NgCampaign.CurrentEvent.EventProgress.GoldValue;
				_platinumTarget = NgCampaign.CurrentEvent.EventProgress.PlatinumValue;
			}
			ChangeTargetTime();

			/*
			 * OnShipLapUpdate executes before Update is done.
			 * Two methods above that updates data for methods below,
			 * are executed in Update.
			 *
			 * So I am making an exception and make them update at
			 * OnShipLapUpdate instead.
			 */
			NgRaceEvents.OnShipLapUpdate += UpdateBestAndTargetTimeOnLapUpdate;

			if (_usingBestTimeDisplay)
			{
				SetBestTime(TargetShip);
				NgRaceEvents.OnShipLapUpdate += SetBestTime;
			}

			if (_usingLeftTimeDisplay)
			{
				SetLeftLabel(TargetShip);
				SetLeftTime();

				NgRaceEvents.OnShipLapUpdate += SetLeftLabel;
				if (_showingLapTimeAdvantage)
					NgRaceEvents.OnShipLapUpdate += UpdateAverageLapTimeAdvantage;
				if (_gamemodeName == StringSpeedLap)
				{
					NgUiEvents.OnGamemodeUpdateCurrentLapTime += UpdateSpeedLapCurrentTime;
					NgUiEvents.OnGamemodeInvalidatedLap += InvalidateLap;
				}
			}

			_initiated = true;
			NgRaceEvents.OnCountdownStart -= Initiate;
		}

		public override void Update()
		{
			base.Update();
			if (_displayType == DisplayType.None || !_initiated || !TargetShip)
				return;

			UpdateBestTime();
			ChangeTargetTime();

			if (!_usingLeftTimeDisplay)
				return;

			if (_gamemodeName != StringSpeedLap) UpdateCurrentTime();
			SetLeftTime();
		}

		private void UpdateSpeedLapCurrentTime(float currentTime)
		{
			_currentTime = currentTime;
			_lapInvalidated = false;
		}

		private void UpdateCurrentTime()
		{
			_currentTime = _timeType == TimeType.Total ?
				TargetShip.TotalRaceTime : TargetShip.CurrentLapTime;
		}

		private void InvalidateLap() =>
			_lapInvalidated = true;

		private void UpdateBestAndTargetTimeOnLapUpdate(ShipController ship)
		{
			if (ship != TargetShip)
				return;

			UpdateBestTime();
			ChangeTargetTime();
			_bestTimeIsUpAtLapUpdate = true;
		}

		private void UpdateBestTime()
		{
			if (_bestTimeIsUpAtLapUpdate)
			{
				_bestTimeIsUpAtLapUpdate = false;
				return;
			}

			_bestTime = TargetShip.LoadedBestLapTime switch
			{
				true when _timeType == TimeType.Total =>
					TargetShip.TargetTime,
				true when TargetShip.BestLapTime <= 0f
				          && TargetShip.TargetTime > 0f =>
					TargetShip.TargetTime / (
						_isPointToPointTrack ? 1 : Race.GetBaseLapCountFor(Race.Speedclass)
					),
				true => TargetShip.BestLapTime,
				false => TargetShip.HasBestLapTime ?
					TargetShip.BestLapTime : -1f
			};
		}

		private void ChangeTargetTime()
		{
			if (_bestTimeIsUpAtLapUpdate)
			{
				_bestTimeIsUpAtLapUpdate = false;
				return;
			}

			/*
			 * If best time is not loaded from the start
			 * (the track was never played in current setting before),
			 * _bestTime will update on every faster finished lap.
			 *
			 * If it's loaded from the start,
			 * _bestTime will keep loading from the same value,
			 * effectively staying at it.
			 *
			 * When not loaded and panel is tracking lap time,
			 * updating _targetTime can gives a lap time to chase
			 * from the second lap.
			 *
			 * But if panel is tracking total time,
			 * this makes the panel stuck at 0 as
			 * _currentTime that won't reset on lap update can't be
			 * any smaller than the first lap time.
			 *
			 * When best time is loaded from the start,
			 * _targetTime can be updated in either tracking mode,
			 * which won't change on lap updates
			 * but will put the loaded time to the panel on the first update.
			 */
			if (!_isCampaign)
			{
				if (TargetShip.LoadedBestLapTime || _timeType == TimeType.Lap)
					_targetTime = _bestTime;
				return;
			}

			if ((double) _currentTime <= _platinumTarget)
			{
				_targetTime = _platinumTarget;
				_awardTimeDifference = _platinumTarget;
			}
			else if ((double) _currentTime <= _goldTarget)
			{
				_targetTime = _goldTarget;
				_awardTimeDifference = _goldTarget - _platinumTarget;
			}
			else if ((double) _currentTime <= _silverTarget)
			{
				_targetTime = _silverTarget;
				_awardTimeDifference = _silverTarget - _goldTarget;
			}
			else if ((double) _currentTime <= _bronzeTarget)
			{
				_targetTime = _bronzeTarget;
				_awardTimeDifference = _bronzeTarget - _silverTarget;
			}
		}

		private void SetBestTime(ShipController ship)
		{
			if (ship != TargetShip)
				return;

			_normalDisplayValue.text = _bestTime >= 0f ?
				FloatToTime.Convert(_bestTime, TimeFormat) : EmptyTime;
		}

		private void UpdateAverageLapTimeAdvantage(ShipController ship)
		{
			if (ship != TargetShip || TargetShip.CurrentLap <= 1)
				return;

			if (_bestTime <= 0f)
				return;

			_averageLapTimeAdvantage +=
				_bestTime - TargetShip.GetLapTime(TargetShip.CurrentLap - 1);
		}

		private void SetLeftLabel(ShipController ship)
		{
			if (ship != TargetShip)
				return;

			_bigDisplay.Label.text = _targetTime <= 0f ? "best" : "target";
		}

		private void SetLeftTime()
		{
			if (_currentTime < 0f || _lapInvalidated)
			{
				_bigDisplay.Value.text = _bigTimeTextBuilder.ToStringNoDecimal(-1f);
				_bigDisplay.SmallValue.text = "--";
				_bigDisplay.FillBoth(0f);
				return;
			}
			if (_targetTime <= 0f)
			{
				_bigDisplay.Value.text = _bigTimeTextBuilder.ToStringNoDecimal(_currentTime);
				_bigDisplay.SmallValue.text =
					IntStrDb.GetNoSingleCharNumber(Mathf.FloorToInt(_currentTime * 100f % 100f));
				_bigDisplay.FillBoth(1f);
				return;
			}

			float timeLeft = _targetTime - _currentTime;
			float timeMax = _isCampaign ? _awardTimeDifference : _targetTime;
			if (_showingLapTimeAdvantage)
				timeLeft += _averageLapTimeAdvantage;
			timeLeft = timeLeft < 0f ? 0f : timeLeft;
			_bigDisplay.Value.text = _bigTimeTextBuilder.ToStringNoDecimal(timeLeft);
			_bigDisplay.SmallValue.text =
				IntStrDb.GetNoSingleCharNumber(Mathf.FloorToInt(timeLeft * 100f % 100f));
			timeLeft = timeLeft > _targetTime ? _targetTime : timeLeft;
			_bigDisplay.FillBoth(timeLeft / timeMax);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnShipLapUpdate -= UpdateBestAndTargetTimeOnLapUpdate;
			if (_usingBestTimeDisplay)
				NgRaceEvents.OnShipLapUpdate -= SetBestTime;
			if (_usingLeftTimeDisplay)
				NgRaceEvents.OnShipLapUpdate -= SetLeftLabel;
			switch (_usingLeftTimeDisplay)
			{
				case true when _showingLapTimeAdvantage:
					NgRaceEvents.OnShipLapUpdate -= UpdateAverageLapTimeAdvantage;
					break;
				case true when _gamemodeName == StringSpeedLap:
					NgUiEvents.OnGamemodeUpdateCurrentLapTime -= UpdateSpeedLapCurrentTime;
					NgUiEvents.OnGamemodeInvalidatedLap -= InvalidateLap;
					break;
			}
		}
	}

	public class ZoneTracker : CustomScaleScriptableHud
	{
		private DoubleGaugePanel _panel;
		private Text _zoneName;
		private Text _zoneScore;
		private bool _usingZoneColors;

		public override void Start()
		{
			base.Start();
			_panel = new DoubleGaugePanel(CustomComponents.GetById("Base"));
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);
			_zoneName = CustomComponents.GetById<Text>("Name");
			_zoneName.gameObject.SetActive(true);
			_zoneScore = CustomComponents.GetById<Text>("Score");
			_zoneScore.gameObject.SetActive(true);

			_usingZoneColors = OptionZoneTintOverride;

			/*
			 * When `OnZoneNumberUpdate` is called,
			 * publicly accessible `ZonePalleteSettings.CurrentColors` is NOT updated!
			 * So the palette settings should be manually fetched to get the next set.
			 */
			if (_usingZoneColors)
				UpdateZonePalleteSettings();

			SetScore("0");
			SetNumber("0");
			SetTitle("toxic");

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame || _usingZoneColors)
				ChangeModeSpecificPartsColor();
			else
				UpdateColor(GetShipRepresentativeColor(TargetShip));

			NgUiEvents.OnZoneProgressUpdate += SetProgress;
			NgUiEvents.OnZoneScoreUpdate += SetScore;
			NgUiEvents.OnZoneNumberUpdate += SetNumber;
			NgUiEvents.OnZoneTitleUpdate += SetTitle;
		}

		private void ChangeModeSpecificPartsColor()
		{
			_zoneName.color = GetTintColor(TextAlpha.ThreeEighths);
			_zoneScore.color = GetTintColor(TextAlpha.NineTenths);
		}
		private void ChangeModeSpecificPartsColor(Color color)
		{
			_zoneName.color = GetTintFromColor(TextAlpha.ThreeEighths, color);
			_zoneScore.color = GetTintFromColor(TextAlpha.NineTenths, color);
		}

		private void UpdateColor(Color color)
		{
			_panel.UpdateColor(GetTintFromColor(color: color));
			ChangeModeSpecificPartsColor(color);
		}

		private void UpdateToZoneColor(int zoneNumber) =>
			UpdateColor(GetZoneColor(zoneNumber));

		private void SetProgress(float progress) =>
			_panel.FillBoth(progress);

		private void SetScore(string score) =>
			_zoneScore.text = score;

		private void SetNumber(string number)
		{
			_panel.Value.text = number;
			int zoneNumber = Convert.ToInt32(number);

			if (_usingZoneColors && zoneNumber % 5 == 0)
				UpdateToZoneColor(zoneNumber);
		}

		private void SetTitle(string title) =>
			_zoneName.text = title;

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (OptionZoneTintOverride)
				FlushZonePalleteSettings();

			NgUiEvents.OnZoneProgressUpdate -= SetProgress;
			NgUiEvents.OnZoneScoreUpdate -= SetScore;
			NgUiEvents.OnZoneNumberUpdate -= SetNumber;
			NgUiEvents.OnZoneTitleUpdate -= SetTitle;
		}
	}

	public class UpsurgeTracker : CustomScaleScriptableHud
	{
		private LayeredDoubleGaugePanel _panel;
		private RectTransform _energyInfo;
		private Text _labelZoneText;
		private Text _labelShieldText;
		private Text _valueZoneText;
		private Text _valueShieldText;
		private Animator _barrierWarning;
		private Color _currentZoneColor;
		private static readonly int WarnLeft = Animator.StringToHash("Left");
		private static readonly int WarnMiddle = Animator.StringToHash("Middle");
		private static readonly int WarnRight = Animator.StringToHash("Right");
		private GmUpsurge _gamemode;
		private UpsurgeShip _upsurgeTargetShip;
		private bool _isPlayerOne;
		private int _valueShield;
		private float _valueZoneTime;
		private const float TransitionSpeed = 8f;
		private const float TransitionTimerMax = 1.5f;
		private float _transitionTimer;
		private float _smallGaugeAlpha;
		private Color _overflowZoneTimeColor;
		private Color _currentSmallGaugeColor;
		private float _overflowTransitionAlpha;
		private bool _valuesAreFinite = true;
		private bool _playingOverflowTransition;
		private float _finiteZoneTimeWidth;
		private float _finiteZoneWidth;
		private float _finiteShieldWidth;
		private float _currentZoneTimeWidth;
		private float _currentZoneWidth;
		private float _currentShieldWidth;
		private bool _usingZoneColors;

		public override void Start()
		{
			base.Start();
			_panel = new LayeredDoubleGaugePanel(CustomComponents.GetById("Base"), true);
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);
			_energyInfo = CustomComponents.GetById("Energy");
			_energyInfo.gameObject.SetActive(true);
			_labelZoneText = _energyInfo.Find("LabelZone").GetComponent<Text>();
			_labelShieldText = _energyInfo.Find("LabelShield").GetComponent<Text>();
			_valueZoneText = _energyInfo.Find("ValueZone").GetComponent<Text>();
			_valueShieldText = _energyInfo.Find("ValueShield").GetComponent<Text>();
			_barrierWarning = CustomComponents.GetById<Animator>("Barrier");
			_barrierWarning.gameObject.SetActive(true);

			_overflowZoneTimeColor = GetTintColor(tintIndex: 2, clarity: 5);
			_currentSmallGaugeColor = _panel.SmallGaugeColor;
			_smallGaugeAlpha = _panel.SmallGaugeColor.a;

			_usingZoneColors = OptionZoneTintOverride;

			_isPlayerOne = TargetShip.playerIndex == 0;

			_gamemode = (GmUpsurge) RaceManager.CurrentGamemode;
			if (_usingZoneColors && _isPlayerOne)
				UpdateZonePalleteSettings(_gamemode);

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame || _usingZoneColors)
				ChangeModeSpecificPartsColor();
			else
				UpdateColor(GetShipRepresentativeColor(TargetShip));

			UpsurgeShip.OnDeployedBarrier += StartTransition;
			UpsurgeShip.OnBuiltBoostStepsIncrease += StartTransition;
			UpsurgeShip.OnShieldActivated += StartTransition;
			NgRaceEvents.OnShipScoreChanged += UpdateToZoneColor;
			Barrier.OnPlayerBarrierWarned += WarnBarrier;
		}

		private void ChangeModeSpecificPartsColor()
		{
			Color infoLabelColor = GetTintColor(TextAlpha.ThreeEighths);
			Color infoValueColor = GetTintColor(TextAlpha.ThreeQuarters);
			_labelZoneText.color = infoLabelColor;
			_labelShieldText.color = infoLabelColor;
			_valueZoneText.color = infoValueColor;
			_valueShieldText.color = infoValueColor;
		}
		private void ChangeModeSpecificPartsColor(Color color)
		{
			Color infoLabelColor = GetTintFromColor(TextAlpha.ThreeEighths, color);
			Color infoValueColor = GetTintFromColor(TextAlpha.ThreeQuarters, color);
			_labelZoneText.color = infoLabelColor;
			_labelShieldText.color = infoLabelColor;
			_valueZoneText.color = infoValueColor;
			_valueShieldText.color = infoValueColor;
		}

		private void StartTransition(ShipController ship)
		{
			if (ship != _upsurgeTargetShip?.TargetShip)
				return;

			_transitionTimer = TransitionTimerMax;
			_valuesAreFinite = false;
			StartCoroutine(ResetZoneTime());
		}

		private void WarnBarrier(
			ShipController ship, Barrier barrier, int side
		)
		{
			if (Gameplay.MirrorEnabled)
				side *= -1;

			switch (side)
			{
				case -1:
					_barrierWarning.SetTrigger(WarnLeft);
					break;
				case 0:
					_barrierWarning.SetTrigger(WarnMiddle);
					break;
				case 1:
					_barrierWarning.SetTrigger(WarnRight);
					break;
			}
		}

		private void UpdateValues()
		{
			if (_upsurgeTargetShip == null)
				return;

			_valueShield = _upsurgeTargetShip.BuiltZones * 20;
			_valueShield = _valueShield < 0 ? 0 : _valueShield > 100 ? 100 : _valueShield;
			_valueZoneTime = _upsurgeTargetShip.ZoneTime;
			_finiteZoneTimeWidth = _upsurgeTargetShip.ZoneTime / 5;
			_finiteZoneWidth = (float) _upsurgeTargetShip.BuiltZones / 10;
			_finiteShieldWidth = (float) _valueShield / 100;
		}

		private void SetValues()
		{
			_panel.Value.text = _upsurgeTargetShip.CurrentZone.ToString();
			_valueZoneText.text = "+" + _upsurgeTargetShip.BuiltZones;
			_valueShieldText.text = "+" + _valueShield;

			_panel.FillBoth(_currentZoneWidth);
			_panel.FillSecondGauges(_currentShieldWidth);
			_panel.FillSmallGauges(_currentZoneTimeWidth);
			_panel.ChangeSmallGaugesColor(_currentSmallGaugeColor);
		}

		private IEnumerator ResetZoneTime()
		{
			_playingOverflowTransition = true;
			NgSound.PlayOneShot(NgSound.Ui_UpsurgeBoostChargeReset, EAudioChannel.Interface, 1f, 1f);
			_currentZoneTimeWidth = 1f;
			_currentSmallGaugeColor = _overflowZoneTimeColor;
			_overflowTransitionAlpha = _smallGaugeAlpha;
			float t = 0.5f;
			while (t > 0f)
			{
				_overflowTransitionAlpha = Mathf.Lerp(
					_overflowTransitionAlpha, 0f,
					Time.deltaTime * TransitionSpeed * 2.5f
				);
				_currentSmallGaugeColor.a = _overflowTransitionAlpha;

				t -= Time.deltaTime;
				yield return null;
			}
			_currentSmallGaugeColor = _panel.SmallGaugeColor;
			_currentZoneTimeWidth = 0f;
			_playingOverflowTransition = false;
		}

		private void UpdateColor(Color color)
		{
			_panel.UpdateColor(GetTintFromColor(color: color));
			// instead of doing `UpdateSmallGaugesColor()`, just update the field
			_panel.SmallGaugeColor = GetTintFromColor(color: color, clarity: 1);
			ChangeModeSpecificPartsColor(color);
		}

		private void UpdateToZoneColor(ShipController ship, float oldScore, float newScore)
		{
			if (!_usingZoneColors || ship != TargetShip)
				return;

			Color currentZoneColor = GetZoneColor((int) newScore);

			if (_currentZoneColor == currentZoneColor)
				return;

			_currentZoneColor = currentZoneColor;
			UpdateColor(currentZoneColor);
		}

		public override void Update()
		{
			base.Update();
			if (_upsurgeTargetShip == null)
			{
				_upsurgeTargetShip = _gamemode.Ships.Find(ship => ship.TargetShip == TargetShip);
				if (OptionZoneTintOverride)
					UpdateToZoneColor(TargetShip, 0f, 0f);
				return;
			}

			if (
				!Mathf.Approximately(_valueZoneTime, _upsurgeTargetShip.ZoneTime) &&
				_valuesAreFinite
			)
			{
				_transitionTimer = TransitionTimerMax;
				_valuesAreFinite = false;
			}

			UpdateValues();

			if (_transitionTimer > 0f)
			{
				if (
					!Mathf.Approximately(_currentZoneTimeWidth, _finiteZoneTimeWidth) &&
					!_playingOverflowTransition
				)
				{
					_overflowTransitionAlpha = _smallGaugeAlpha;
					_currentZoneTimeWidth = Mathf.Lerp(
						_currentZoneTimeWidth, _finiteZoneTimeWidth,
						Time.deltaTime * TransitionSpeed
					);
				}
				if (!Mathf.Approximately(_currentZoneWidth, _finiteZoneWidth))
				{
					_currentZoneWidth = Mathf.Lerp(
						_currentZoneWidth, _finiteZoneWidth,
						Time.deltaTime * TransitionSpeed
					);
				}
				if (!Mathf.Approximately(_currentShieldWidth, _finiteShieldWidth))
				{
					_currentShieldWidth = Mathf.Lerp(
						_currentShieldWidth, _finiteShieldWidth,
						Time.deltaTime * TransitionSpeed
					);
				}
				_transitionTimer -= Time.deltaTime;
			}
			else if (!_valuesAreFinite)
			{
				_currentZoneTimeWidth = _finiteZoneTimeWidth;
				_currentZoneWidth = _finiteZoneWidth;
				_currentShieldWidth = _finiteShieldWidth;

				_valuesAreFinite = true;
				_transitionTimer = 0f;
			}

			SetValues();
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (OptionZoneTintOverride && _isPlayerOne)
				FlushZonePalleteSettings();

			UpsurgeShip.OnDeployedBarrier -= StartTransition;
			UpsurgeShip.OnBuiltBoostStepsIncrease -= StartTransition;
			UpsurgeShip.OnShieldActivated -= StartTransition;
			NgRaceEvents.OnShipScoreChanged -= UpdateToZoneColor;
			Barrier.OnPlayerBarrierWarned -= WarnBarrier;
		}
	}

	public class Placement : CustomScaleScriptableHud
	{
		private FractionPanel _panel;
		private bool _warnOnLastPlace;
		private bool _onWarning;
		private readonly Color _highlightColor = GetTintColor(tintIndex: 1, clarity: 4);
		private float _warningTimer;
		private float _warningSin;
		private bool _playedWarningSound;

		public override void Start()
		{
			base.Start();
			_panel = new FractionPanel(CustomComponents.GetById("Base"));
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);
			switch (RaceManager.CurrentGamemode.Name)
			{
				case "Knockout":
				case "Rush Hour":
					_warnOnLastPlace = true;
					break;
				default:
					_warnOnLastPlace = false;
					break;
			}

			if (OptionValueTint == OptionValueTintShipEngineIndexForGame)
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_panel.UpdateColor(GetTintFromColor(color: engineColor));
			}

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			StartCoroutine(UpdateData());
			NgRaceEvents.OnCountdownStart -= Initiate;
		}

		private IEnumerator UpdateData()
		{
			while (true)
			{
				int place = TargetShip.CurrentPlace;
				int maxPlace = Ships.Active.Count;
				_onWarning = _warnOnLastPlace && place == maxPlace;
				_panel.Value.text = IntStrDb.GetNoSingleCharNumber(place);
				_panel.MaxValue.text = IntStrDb.GetNoSingleCharNumber(maxPlace);
				_panel.Fill(maxPlace == 1 ?
					0f : (float) (maxPlace - place) / (maxPlace - 1));
				_panel.ChangeDataPartColor(_onWarning ? _highlightColor : _panel.GaugeColor);

				yield return new WaitForSeconds(Position.UpdateTime);
			}
		}

		public override void Update()
		{
			base.Update();
			/*
			 * I wanted to just let it play at every fixed interval,
			 * but then I couldn't get the exact length of the interval:
			 * I don't know the length of the audio file,
			 * so I can't decide if the time the sound is done and the sin goes below 0.1
			 * would be always finite.
			 * This is how I think it's done in the default hud.
			 */
			if (!_warnOnLastPlace)
				return;

			_warningTimer += Time.deltaTime * 3f;
			_warningSin = Mathf.Abs(Mathf.Sin(_warningTimer));

			if (_warningSin > Position.UpdateTime)
				_playedWarningSound = false;
			if (
				!_onWarning ||
				_warningSin >= Position.UpdateTime ||
				_playedWarningSound ||
				!Race.HasCountdownFinished ||
				TargetShip.Eliminated ||
				Ships.Active.Count <= 1
			)
				return;
			NgSound.PlayOneShot(NgSound.UI_KnockoutWarning, EAudioChannel.Interface, 1f, 1f);
			_playedWarningSound = true;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			StopCoroutine(UpdateData());
		}
	}

	public class LapCounter : CustomScaleScriptableHud
	{
		private FractionPanel _panel;

		public override void Start()
		{
			base.Start();
			_panel = new FractionPanel(CustomComponents.GetById("Base"))
			{
				Value = { text = IntStrDb.GetNoSingleCharNumber(0) },
				MaxValue = { text = IntStrDb.GetNoSingleCharNumber(Race.MaxLaps) }
			};
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);

			if (OptionValueTint == OptionValueTintShipEngineIndexForGame)
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_panel.UpdateColor(GetTintFromColor(color: engineColor));
			}

			NgRaceEvents.OnShipLapUpdate += UpdateLap;
		}

		private void UpdateLap(ShipController ship)
		{
			if (ship != TargetShip)
				return;

			base.Update();
			_panel.Value.text = IntStrDb.GetNoSingleCharNumber(TargetShip.CurrentLap);
			_panel.Fill((float) TargetShip.CurrentLap / Race.MaxLaps);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnShipLapUpdate -= UpdateLap;
		}
	}

	public class PositionTracker : CustomScaleScriptableHud
	{
		private const float AlphaEliminated = 0.5f;
		private static int _totalSections;
		// A ship 16 sections away is barely visible in horizontal splitscreen.
		private const int MinimumEndDistance = 16 * 5;
		private static EPosHudMode _previousMode;
		private bool _canShipRespawn;
		private bool _initiated;
		private bool _modeChanged;

		// for game modes that don't count laps.
		private static bool _manuallyCountLaps;

		private RectTransform _panel;
		private RectTransform _nodeTemplate;
		private ShipNode _singleNode;
		private List<ShipNode> _nodes;
		private static int[] _racerSectionsTraversed;
		private List<RacerRelativeSectionData> _racerRelativeSections;

		private bool _isPlayerOne;
		private static bool _sectionsTraversedUpdated;

		private class ShipNode
		{
			internal static float MaxSize;
			// speed of 4 makes it nearly in sync with the placement component
			private const int TransitionSpeed = 4;
			internal int Id;
			private readonly RectTransform _node;
			private readonly Image _nodeImage;
			private float _currentPositionRate;
			private Vector2 _position;

			// for game modes that don't count laps.
			internal int ManuallyCountedCurrentLap;
			internal bool ManuallySetLapValidated;
			internal int? ManuallySetMiddleSection;

			public int SiblingIndex
			{
				set => _node.SetSiblingIndex(value);
			}

			public void SetPosition(float rate, bool forceUpdate = false)
			{
				rate = rate < 0f ? 0f : rate > 1f ? 1f : rate;

				if (!Mathf.Approximately(_currentPositionRate, rate))
					_currentPositionRate = forceUpdate ?
						rate :
						Mathf.Lerp(_currentPositionRate, rate, Time.deltaTime * TransitionSpeed);

				_position.x = _currentPositionRate * MaxSize;
				_node.anchoredPosition = _position;
			}

			/*public float GetPosition() => _position.x;*/

			public float Alpha
			{
				set
				{
					Color color = _nodeImage.color with
					{
						a = value
					};
					_nodeImage.color = color;
				}
			}

			public bool Enabled
			{
				set => _nodeImage.enabled = value;
			}

			public ShipNode(RectTransform template, Color color, int id = -1)
			{
				Id = id;
				_node = Instantiate(template, template.parent);
				_node.localScale = template.localScale;
				_node.anchoredPosition = template.anchoredPosition;
				_nodeImage = _node.GetComponent<Image>();
				_nodeImage.color = color;
				SetPosition(0.5f, true);
			}
		}

		private class RacerRelativeSectionData
		{
			public readonly int Id;
			public int Value;

			public RacerRelativeSectionData(int id, int value)
			{
				Id = id;
				Value = value;
			}
		}

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_panel.Find("BackgroundFill").GetComponent<Image>().color =
				OptionValueTint != OptionValueTintShipEngineIndexForGame ?
					GetTintColor(TextAlpha.ThreeEighths) :
					GetTintFromColor(TextAlpha.ThreeEighths, GetShipRepresentativeColor(TargetShip));

			_nodeTemplate = CustomComponents.GetById("Node");
			ShipNode.MaxSize = _panel.sizeDelta.x - _nodeTemplate.sizeDelta.x;

			_isPlayerOne = TargetShip.playerIndex == 0;

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			_canShipRespawn = RaceManager.CurrentGamemode.CanShipsRespawn();
			_manuallyCountLaps = RaceManager.CurrentGamemode.Name switch
			{
				"Eliminator" => true,
				_ => false
			};

			_singleNode = new ShipNode(_nodeTemplate, GetTintColor(tintIndex: 2, clarity: 1));

			int totalShips = Ships.Loaded.Count;
			_nodes = new List<ShipNode>(totalShips);
			if (_isPlayerOne) _racerSectionsTraversed = new int[totalShips];
			_racerRelativeSections = new List<RacerRelativeSectionData>(totalShips);

			foreach (ShipController ship in Ships.Loaded)
			{
				if (ship == TargetShip)
					_nodes.Add(new ShipNode(
						_nodeTemplate,
						OptionValueTint != OptionValueTintShipEngineIndexForGame ?
							GetTintColor(clarity: 1) :
							GetTintFromColor(color: GetShipRepresentativeColor(ship), clarity: 1),
						ship.ShipId
					));
				else
					_nodes.Add(new ShipNode(
						_nodeTemplate,
						GetShipRepresentativeColor(ship),
						ship.ShipId
					));

				if (_isPlayerOne) _racerSectionsTraversed[ship.ShipId] = 0;
				_racerRelativeSections.Add(new RacerRelativeSectionData(ship.ShipId, 0));
			}

			/*
			 * after nodes for every ship is instantiated,
			 * move the template to the last, THEN
			 * move the player node to last before template, THEN
			 * move the single node before the player node.
			 *
			 * Total node count is Ships.Loaded.Count + 2 because
			 * every ship from the race plus the single node
			 * and the template exist.
			 */
			_nodeTemplate.SetSiblingIndex(totalShips + 1);
			_nodeTemplate.gameObject.SetActive(false);
			_nodes[TargetShip.ShipId].SiblingIndex = totalShips;
			_singleNode.SiblingIndex = totalShips - 1;
			if (_isPlayerOne)
				_totalSections = GetTotalSectionCount();

			StartCoroutine(UpdateSectionsTraversed());

			_initiated = true;
			NgRaceEvents.OnCountdownStart -= Initiate;

			if (_manuallyCountLaps)
			{
				/*
				 * When passing the validation gate,
				 * MidLineReset and then MidLine is triggered.
				 * When trying to pass it in the reverse direction,
				 * both are called in the reversed order as well.
				 */
				NgRaceEvents.OnStartLineTriggered += LimitNodeToFirstHalf;
				NgRaceEvents.OnMidLineResetTriggered += ResetValidationState;
				NgRaceEvents.OnMidLineTriggered += ReleaseNodeBeyondFirstHalf;
			}
		}

		public override void Update()
		{
			base.Update();
			if (!TargetShip || !_initiated)
				return;

			if (_modeChanged)
				UpdateMode();
			if (_previousMode != Hud.PositionTrackerHudMode)
			{
				if (_isPlayerOne) _previousMode = Hud.PositionTrackerHudMode;
				_modeChanged = true;
			}

			SetNodes();
		}

		private void UpdateMode()
		{
			_modeChanged = false;
			switch (Hud.PositionTrackerHudMode)
			{
				case EPosHudMode.Multiple:
					_singleNode.Enabled = false;
					foreach (ShipNode node in _nodes)
						node.Enabled = true;
					break;
				case EPosHudMode.Single:
				default:
					_singleNode.Enabled = true;
					foreach (ShipNode node in _nodes)
					{
						if (node.Id == TargetShip.ShipId)
							continue;
						node.Enabled = false;
					}
					break;
			}
		}

		private IEnumerator UpdateSectionsTraversed()
		{
			while (true)
			{
				if (_isPlayerOne)
				{
					for (int id = 0; id < _racerSectionsTraversed.Length; id++)
					{
						ShipController ship = Ships.Loaded[id];
						if (
							!ship.CurrentSection ||
							ship.CurrentSection.index - 1 == 0
						)
							continue;

						_racerSectionsTraversed[id] =
							GetPassingSectionIndex(
								ship,
								_manuallyCountLaps ?
									_nodes[id].ManuallyCountedCurrentLap : ship.CurrentLap,
								_totalSections
							);
						if (
							_manuallyCountLaps &&
							_nodes[id].ManuallySetMiddleSection is not null &&
							ship.CurrentSection.index >= _nodes[id].ManuallySetMiddleSection &&
							!_nodes[id].ManuallySetLapValidated
						)
						{
							_racerSectionsTraversed[id] -= _totalSections;
						}
					}
					_sectionsTraversedUpdated = true;
				}

				yield return new WaitUntil(() => _sectionsTraversedUpdated);

				int playerSection = _racerSectionsTraversed[TargetShip.ShipId];
				for (int id = 0; id < _racerSectionsTraversed.Length; id++)
				{
					_racerRelativeSections[id].Value =
						_racerSectionsTraversed[id] - playerSection;
				}

				if (_isPlayerOne) _sectionsTraversedUpdated = false;

				yield return new WaitForSeconds(Position.UpdateTime);
			}
		}

		private void LimitNodeToFirstHalf(ShipController ship)
		{
			if (
				!_nodes[ship.ShipId].ManuallySetLapValidated &&
				_nodes[ship.ShipId].ManuallyCountedCurrentLap > 0
			)
				return;

			_nodes[ship.ShipId].ManuallyCountedCurrentLap++;
			_nodes[ship.ShipId].ManuallySetLapValidated = false;
		}

		private void ResetValidationState(ShipController ship)
		{
			_nodes[ship.ShipId].ManuallySetLapValidated = false;
		}

		private void ReleaseNodeBeyondFirstHalf(ShipController ship)
		{
			_nodes[ship.ShipId].ManuallySetLapValidated = true;
			_nodes[ship.ShipId].ManuallySetMiddleSection = ship.CurrentSection.index;
		}

		private void SetNodes()
		{
			switch (Hud.PositionTrackerHudMode)
			{
				case EPosHudMode.Multiple:
					SetMultipleNodes();
					break;
				case EPosHudMode.Single:
				default:
					SetSingleNode();
					break;
			}
	}

		private void SetSingleNode()
		{
			int previousId = _singleNode.Id;
			_singleNode.Id =
				Ships.FindShipInPlace(TargetShip.CurrentPlace == 1 ? 2 : 1).ShipId;

			_singleNode.SetPosition(ConvertDistanceRate(
				(float) _racerRelativeSections[_singleNode.Id].Value / MinimumEndDistance
			), previousId != _singleNode.Id);
		}

		private void SetMultipleNodes()
		{
			List<RacerRelativeSectionData> orderedList =
				_racerRelativeSections.OrderByDescending(p => p.Value).ToList();
			int endDistance;

			if (
				Ships.Active.Count <= 2 ||
				orderedList.Count <= 2
			)
				endDistance = MinimumEndDistance;
			else
			{
				int indexFirstShipAlive = 0;
				int indexLastShipAlive = orderedList.Count - 1;

				while (Ships.Loaded[orderedList[indexFirstShipAlive].Id].Eliminated)
					if (++indexFirstShipAlive >= orderedList.Count)
					{
						indexFirstShipAlive = -1;
						break;
					}

				while (Ships.Loaded[orderedList[indexLastShipAlive].Id].Eliminated)
					if (--indexLastShipAlive < 0)
					{
						indexLastShipAlive = -1;
						break;
					}

				endDistance = Math.Max(
					indexFirstShipAlive >= 0 ?
						orderedList[indexFirstShipAlive].Value :
						1, // Value >= 0
					indexLastShipAlive >= 0 ?
						Math.Abs(orderedList[indexLastShipAlive].Value) :
						1 // Value <= 0
				);
				endDistance = endDistance < MinimumEndDistance ? MinimumEndDistance : endDistance;
			}

			int siblingIndex = 0;
			bool siblingIndexUpdateFromTop = true;
			foreach (RacerRelativeSectionData p in orderedList)
			{
				/*
				 * Assign from the top position, starting from top index,
				 * then if the loop hits the player, skip the player index,
				 * then continue assigning on next position, but
				 * starting from the bottom index.
				 *
				 * This makes it so the closer other racers are to the player,
				 * the later their position is drawn overlapping any early ones,
				 * regardless if they're ahead or behind of you.
				 *
				 * For example, in an 8-player race where the player is at 5th,
				 * siblingIndex from the first positioned player to the last
				 * would be this: 0, 1, 2, 3, 8, 6, 5, 4.
				 * 7 is the index of the single node.
				 */
				if (p.Id == TargetShip.ShipId)
				{
					siblingIndexUpdateFromTop = false;
					siblingIndex = orderedList.Count - 1;
					continue;
				}

				_nodes[p.Id].Alpha = Ships.Loaded[p.Id].Eliminated ?
					_canShipRespawn ? AlphaEliminated : 0f : 1f;
				/*
				 * When SetSiblingIndex is used for a child in the middle,
				 * children after the old index to and including the new index
				 * will be pushed away to make the new index empty, then
				 * the one child will occupy the index.
				 *
				 * It's probably safe to order them from the end.
				 *
				 * example:
				 * node:         0 1 2 3 4 5 6 7
				 * siblingIndex: 0 1 2 3 4 5 6 7
				 * > node[0].SetSiblingIndex(5)
				 * siblingIndex: 5 0 1 2 3 4 6 7
				 * node ordered by sI: 1 2 3 4 5 0 6 7
				 */
				_nodes[p.Id].SiblingIndex = siblingIndexUpdateFromTop ?
					siblingIndex++ : --siblingIndex;

				_nodes[p.Id].SetPosition(ConvertDistanceRate(
					(float) p.Value / endDistance
				));
			}
		}

		private static float ConvertDistanceRate(float distanceRate) =>
			(float) (
				(
					Math.Sin(
						(distanceRate > 1f ? 1f :
							distanceRate < -1f ? -1f : distanceRate)
						* Math.PI / 2
					) + 1
				) / 2
			);

		/*private string Dump()
		{
			StringBuilder[] sb = { new(), new(), new(), new(), new() };
			sb[0].AppendLine($"Relative Sections for {(_isPlayerOne ? "P1" : "P2")}: ");
			foreach (RacerRelativeSectionData d in _racerRelativeSections)
			{
				sb[1].Append($" {d.Id}");
				sb[2].Append($" {d.Value}");
				sb[3].Append($" {_nodes[d.Id].GetPosition()}");
				sb[4].Append($" {Ships.Loaded[d.Id].ShipName}");
			}
			for (int i = 1; i < sb.Length; i++)
			{
				sb[0].AppendLine(sb[i].ToString());
			}
			return sb[0].ToString();
		}*/

		public override void OnDestroy()
		{
			base.OnDestroy();
			StopCoroutine(UpdateSectionsTraversed());
			if (_manuallyCountLaps)
			{
				NgRaceEvents.OnStartLineTriggered -= LimitNodeToFirstHalf;
				NgRaceEvents.OnMidLineResetTriggered -= ResetValidationState;
				NgRaceEvents.OnMidLineTriggered -= ReleaseNodeBeyondFirstHalf;
			}
		}
	}

	public class Pitlane : CustomScaleScriptableHud
	{
		private RectTransform _panel;
		private Animator _panelAnimator;
		private static readonly int Active = Animator.StringToHash("Active");
		private static readonly int PointRight = Animator.StringToHash("Point Right");

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_panelAnimator = _panel.GetComponent<Animator>();

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame)
			{
				_panel.Find("Left").Find("Text").GetComponent<Text>().color =
					GetTintColor();
				_panel.Find("Right").Find("Text").GetComponent<Text>().color =
					GetTintColor();
			}
			else
			{
				_panel.Find("Left").Find("Text").GetComponent<Text>().color =
					GetTintFromColor(color: GetShipRepresentativeColor(TargetShip));
				_panel.Find("Right").Find("Text").GetComponent<Text>().color =
					GetTintFromColor(color: GetShipRepresentativeColor(TargetShip));
			}

			/*
			 * Using `SetActive()` is ineffective here,
			 * so instead I made an empty animation for the default state.
			 * Now the components won't show up at start
			 * even if I use `SetActive(true)`.
			 */

			PitlaneIndicator.OnPitlaneIndicatorTriggered += Play;
		}

		private void Play(ShipController ship, int side)
		{
			if (ship != TargetShip || side != -1 && side != 1)
				return;

			if (Gameplay.MirrorEnabled)
				side *= -1;

			switch (side)
			{
				case -1:
					_panelAnimator.SetBool(PointRight, false);
					break;
				case 1:
					_panelAnimator.SetBool(PointRight, true);
					break;
			}

			_panelAnimator.SetTrigger(Active);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			PitlaneIndicator.OnPitlaneIndicatorTriggered -= Play;
		}
	}

	public class MessageLogger : CustomScaleScriptableHud
	{
		private RectTransform _panel;
		private CanvasGroup _timeGroup;
		private Text _timeDiff;
		private Text _lapResult;
		private Text _finalLap;
		private RectTransform _lineTemplate;
		private Text _nowPlaying;
		private Text _wrongWay;
		private Color? _currentZoneColor = null;
		private bool _initiated;

		private const int LineMax = 3;
		private const float LapResultLineHeight = 22f; // at hud font size 15
		private const float DefaultDisplayTime = 3.0f;
		private const int FadeOutSpeed = 9;
		private const float FadeOutTimeMax = 1.2f;
		private const int WrongWayFadeSpeed = 13;
		private const float WrongWayFadeTimeMax = 0.8f;
		private float _wrongWayAlpha;
		private float _wrongWayCurrentAlpha;
		private Color _wrongWayCurrentColor;

		/*
		 * First three values of `StringKind` are in the same order
		 * the three elements of `DefinedStrings` are,
		 * so the comparison method `GetStringKind` can do the task
		 * with a for loop and give the corresponding enum value straight from the int.
		 */
		private enum StringKind
		{
			NewLapRecord, PerfectLap, FinalLap, TimeDiff, Time, General
		}
		private static readonly string[] DefinedStrings = {
			"NEW LAP RECORD", "PERFECT LAP", "FINAL LAP"
		};
		private static readonly Regex TimeFormatRegex = new(
			@"\d\d?:\d{2}\.\d{2}", RegexOptions.None, TimeSpan.FromMilliseconds(10));
		private struct TimeFormatTrimMaterial
		{
			internal const char Minus = '-';
			internal const string MinusUnderTen = "-0:0";
			internal const string MinusUnderSixty = "-0:";
			internal const char Plus = '+';
			internal const string PlusUnderTen = "+0:0";
			internal const string PlusUnderSixty = "+0:";
			internal const string Second = "<size=150>s</size>";
			internal const string LeftPadding = "<size=50> </size>";
			internal const string RightPadding = "<color=#0000>-</color>";
		}
		private readonly StringBuilder _timeFormatTrimmer = new();
		private static StringKind GetStringKind(string message)
		{
			for (int enumIndex = 0; enumIndex < DefinedStrings.Length; enumIndex++)
			{
				string definedString = DefinedStrings[enumIndex];
				// https://forum.unity.com/threads/the-fastest-string-contains-indexof-very-very-fast.126429/#post-852976
				if (message.Length >= definedString.Length && message.Contains(definedString))
					return (StringKind) enumIndex;
			}

			if (TimeFormatRegex.IsMatch(message))
				return message[0] == '-' || message[0] == '+' ?
					StringKind.TimeDiff :
					StringKind.Time;

			return StringKind.General;
		}

		private Dictionary<string, Color> _textColor;
		private Dictionary<string, Color> _defaultColor;

		private readonly List<Line> _lines = new(LineMax);
		private class Line
		{
			internal readonly Text Value;
			private readonly CanvasGroup _cg;
			public float Alpha
			{
				set => _cg.alpha = value;
				get => _cg.alpha;
			}

			public Line(RectTransform template)
			{
				Value = template.GetComponent<Text>();
				_cg = template.GetComponent<CanvasGroup>();
			}
		}
		private readonly float[] _lineDisplayTime = new float[LineMax];
		private readonly float[] _lineFadeOutTimeRemaining = new float[LineMax];
		private readonly bool[] _lineFadeOutInProgress = new bool[LineMax];
		private float _timeDisplayTime;
		private float _timeFadeOutTimeRemaining;
		private bool _timeFadeOutInProgress;
		private float _npDisplayTime;
		private float _npFadeOutTimeRemaining;
		private bool _npFadeOutInProgress;
		private float _wrongWayFadeTimeRemaining;
		private bool _wasWrongWay;

		private string _gamemodeName;
		private bool _facingBackwardExpected;
		private bool _noNewLapRecord;
		private bool _usingThirdLine;
		private bool _usingZoneColors;

		public override void Start()
		{
			base.Start();
			_gamemodeName = RaceManager.CurrentGamemode.Name;
			_facingBackwardExpected = _gamemodeName switch
			{
				"Eliminator" => true,
				_ => false
			};
			_noNewLapRecord = _gamemodeName switch
			{
				"Speed Lap" => false,
				_ => true
			};
			_usingThirdLine = _gamemodeName switch
			{
				"Team Race" => false,
				_ => true
			};
			_usingZoneColors = _gamemodeName switch
			{
				"Survival" when OptionZoneTintOverride => true,
				"Upsurge" when OptionZoneTintOverride => true,
				_ => false
			};

			_textColor = new Dictionary<string, Color>
			{
				{"75", GetTintColor(TextAlpha.ThreeQuarters)},
				{"90", GetTintColor(TextAlpha.NineTenths)},
				{"100", GetTintColor()},
				{"red", GetTintColor(TextAlpha.NineTenths, 1, 1)},
				{"green", GetTintColor(TextAlpha.NineTenths, 5, 1)},
				{"magenta", GetTintColor(TextAlpha.NineTenths, 11, 1)},
				{"cyan", GetTintColor(TextAlpha.NineTenths, 7, 1)},
				{"empty", GetTintColor(TextAlpha.Zero)}
			};

			// using ship engine tint and not zone colors
			if (OptionValueTint == OptionValueTintShipEngineIndexForGame && !_usingZoneColors)
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_textColor["75"] = GetTintFromColor(TextAlpha.ThreeQuarters, engineColor);
				_textColor["90"] = GetTintFromColor(TextAlpha.NineTenths, engineColor);
				_textColor["100"] = GetTintFromColor(color: engineColor);
			}

			_defaultColor = new Dictionary<string, Color>
			{
				{"TimeDiff", _textColor["90"]},
				{"LapResult", _textColor["90"]},
				{"FinalLap", _textColor["90"]},
				{"Line", _textColor["90"]},
				{"NowPlaying", _textColor["90"]},
				{"WrongWay", _textColor["100"]}
			};

			Initiate();

			/*
			 * `OnMidLineTriggered` happens when any ship hits the checkpoint laser,
			 *  prior to the base game call for time difference calculation.
			 * `ShipController.PassedValidationGate` is not
			 * set to true at this point.
			 */
			NgRaceEvents.OnMidLineTriggered += FlushTimeGroupTexts;
			NgUiEvents.OnTriggerMessage += AddMessage;
			NgRaceEvents.OnShipExploded += AddEliminationMessage;
			NgUiEvents.OnNewSongPlaying += AddSong;
			if (_usingZoneColors)
			{
				switch (_gamemodeName)
				{
					case "Survival": NgUiEvents.OnZoneNumberUpdate += UpdateToZoneColor; break;
					case "Upsurge": NgRaceEvents.OnShipScoreChanged += UpdateToZoneColor; break;
				}
			}
		}

		private void FlushTimeGroupTexts(ShipController ship)
		{
			if (ship != TargetShip)
				return;

			_timeDiff.text = "";
			_lapResult.text = "";
			_finalLap.text = "";
		}

		private void Initiate()
		{
			_panel = CustomComponents.GetById("Base");
			RectTransform timeGroupRT = CustomComponents.GetById("TimeGroup");
			_timeGroup = timeGroupRT.GetComponent<CanvasGroup>();
			_timeDiff = timeGroupRT.Find("Difference").GetComponent<Text>();
			_lapResult = timeGroupRT.Find("LapResult").GetComponent<Text>();
			_finalLap = timeGroupRT.Find("FinalLap").GetComponent<Text>();
			_lineTemplate = CustomComponents.GetById("MessageLine");
			Text lineTemplateText = _lineTemplate.GetComponent<Text>();
			_nowPlaying = CustomComponents.GetById<Text>("NowPlaying");
			_wrongWay = CustomComponents.GetById<Text>("WrongWay");
			if (OptionMotion)
			{
				Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
				Shifter.Add(_nowPlaying.GetComponent<RectTransform>(), TargetShip.playerIndex, GetType().Name);
				Shifter.Add(_wrongWay.GetComponent<RectTransform>(), TargetShip.playerIndex, GetType().Name);
			}

			_timeDiff.color = _defaultColor["TimeDiff"];
			_lapResult.color = _defaultColor["LapResult"];
			_finalLap.color = _defaultColor["FinalLap"];
			lineTemplateText.color = _defaultColor["Line"];
			_nowPlaying.color = _defaultColor["NowPlaying"];
			_wrongWayCurrentColor = _defaultColor["WrongWay"] with { a = _wrongWayCurrentAlpha };
			_wrongWay.color = _wrongWayCurrentColor;

			if (Audio.Levels.MusicVolume == 0f)
				_nowPlaying.gameObject.SetActive(false);

			_timeDiff.text = "";
			_lapResult.text = "";
			_finalLap.text = "";
			lineTemplateText.text = "";

			if (_noNewLapRecord)
				_lineTemplate.parent.GetComponent<RectTransform>().anchoredPosition +=
					Vector2.down * LapResultLineHeight;

			float lineHeight = _lineTemplate.sizeDelta.y * _lineTemplate.localScale.y;
			for (int i = 0; i < LineMax; i++)
			{
				RectTransform line =
					Instantiate(_lineTemplate.gameObject).GetComponent<RectTransform>();
				line.SetParent(_lineTemplate.parent);
				line.localScale = _lineTemplate.localScale;
				line.anchoredPosition = _lineTemplate.anchoredPosition;

				line.localPosition += Vector3.up * lineHeight * i;

				_lines.Add(new Line(line));
			}

			_lines[2].Value.gameObject.SetActive(_usingThirdLine);
			_lineTemplate.gameObject.SetActive(false);

			NgRaceEvents.OnCountdownStart += InitiateWrongWayIndicator;
		}

		private void InitiateWrongWayIndicator()
		{
			_wrongWay.text = "wrong way";
			NgRaceEvents.OnCountdownStart -= InitiateWrongWayIndicator;

			_initiated = true;
		}

		private Color GetAdaptedColor(Color inputColor) =>
			inputColor == Color.green ? _textColor["green"] :
			inputColor == Color.red ? _textColor["red"] :
			inputColor == BnGAccent ? _defaultColor["Line"] :
			GetTintFromColor(TextAlpha.NineTenths, inputColor);

		private string TrimTimeDiffText(string timeDiff)
		{
			_timeFormatTrimmer
				.Clear()
				.Append(timeDiff);

			char sign = timeDiff[0];

			switch (sign)
			{
				case TimeFormatTrimMaterial.Minus:
					_timeFormatTrimmer
						.Replace(TimeFormatTrimMaterial.MinusUnderTen, null)
						.Replace(TimeFormatTrimMaterial.MinusUnderSixty, null);
					break;
				case TimeFormatTrimMaterial.Plus:
					_timeFormatTrimmer
						.Replace(TimeFormatTrimMaterial.PlusUnderTen, null)
						.Replace(TimeFormatTrimMaterial.PlusUnderSixty, null);
					break;
				default:
					return timeDiff;
			}

			if (_timeFormatTrimmer.Length < timeDiff.Length)
			{
				_timeFormatTrimmer
					.Insert(0, sign)
					.Insert(0, TimeFormatTrimMaterial.LeftPadding)
					.Append(TimeFormatTrimMaterial.Second);
			}
			else
				_timeFormatTrimmer.Append(TimeFormatTrimMaterial.RightPadding);

			return _timeFormatTrimmer.ToString();
		}

		private void AddMessage(string message, ShipController ship, Color color)
		{
			// this will ignore messages that have an other ship as "a receiver"
			if (ship != TargetShip)
				return;

			color = GetAdaptedColor(color);

			StringKind kind = GetStringKind(message);

			if (kind != StringKind.General)
			{
				color = OptionTimeDiffColour switch
				{
					2 when color == _textColor["green"] => _textColor["cyan"],
					1 when color == _textColor["red"] => _textColor["magenta"],
					_ => color
				};

				switch (kind)
				{
					case StringKind.NewLapRecord:
						_lapResult.text = message;
						break;
					case StringKind.PerfectLap:
						_lapResult.text += Environment.NewLine + message;
						break;
					case StringKind.TimeDiff:
						_timeDiff.text = TrimTimeDiffText(message);
						_timeDiff.color = color;
						break;
					case StringKind.Time:
						_timeDiff.text = message;
						break;
					case StringKind.FinalLap:
						_finalLap.text = message;
						break;
				}

				_timeGroup.alpha = 1f;
				_timeDisplayTime = DefaultDisplayTime;
				_timeFadeOutTimeRemaining = 0f;
			}
			else
			{
				InsertMessageLine(message, color);
			}
		}

		private void AddSong(string songName)
		{
			bool musicIsOn = Audio.Levels.MusicVolume != 0f;
			_nowPlaying.gameObject.SetActive(musicIsOn);
			if (!musicIsOn)
				return;

			_nowPlaying.text = songName;
			_nowPlaying.color = _defaultColor["NowPlaying"];
			_npDisplayTime = DefaultDisplayTime;
			_npFadeOutTimeRemaining = 0f;
		}

		private void AddEliminationMessage(ShipController ship)
		{
			if (!RaceManager.CurrentGamemode.Configuration.KillFeedEnabled)
				return;

			string message =
				ship.LastAttacker is not null ?
					ship.LastAttacker.ShipName + " eliminated " + ship.ShipName :
					ship.ShipName + " eliminated";

			InsertMessageLine(
				message,
				GetAdaptedColor(Color.red),
				/*
				 * Motion effect includes hiding hud on getting exploded,
				 * so if it's disabled the extended display time is not needed.
				 */
				OptionMotion && ship == TargetShip ? Gamemode.DefaultRespawnTime : 0
			);
		}

		private void InsertMessageLine(string message, Color color, float extendTime = 0)
		{
			Line line;
			for (int i = _lines.Count - 1; i > 0; i--)
			{
				line = _lines[i];
				Line lineBelow = _lines[i - 1];

				line.Alpha = lineBelow.Alpha;
				line.Value.text = lineBelow.Value.text;
				line.Value.color = lineBelow.Value.color;
				_lineDisplayTime[i] = _lineDisplayTime[i - 1];
				_lineFadeOutTimeRemaining[i] = _lineFadeOutTimeRemaining[i - 1];
			}

			line = _lines[0];
			line.Alpha = 1f;
			line.Value.text = message;
			line.Value.color = color;
			_lineDisplayTime[0] = DefaultDisplayTime + extendTime;
			_lineFadeOutTimeRemaining[0] = 0f;
		}

		public override void Update()
		{
			base.Update();
			if (!_initiated)
				return;

			if (_usingZoneColors && _currentZoneColor is null && PalleteSettingsLoaded())
				UpdateToZoneColor(0);

			// general messages
			for (int i = 0; i < _lines.Count; i++)
			{
				if (_lineDisplayTime[i] > 0f)
				{
					_lineDisplayTime[i] -= Time.deltaTime;
					if (_lineDisplayTime[i] <= 0f) _lineFadeOutTimeRemaining[i] = FadeOutTimeMax;
				}
				else
					_lineDisplayTime[i] = 0f;

				if (
					_lineFadeOutTimeRemaining[i] > 0f &&
					!_lineFadeOutInProgress[i]
				)
					StartCoroutine(RemoveMessage(i));

				if (
					_lineDisplayTime[i] == 0f &&
					_lineFadeOutTimeRemaining[i] == 0f &&
					!_lineFadeOutInProgress[i]
				)
				{
					_lines[i].Alpha = 0f;
					_lines[i].Value.text = "";
					_lines[i].Value.color = _defaultColor["Line"];
				}
			}

			// time display
			if (_timeDisplayTime > 0f)
			{
				_timeDisplayTime -= Time.deltaTime;
				if (_timeDisplayTime <= 0f)
					_timeFadeOutTimeRemaining = FadeOutTimeMax;
			}
			else
				_timeDisplayTime = 0f;

			if (
				_timeFadeOutTimeRemaining > 0f &&
				!_timeFadeOutInProgress
			)
				StartCoroutine(RemoveTime());

			if (
				_timeDisplayTime == 0f &&
				_timeFadeOutTimeRemaining == 0f &&
				!_timeFadeOutInProgress
			)
			{
				_timeDiff.text = "";
				_lapResult.text = "";
				_finalLap.text = "";
				_timeDiff.color = _defaultColor["TimeDiff"];
				_lapResult.color = _defaultColor["LapResult"];
				_finalLap.color = _defaultColor["FinalLap"];
			}

			// now playing
			if (Audio.Levels.MusicVolume != 0f)
			{
				if (_npDisplayTime > 0f)
				{
					_npDisplayTime -= Time.deltaTime;
					if (_npDisplayTime <= 0f)
						_npFadeOutTimeRemaining = FadeOutTimeMax;
				}
				else
					_npDisplayTime = 0f;

				if (
					_npFadeOutTimeRemaining > 0f &&
					!_npFadeOutInProgress
				)
					StartCoroutine(RemoveSong());

				if (
					_npDisplayTime == 0f &&
					_npFadeOutTimeRemaining == 0f &&
					!_npFadeOutInProgress
				)
				{
					_nowPlaying.text = "";
					_nowPlaying.color = _defaultColor["NowPlaying"];
				}
			}

			if (_facingBackwardExpected)
				return;

			// wrong way
			if (
				!_wasWrongWay && !TargetShip.FacingForward ||
				_wasWrongWay && TargetShip.FacingForward
			)
			{
				_wrongWayAlpha = _wasWrongWay ? 0f : 1f;
				_wrongWayFadeTimeRemaining = WrongWayFadeTimeMax;
				_wasWrongWay = !TargetShip.FacingForward;
			}

			if (_wrongWayFadeTimeRemaining > 0f)
			{
				_wrongWayCurrentAlpha =
					Mathf.Lerp(_wrongWayCurrentAlpha, _wrongWayAlpha,
						Time.deltaTime * WrongWayFadeSpeed);
				_wrongWay.color = _wrongWayCurrentColor with { a = _wrongWayCurrentAlpha };
				_wrongWayFadeTimeRemaining -= Time.deltaTime;
			}
			else
			{
				_wrongWay.color = _wrongWayCurrentColor with { a = _wrongWayAlpha };
				_wrongWayFadeTimeRemaining = 0f;
			}
		}

		private IEnumerator RemoveMessage(int i)
		{
			_lineFadeOutInProgress[i] = true;

			while (_lineFadeOutTimeRemaining[i] > 0f)
			{
				_lines[i].Alpha = Mathf.Lerp(_lines[i].Alpha, 0f, Time.deltaTime * FadeOutSpeed);
				_lineFadeOutTimeRemaining[i] -= Time.deltaTime;
				yield return null;
			}

			_lineFadeOutTimeRemaining[i] = 0f;
			_lineFadeOutInProgress[i] = false;
		}

		private IEnumerator RemoveTime()
		{
			_timeFadeOutInProgress = true;

			while (_timeFadeOutTimeRemaining > 0f)
			{
				_timeGroup.alpha = Mathf.Lerp(_timeGroup.alpha, 0f, Time.deltaTime * FadeOutSpeed);

				_timeFadeOutTimeRemaining -= Time.deltaTime;
				yield return null;
			}

			_timeFadeOutTimeRemaining = 0f;
			_timeFadeOutInProgress = false;
		}

		private IEnumerator RemoveSong()
		{
			_npFadeOutInProgress = true;
			Color color = _nowPlaying.color;

			while (_npFadeOutTimeRemaining > 0f)
			{
				if (_npDisplayTime >= DefaultDisplayTime)
				{
					_nowPlaying.color = _defaultColor["NowPlaying"];
					break;
				}

				color.a = Mathf.Lerp(color.a, 0f, Time.deltaTime * FadeOutSpeed);
				_nowPlaying.color = color;

				_npFadeOutTimeRemaining -= Time.deltaTime;
				yield return null;
			}

			_npFadeOutTimeRemaining = 0f;
			_npFadeOutInProgress = false;
		}

		private void UpdateToZoneColor(int zoneNumber)
		{
			Color color = GetZoneColor(zoneNumber);
			if (_currentZoneColor == color)
				return;

			_currentZoneColor = color;
			_textColor["75"] = GetTintFromColor(TextAlpha.ThreeQuarters, color);
			_textColor["90"] = GetTintFromColor(TextAlpha.NineTenths, color);
			_textColor["100"] = GetTintFromColor(color: color);

			// oh no I can't iterate over a Dictionary
			foreach (string text in new List<string>(_defaultColor.Keys))
				_defaultColor[text] = text != "WrongWay" ? _textColor["90"] : _textColor["100"];
		}
		private void UpdateToZoneColor(string number)
		{
			int zoneNumber = Convert.ToInt32(number);
			if (zoneNumber % 5 != 0)
				return;
			UpdateToZoneColor(zoneNumber);
		}
		private void UpdateToZoneColor(ShipController ship, float oldScore, float newScore)
		{
			if (ship != TargetShip)
				return;
			UpdateToZoneColor((int) newScore);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnMidLineTriggered -= FlushTimeGroupTexts;
			NgUiEvents.OnTriggerMessage -= AddMessage;
			NgRaceEvents.OnShipExploded -= AddEliminationMessage;
			NgUiEvents.OnNewSongPlaying -= AddSong;
			if (_usingZoneColors)
			{
				switch (_gamemodeName)
				{
					case "Survival": NgUiEvents.OnZoneNumberUpdate -= UpdateToZoneColor; break;
					case "Upsurge": NgRaceEvents.OnShipScoreChanged -= UpdateToZoneColor; break;
				}
			}
		}
	}

	public class PickupDisplay : CustomScaleScriptableHud
	{
		private RectTransform _panel;
		private PickupPanel _playerPanel;
		private PickupPanel _warningPanel;
		private float _warningTimer;
		private const float WarningTimeMax = 2.5f;

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_playerPanel = new PickupPanel(
				_panel.Find("IconBackground").GetComponent<RectTransform>(),
				_panel.Find("Info").GetComponent<Text>());
			_warningPanel = new PickupPanel(
				_panel.Find("WarningBackground").GetComponent<RectTransform>());

			if (OptionValueTint == OptionValueTintShipEngineIndexForGame)
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_playerPanel.UpdateColor(engineColor);
				_warningPanel.UpdateColor(engineColor);
			}

			PickupBase.OnPickupInit += ShowPickup;
			PickupBase.OnPickupDeinit += HidePickup;
			NgUiEvents.OnWeaponWarning += Warn;
		}

		private void ShowPickup(PickupBase pickup, ShipController ship)
		{
			if (ship != TargetShip)
				return;
			_playerPanel.UpdateText("");
			_playerPanel.UpdateSprite(ship.CurrentPickupRegister.Name);
			if(_playerPanel.CurrentTransition is not null)
				StopCoroutine(_playerPanel.CurrentTransition);
			_playerPanel.CurrentTransition =
				StartCoroutine(_playerPanel.ColorFade(
					true,
					ship.CurrentPickupRegister.HudColor == Pickup.EHudColor.Offensive
				));
		}

		private void HidePickup(PickupBase pickup, ShipController ship)
		{
			if (ship != TargetShip)
				return;
			if(_playerPanel.CurrentTransition is not null)
				StopCoroutine(_playerPanel.CurrentTransition);
			_playerPanel.CurrentTransition =
				StartCoroutine(_playerPanel.ColorFade(false));
		}

		private void Warn(Pickup pickup)
		{
			if(_warningPanel.CurrentTransition is not null)
				StopCoroutine(_warningPanel.CurrentTransition);
			_warningPanel.UpdateSprite(pickup.Name);
			_warningPanel.ShowWarning();
			_warningTimer = WarningTimeMax;
		}

		public override void Update()
		{
			base.Update();
			if (!TargetShip)
				return;
			_playerPanel.UpdateText(TargetShip.PickupDisplayText);

			if (_warningTimer > 0f)
				_warningTimer -= Time.deltaTime;
			else
			{
				if (!_warningPanel.IconEnabled)
					return;
				_warningPanel.CurrentTransition =
					StartCoroutine(_warningPanel.ColorFade(false));
				_warningTimer = 0f;
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			PickupBase.OnPickupInit -= ShowPickup;
			PickupBase.OnPickupDeinit -= HidePickup;
			NgUiEvents.OnWeaponWarning -= Warn;
		}
	}

	public class TurboDisplay : CustomScaleScriptableHud
	{
		private RectTransform _panel;
		private PickupPanel _playerPanel;

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_playerPanel = new PickupPanel(_panel);

			if (OptionValueTint == OptionValueTintShipEngineIndexForGame)
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_playerPanel.UpdateColor(engineColor);
			}

			PickupBase.OnPickupInit += ShowPickup;
			PickupBase.OnPickupDeinit += HidePickup;
		}

		private void ShowPickup(PickupBase pickup, ShipController ship)
		{
			if (ship != TargetShip)
				return;
			_playerPanel.UpdateSprite(ship.CurrentPickupRegister.Name);
			if(_playerPanel.CurrentTransition is not null)
				StopCoroutine(_playerPanel.CurrentTransition);
			_playerPanel.CurrentTransition =
				StartCoroutine(_playerPanel.ColorFade(true, usePickupColor: false));
		}

		private void HidePickup(PickupBase pickup, ShipController ship)
		{
			if (ship != TargetShip)
				return;
			if(_playerPanel.CurrentTransition is not null)
				StopCoroutine(_playerPanel.CurrentTransition);
			_playerPanel.CurrentTransition =
				StartCoroutine(_playerPanel.ColorFade(false));
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			PickupBase.OnPickupInit -= ShowPickup;
			PickupBase.OnPickupDeinit -= HidePickup;
		}
	}

	public class Leaderboard : CustomScaleScriptableHud
	{
		private Playerboard _panel;
		private bool _hideLastSlotOnExplosion;

		private Color? _currentZoneColor = null;
		private bool _usingZoneColors;

		private const int splitscreenSlotScaleThreshold = 10;

		public override void Start()
		{
			base.Start();
			_panel = new Playerboard(CustomComponents.GetById("Base"),
				RaceManager.CurrentGamemode.Name);
			if (OptionMotion) Shifter.Add(_panel.Base, TargetShip.playerIndex, GetType().Name);

			_usingZoneColors = 
				RaceManager.CurrentGamemode.Name == "Upsurge" && OptionZoneTintOverride;

			// Data in `RaceManager.CurrentGamemode` are lost when `OnDestroy()` is called.
			_hideLastSlotOnExplosion = RaceManager.CurrentGamemode.Name == "Knockout";
			if (_hideLastSlotOnExplosion)
				NgRaceEvents.OnShipExploded += _panel.HideLastSlot;
			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			if (
				Gameplay.SplitscreenEnabled && !Gameplay.InVerticalSplitscreen &&
				Ships.Loaded.Count > splitscreenSlotScaleThreshold
			)
				_panel.ScaleSlot((float) splitscreenSlotScaleThreshold / Ships.Loaded.Count);

			/*
			 * CurrentGamemode can be Gamemode or inheritances of it.
			 * Accessing it through RaceManager instead of
			 * assigning it to a field can make it easy to
			 * access common fields.
			 */
			_panel.InitiateLayout(RaceManager.CurrentGamemode.TargetScore);
			_panel.InitiateSlots(Ships.Loaded);
			StartCoroutine(_panel.Update(Ships.Loaded));

			if (_usingZoneColors)
				UpdateToZoneColor(0);
			else if (OptionValueTint == OptionValueTintShipEngineIndexForGame)
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_panel.UpdateColor(GetTintFromColor(color: engineColor));
			}

			NgRaceEvents.OnCountdownStart -= Initiate;

			if (_usingZoneColors)
				NgRaceEvents.OnShipScoreChanged += UpdateToZoneColor;
		}

		private void UpdateToZoneColor(int zoneNumber)
		{
			Color color = GetZoneColor(zoneNumber);
			if (_currentZoneColor == color)
				return;

			_currentZoneColor = color;
			_panel.UpdateColor(GetTintFromColor(color: color));
		}
		private void UpdateToZoneColor(ShipController ship, float oldScore, float newScore)
		{
			if (ship != TargetShip)
				return;
			UpdateToZoneColor((int) newScore);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			StopCoroutine(_panel.Update(Ships.Loaded));
			if (_hideLastSlotOnExplosion)
				NgRaceEvents.OnShipExploded -= _panel.HideLastSlot;
			if (_usingZoneColors)
				NgRaceEvents.OnShipScoreChanged -= UpdateToZoneColor;
		}
	}

	public class TeamScoreboard : CustomScaleScriptableHud
	{
		private RectTransform _panel;
		private PickupPanel _teammatePickupPanel;
		private Text _labelFirst;
		private Text _labelSecond;
		private RectTransform _slotLeft;
		private RectTransform _slotRight;
		private RectTransform _slotMiddle;
		private TeamPanel[] _teamPanels;

		private const float SlotShiftAmount = 105f;
		private static readonly string[] SecondLabelString = {
			"second", "third", "fourth"
		};

		private class TeamPanel
		{
			private Color ValueColor;
			private Color ValueAdditionColor;
			private Color MemberColor;
			private Color PlacementColor;
			private Color PlayerHighlightColor;
			private readonly Color PanelColor = GetPanelColor();
			private Color PlayerPanelColor;

			private RaceTeam _team;
			private bool _isPlayerTeam;

			private readonly Image _panelImage;
			private readonly Text _value;
			private readonly Text _valueAddition;
			private readonly Text _memberFirst;
			private readonly Text _memberSecond;
			private readonly Text _placementFirst;
			private readonly Text _placementSecond;
			private readonly Image _playerHighlight;

			private bool IsPlayerTeam
			{
				set
				{
					_isPlayerTeam = value;
					if (value)
					{
						_panelImage.color = PlayerPanelColor;
						_memberFirst.color = MemberColor;
						_memberSecond.color = MemberColor;
						_placementFirst.enabled = true;
						_placementSecond.enabled = true;
						_playerHighlight.enabled = true;
					}
					else
					{
						_panelImage.color = PanelColor;
						_memberFirst.color = PlacementColor;
						_memberSecond.color = PlacementColor;
						_placementFirst.enabled = false;
						_placementSecond.enabled = false;
						_playerHighlight.enabled = false;
					}

				}
			}

			private float Score
			{
				set => _value.text = value switch
				{
					< 2000f => (Math.Round(value * 100f) / 100.0).ToString("F2", CultureInfo.InvariantCulture),
					_       => (Math.Round(value *  10f) /  10.0).ToString("F1", CultureInfo.InvariantCulture),
				};
			}

			private float ScoreAddition
			{
				set => _valueAddition.text =
					"+" + (Math.Round(value * 100f) / 100.0).ToString("F2", CultureInfo.InvariantCulture);
			}

			internal void UpdateTeam(RaceTeam team, ShipController playerShip)
			{

				if (team == _team)
					Score = team.Score;
				else
				{
					IsPlayerTeam = team.Ships[0] == playerShip || team.Ships[1] == playerShip;
					_team = team;
					Score = team.Score;
					ScoreAddition = team.PlaceScore;
					_memberFirst.text = team.Ships[0].ShipName;
					_memberSecond.text = team.Ships[1].ShipName;
					/*
					 * Default hud just clears the text instead when changing the panels,
					 * leaving the text empty for few frames after they are changed.
					 * I wonder if keeping that way is safer,
					 * whatever "safe" would mean here.
					 */
					UpdatePlacement();
				}
			}

			internal void UpdatePlacement()
			{
				if (!_isPlayerTeam)
					return;

				_placementFirst.text =
					IntStrDb.GetNoSingleCharNumber(_team.Ships[0].CurrentPlace);
				_placementSecond.text =
					IntStrDb.GetNoSingleCharNumber(_team.Ships[1].CurrentPlace);
				ScoreAddition = _team.PlaceScore;
			}

			internal void UpdateColor()
			{
				ValueColor = GetTintColor();
				ValueAdditionColor = GetTintColor(TextAlpha.Half);
				MemberColor = GetTintColor(TextAlpha.ThreeEighths);
				PlacementColor = GetTintColor(clarity: 2);
				PlayerHighlightColor = GetTintColor(clarity: 1);
				PlayerPanelColor = GetPanelColor(OptionValueTint);

				_value.color = ValueColor;
				_valueAddition.color = ValueAdditionColor;
				_placementFirst.color = PlacementColor;
				_placementSecond.color = PlacementColor;
				_playerHighlight.color = PlayerHighlightColor;
			}

			internal void UpdateColor(Color color)
			{
				ValueColor = GetTintFromColor(color: color);
				ValueAdditionColor = GetTintFromColor(TextAlpha.Half, color);
				MemberColor = GetTintFromColor(TextAlpha.ThreeEighths, color);
				PlacementColor = GetTintFromColor(color: color, clarity: 2);
				PlayerHighlightColor = GetTintFromColor(color: color, clarity: 1);
				PlayerPanelColor = GetPanelColorFromColor(color);

				_value.color = ValueColor;
				_valueAddition.color = ValueAdditionColor;
				_placementFirst.color = PlacementColor;
				_placementSecond.color = PlacementColor;
				_playerHighlight.color = PlayerHighlightColor;
			}

			public TeamPanel(RectTransform panel)
			{
				_panelImage = panel.GetComponent<Image>();
				_value = panel.Find("Value").GetComponent<Text>();
				_valueAddition = panel.Find("ValueAddition").GetComponent<Text>();
				_memberFirst = panel.Find("MemberFirst").GetComponent<Text>();
				_memberSecond = panel.Find("MemberSecond").GetComponent<Text>();
				_placementFirst = panel.Find("PlacementFirst").GetComponent<Text>();
				_placementSecond = panel.Find("PlacementSecond").GetComponent<Text>();
				_playerHighlight = panel.Find("Highlight").GetComponent<Image>();

				UpdateColor();
			}
		}

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_teammatePickupPanel =
				new PickupPanel(_panel.Find("TeammatePickup").GetComponent<RectTransform>());
			_labelFirst = _panel.Find("LabelFirst").GetComponent<Text>();
			_labelSecond = _panel.Find("LabelSecond").GetComponent<Text>();
			_slotLeft = _panel.Find("SlotLeft").GetComponent<RectTransform>();
			_slotRight = _panel.Find("SlotRight").GetComponent<RectTransform>();
			_slotMiddle = _panel.Find("SlotMiddle").GetComponent<RectTransform>();

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void InitiateLayout()
		{
			int teamCount = Ships.Teams.Count;
			_teamPanels = new TeamPanel[Math.Min(teamCount, 6)];

			_slotMiddle.gameObject.SetActive(teamCount % 2 != 0 && teamCount <= 5);
			_slotLeft.gameObject.SetActive(teamCount >= 2);
			_slotRight.gameObject.SetActive(teamCount >= 2);
			if (teamCount is 3 or 5)
			{
				_slotLeft.anchoredPosition += Vector2.left * SlotShiftAmount;
				_slotRight.anchoredPosition += Vector2.right * SlotShiftAmount;
			}

			int teamPanelIndex = 0;
			switch (teamCount)
			{
				case 1:
					_teamPanels[teamPanelIndex] = new TeamPanel(_slotMiddle);
					break;
				case 2 or 3:
					_teamPanels[teamPanelIndex++] = new TeamPanel(_slotRight);
					if (teamCount == 3)
						_teamPanels[teamPanelIndex++] = new TeamPanel(_slotMiddle);
					_teamPanels[teamPanelIndex] = new TeamPanel(_slotLeft);
					break;
				case >= 4:
					RectTransform slotLefter = Instantiate(_slotLeft, _slotLeft.parent);
					slotLefter.anchoredPosition += Vector2.left * SlotShiftAmount * 2;
					RectTransform slotRighter = Instantiate(_slotRight, _slotRight.parent);
					slotRighter.anchoredPosition += Vector2.right * SlotShiftAmount * 2;
					RectTransform slotLeftest = null;
					RectTransform slotRightest = null;

					if (teamCount >= 6)
					{
						slotLeftest = Instantiate(_slotLeft, _slotLeft.parent);
						slotLeftest.anchoredPosition += Vector2.left * SlotShiftAmount * 4;
						slotRightest = Instantiate(_slotRight, _slotRight.parent);
						slotRightest.anchoredPosition += Vector2.right * SlotShiftAmount * 4;
					}

					if (teamCount >= 6)
						_teamPanels[teamPanelIndex++] = new TeamPanel(slotRightest);
					_teamPanels[teamPanelIndex++] = new TeamPanel(slotRighter);
					_teamPanels[teamPanelIndex++] = new TeamPanel(_slotRight);
					if (teamCount == 5)
						_teamPanels[teamPanelIndex++] = new TeamPanel(_slotMiddle);
					_teamPanels[teamPanelIndex++] = new TeamPanel(_slotLeft);
					_teamPanels[teamPanelIndex++] = new TeamPanel(slotLefter);
					if (teamCount >= 6)
						_teamPanels[teamPanelIndex] = new TeamPanel(slotLeftest);

					_labelFirst.gameObject.SetActive(teamCount > 6);
					_labelSecond.gameObject.SetActive(teamCount > 6);
					break;
			}

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame)
			{
				_labelFirst.color = GetTintColor();
				_labelSecond.color = GetTintColor();
			}
			else
				UpdateColor(GetShipRepresentativeColor(TargetShip));
		}

		private void Initiate()
		{
			InitiateLayout();

			for (int i = 0; i < _teamPanels.Length; i++)
				_teamPanels[i].UpdateTeam(Ships.Teams[i], TargetShip);

			StartCoroutine(SetPlacement());
			RaceTeam.OnScoreUpdated += UpdatePanels;
			PickupBase.OnPickupInit += ShowPickup;
			PickupBase.OnPickupDeinit += HidePickup;

			NgRaceEvents.OnCountdownStart -= Initiate;
		}

		private void UpdatePanels(RaceTeam team, float oldScore, float newScore)
		{
			RaceTeam[] teams = Ships.Teams.OrderByDescending(t => t.Score).ToArray();
			int otherTeamIndex = 1;
			if (teams.Length > 6)
			{
				otherTeamIndex =
					Array.FindIndex(teams, t => t == Ships.GetTeamForShip(TargetShip)) - 2;
				otherTeamIndex = otherTeamIndex < 1 ?
					1 : otherTeamIndex > teams.Length - 5 ?
						teams.Length - 5 : otherTeamIndex;
			}

			_labelSecond.text = SecondLabelString[otherTeamIndex - 1];

			_teamPanels[0].UpdateTeam(teams[0], TargetShip);
			for (int i = 1; i < _teamPanels.Length; i++)
				_teamPanels[i].UpdateTeam(teams[otherTeamIndex++], TargetShip);
		}

		private bool IsTeammateShip(ShipController ship)
		{
			RaceTeam playerTeam = Ships.GetTeamForShip(TargetShip);
			return ship == playerTeam.Ships[
				playerTeam.Ships[0] == TargetShip ? 1 : 0
			];
		}

		private void ShowPickup(PickupBase pickup, ShipController ship)
		{
			if (!IsTeammateShip(ship))
				return;

			_teammatePickupPanel.UpdateSprite(ship.CurrentPickupRegister.Name);
			if (_teammatePickupPanel.CurrentTransition is not null)
				StopCoroutine(_teammatePickupPanel.CurrentTransition);
			_teammatePickupPanel.ShowInstant(
				ship.CurrentPickupRegister.HudColor == Pickup.EHudColor.Offensive
			);
		}

		private void HidePickup(PickupBase pickup, ShipController ship)
		{
			if (!IsTeammateShip(ship))
				return;

			if (_teammatePickupPanel.CurrentTransition is not null)
				StopCoroutine(_teammatePickupPanel.CurrentTransition);
			_teammatePickupPanel.CurrentTransition =
				StartCoroutine(_teammatePickupPanel.ColorFade(false));
		}

		private IEnumerator SetPlacement()
		{
			while (true)
			{
				foreach (TeamPanel panel in _teamPanels)
					panel.UpdatePlacement();

				yield return new WaitForSeconds(Position.UpdateTime);
			}
		}

		public void UpdateColor(Color color)
		{
			_labelFirst.color = GetTintFromColor(color: color);
			_labelSecond.color = GetTintFromColor(color: color);

			foreach (TeamPanel p in _teamPanels)
				p.UpdateColor(color);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			StopCoroutine(SetPlacement());
			RaceTeam.OnScoreUpdated -= UpdatePanels;
			PickupBase.OnPickupInit -= ShowPickup;
			PickupBase.OnPickupDeinit -= HidePickup;
		}
	}

	public class Awards : CustomScaleScriptableHud
	{
		private const float InitialPanelAlpha = 0.9f;
		private const float ActivePanelAlpha = 1f;
		private const float TemporarilyInactivePanelAlpha = 0.5f;
		private const float InactivePanelAlpha = 0.25f;

		private RectTransform _panel;
		private CanvasGroup _panelPlatinum;
		private Image _panelPlatinumImage;
		private Text _textPlatinum;
		private CanvasGroup _panelGold;
		private Image _panelGoldImage;
		private Text _textGold;
		private CanvasGroup _panelSilver;
		private Image _panelSilverImage;
		private Text _textSilver;
		private CanvasGroup _panelBronze;
		private Image _panelBronzeImage;
		private Text _textBronze;

		private readonly Color _activePlatinumColor =
			GetTintColor(TextAlpha.ThreeQuarters, 7, 0);
		private readonly Color _activeGoldColor =
			GetTintColor(TextAlpha.ThreeQuarters, 3, 0);
		private readonly Color _activeSilverColor =
			GetTintColor(TextAlpha.ThreeQuarters, 0, 0);
		private readonly Color _activeBronzeColor =
			GetTintColor(TextAlpha.ThreeQuarters, 2, 0);
		private readonly Color _activeTextColor =
			GetPanelColor() with { a = GetTransparency(TextAlpha.Full) };

		private string _gamemodeName;
		private float _missedPanelAlpha = InactivePanelAlpha;

		private float _platinumTarget;
		private float _goldTarget;
		private float _silverTarget;
		private float _bronzeTarget;
		private bool _targetIsTime;
		private bool _isSpeedLap;

		private float _progressTime;
		private int _progressZone;

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_panelPlatinum = _panel.Find("Platinum").GetComponent<CanvasGroup>();
			_panelPlatinumImage = _panelPlatinum.GetComponent<Image>();
			_textPlatinum = _panel.Find("Platinum").Find("Value").GetComponent<Text>();
			_panelGold = _panel.Find("Gold").GetComponent<CanvasGroup>();
			_panelGoldImage = _panelGold.GetComponent<Image>();
			_textGold = _panel.Find("Gold").Find("Value").GetComponent<Text>();
			_panelSilver = _panel.Find("Silver").GetComponent<CanvasGroup>();
			_panelSilverImage = _panelSilver.GetComponent<Image>();
			_textSilver = _panel.Find("Silver").Find("Value").GetComponent<Text>();
			_panelBronze = _panel.Find("Bronze").GetComponent<CanvasGroup>();
			_panelBronzeImage = _panelBronze.GetComponent<Image>();
			_textBronze = _panel.Find("Bronze").Find("Value").GetComponent<Text>();

			_platinumTarget = NgCampaign.CurrentEvent.EventProgress.PlatinumValue;
			_goldTarget = NgCampaign.CurrentEvent.EventProgress.GoldValue;
			_silverTarget = NgCampaign.CurrentEvent.EventProgress.SilverValue;
			_bronzeTarget = NgCampaign.CurrentEvent.EventProgress.BronzeValue;

			_gamemodeName = RaceManager.CurrentGamemode.Name;

			_targetIsTime = _gamemodeName switch
			{
				"Time Trial" => true,
				"Speed Lap" => true,
				"Survival" => false,
				_ => true
			};
			_isSpeedLap = _gamemodeName == "Speed Lap";

			if (_isSpeedLap)
			{
				const float shiftAmount = 75f;
				_panelPlatinum.GetComponent<RectTransform>().anchoredPosition += Vector2.left * shiftAmount;
				_panelGold.GetComponent<RectTransform>().anchoredPosition += Vector2.left * shiftAmount;
				_panelSilver.GetComponent<RectTransform>().anchoredPosition += Vector2.right * shiftAmount;
				_panelBronze.GetComponent<RectTransform>().anchoredPosition += Vector2.right * shiftAmount;
				_missedPanelAlpha = TemporarilyInactivePanelAlpha;
				NgUiEvents.OnGamemodeUpdateCurrentLapTime += UpdateTime;
				NgUiEvents.OnGamemodeInvalidatedLap += InvalidateLap;
				NgRaceEvents.OnShipLapUpdate += ResetPanelAlpha;
			}

			if (_targetIsTime)
			{
				_textPlatinum.text = FloatToTime.Convert(_platinumTarget, TimeFormat);
				_textGold.text = FloatToTime.Convert(_goldTarget, TimeFormat);
				_textSilver.text = FloatToTime.Convert(_silverTarget, TimeFormat);
				_textBronze.text = FloatToTime.Convert(_bronzeTarget, TimeFormat);
			}
			else
			{
				_textPlatinum.text = "Zone " + _platinumTarget;
				_textGold.text = "Zone " + _goldTarget;
				_textSilver.text = "Zone " + _silverTarget;
				_textBronze.text = "Zone " + _bronzeTarget;
				NgUiEvents.OnZoneNumberUpdate += UpdateZone;
			}
		}

		public override void Update()
		{
			base.Update();
			if (_targetIsTime && !_isSpeedLap) UpdateTime(TargetShip.TotalRaceTime);
		}

		private void UpdateTime(float currentTime = -1f)
		{
			_progressTime = currentTime == -1f ?
				TargetShip.TotalRaceTime : currentTime;

			if (_progressTime <= _platinumTarget)
			{
				_panelPlatinum.alpha = ActivePanelAlpha;
			}
			else if (_progressTime <= _goldTarget)
			{
				_panelPlatinum.alpha = _missedPanelAlpha;
				_panelGold.alpha = ActivePanelAlpha;
			}
			else if (_progressTime <= _silverTarget)
			{
				_panelGold.alpha = _missedPanelAlpha;
				_panelSilver.alpha = ActivePanelAlpha;
			}
			else if (_progressTime <= _bronzeTarget)
			{
				_panelSilver.alpha = _missedPanelAlpha;
				_panelBronze.alpha = ActivePanelAlpha;
			}
			else
			{
				_panelBronze.alpha = _missedPanelAlpha;
			}
		}

		private void InvalidateLap()
		{
			_panelPlatinum.alpha = _missedPanelAlpha;
			_panelGold.alpha = _missedPanelAlpha;
			_panelSilver.alpha = _missedPanelAlpha;
			_panelBronze.alpha = _missedPanelAlpha;
		}

		private void ResetPanelAlpha(ShipController ship)
		{
			_panelPlatinum.alpha = InitialPanelAlpha;
			_panelGold.alpha = InitialPanelAlpha;
			_panelSilver.alpha = InitialPanelAlpha;
			_panelBronze.alpha = InitialPanelAlpha;
		}

		private void UpdateZone(string number)
		{
			_progressZone = Convert.ToInt32(number);

			if (_progressZone >= _bronzeTarget)
			{
				_panelBronzeImage.color = _activeBronzeColor;
				_textBronze.color = _activeTextColor;
			}
			if (_progressZone >= _silverTarget)
			{
				_panelSilverImage.color = _activeSilverColor;
				_textSilver.color = _activeTextColor;
			}
			if (_progressZone >= _goldTarget)
			{
				_panelGoldImage.color = _activeGoldColor;
				_textGold.color = _activeTextColor;
			}
			if (_progressZone >= _platinumTarget)
			{
				_panelPlatinumImage.color = _activePlatinumColor;
				_textPlatinum.color = _activeTextColor;
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (_isSpeedLap)
			{
				NgUiEvents.OnGamemodeUpdateCurrentLapTime -= UpdateTime;
				NgUiEvents.OnGamemodeInvalidatedLap -= InvalidateLap;
				NgRaceEvents.OnShipLapUpdate -= ResetPanelAlpha;
			}
			if (!_targetIsTime)
			{
				NgUiEvents.OnZoneNumberUpdate -= UpdateZone;
			}
		}
	}

	public class RaceFinishCountdown : CustomScaleScriptableHud
	{
		private RectTransform _panel;
		private Text _label;
		private Text _value;
		private float _timer;

		public override void Start()
		{
			base.Start();
			_panel = CustomComponents.GetById("Base");
			if (OptionMotion) Shifter.Add(_panel, TargetShip.playerIndex, GetType().Name);
			_label = _panel.Find("Label").GetComponent<Text>();
			_value = _panel.Find("Value").GetComponent<Text>();

			if (OptionValueTint != OptionValueTintShipEngineIndexForGame)
			{
				_label.color = GetTintColor(TextAlpha.ThreeQuarters);
				_value.color = GetTintColor(TextAlpha.NineTenths);
			}
			else
			{
				Color engineColor = GetShipRepresentativeColor(TargetShip);
				_label.color = GetTintFromColor(TextAlpha.ThreeQuarters, engineColor);
				_value.color = GetTintFromColor(TextAlpha.NineTenths, engineColor);
			}

			NgNetworkBase.CurrentNetwork.OnCountdownStarted += CountdownInitiate;
		}

		public override void Update()
		{
			base.Update();
			if (!_value.gameObject.activeSelf)
				return;

			if (_timer >= 0f)
			{
				_timer -= Time.deltaTime;
				if (_timer < 0f) _timer = 0f;
			}

			_value.text =
				(Math.Round(_timer * 100f) / 100.0).ToString("F2", CultureInfo.InvariantCulture);
		}

		private void CountdownInitiate(NgCountdownHeaders type)
		{
			if (type != NgCountdownHeaders.RaceFinish)
				return;

			_label.gameObject.SetActive(true);
			_value.gameObject.SetActive(true);

			_label.text = "race ends in";
			_timer = GetCountdownTime();
		}

		// can't access the actual value at the moment so here's a placeholder.
		private static float GetCountdownTime() => 30.0f;

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgNetworkBase.CurrentNetwork.OnCountdownStarted -= CountdownInitiate;
		}
	}

	public class ShifterHud : ScriptableHud
	{
		private int _playerIndex;
		private bool _isPlayerOne;

		public override void Start()
		{
			base.Start();
			/*
			 * When running this block while loading hud components for Player Two:
			 * 
			 * Player Two TargetShip: Ships.Loaded[1]
			 * Player Two TargetShip.ShipId == Player One TargetShip.ShipId: ｔｒｕｅ
			 * Player Two TargetShip.Is(Ships.PlayerOneShip): ｔｒｕｅ
			 * Player Two TargetShip.playerIndex: 1
			 */
			_playerIndex = TargetShip.playerIndex;
			_isPlayerOne = _playerIndex == 0;
			
			Shifter.TargetShips.Add(TargetShip);

			StartCoroutine(Shifter.Shift(_playerIndex));

			NgRaceEvents.OnShipExploded += Shifter.HideHud;
			NgRaceEvents.OnShipRespawn += Shifter.ShowHud;
		}

		public override void Update()
		{
			base.Update();
			Shifter.UpdateAmount(TargetShip);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			StopCoroutine(Shifter.Shift(_playerIndex));

			if (_isPlayerOne)
				Shifter.Flush();

			NgRaceEvents.OnShipExploded -= Shifter.HideHud;
			NgRaceEvents.OnShipRespawn -= Shifter.ShowHud;
		}
	}
}
