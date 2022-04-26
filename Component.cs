using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NgAudio;
using NgData;
using NgEvents;
using NgGame;
using NgLib;
using NgModes;
using NgMp;
using NgPickups;
using NgPickups.Physical;
using NgSettings;
using NgShips;
using NgSp;
using NgTrackData.Triggers;
using NgUi.RaceUi;
using NgUi.RaceUi.HUD;
using UnityEngine;
using UnityEngine.UI;
using static Streamliner.HudRegister;
using static Streamliner.PresetColorPicker;
using static Streamliner.SectionManager;

namespace Streamliner
{
	public class Speedometer : ScriptableHud
	{
		internal SpeedPanel Panel;
		private float _computedValue;

		private readonly Color _highlightColor = GetTintColor(clarity: 0);

		private float _currentSpeed;
		private float _previousSpeed;

		private const float AnimationSpeed = 8f;
		private const float AnimationTimerMax = 1.5f;
		private float _speedDecreaseAnimationTimer;
		private float _speedIncreaseAnimationTimer;

		public override void Start()
		{
			base.Start();
			Panel = new SpeedPanel(CustomComponents.GetById("Base"));
		}

		public override void Update()
		{
			base.Update();
			Panel.FillAccel(GetHudAccelWidth());
			Panel.Fill(GetHudSpeedWidth());
			Panel.Value.text = GetSpeedValueString();

			if (!OptionSpeedHighlight)
				return;

			_currentSpeed = Panel.CurrentSize.x;
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
		private string GetSpeedValueString() =>
			IntStrDb.GetNumber(Mathf.Abs(Mathf.RoundToInt(TargetShip.HudSpeed)));

		private void ColorSpeedComponent()
		{
			if (_currentSpeed < _previousSpeed)
			{
				_speedDecreaseAnimationTimer = AnimationTimerMax;
				_speedIncreaseAnimationTimer = 0f;
			}
			else
			{
				_speedIncreaseAnimationTimer = AnimationTimerMax;
				_speedDecreaseAnimationTimer = 0f;
			}

			Color color = Panel.Value.color;

			if (_speedDecreaseAnimationTimer > 0f)
			{
				color = Color.Lerp(color, _highlightColor, Time.deltaTime * AnimationSpeed);
				_speedDecreaseAnimationTimer -= Time.deltaTime;
			}
			else
				_speedDecreaseAnimationTimer = 0f;

			if (_speedIncreaseAnimationTimer > 0f)
			{
				color = Color.Lerp(color, Panel.GaugeColor, Time.deltaTime * AnimationSpeed);
				_speedIncreaseAnimationTimer -= Time.deltaTime;
			}
			else
				_speedIncreaseAnimationTimer = 0f;

			Panel.ChangeDataPartColor(color);
		}
	}

	public class EnergyMeter : ScriptableHud
	{
		internal RectTransform Panel;
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
		private const float SlowTransitionTimerMax = 2.2f;
		private const float RechargeDisplayTimerMax = 3.0f;
		private float _damageAnimationTimer;
		private float _transitionAnimationTimer;
		private float _deltaAnimationTimer;
		private float _rechargeDisplayTimer;

		public override void Start()
		{
			base.Start();
			_energyConstantlyDischarges =
				RaceManager.CurrentGamemode.Name == "Rush Hour";

			Panel = CustomComponents.GetById("Base");
			_value = Panel.Find("Value").GetComponent<Text>();
			_delta = Panel.Find("Delta").GetComponent<Text>();
			_gaugeBackground = Panel.Find("GaugeBackground").GetComponent<Image>();
			_gauge = (RectTransform)_gaugeBackground.GetComponent<RectTransform>()
				.Find("Gauge");
			_gaugeImage = _gauge.GetComponent<Image>();

			// Gauge is stored in its maximum size, store the max width here.
			_maxSize = _gauge.sizeDelta;

			// Initiate the gauge size.
			_currentSize = _maxSize;
			_gauge.sizeDelta = _currentSize;

			// Coloring
			_defaultColor = GetTintColor(TextAlpha.ThreeQuarters);
			_gaugeBackground.color = GetTintColor(TextAlpha.ThreeEighths);
			_currentColor = _defaultColor;
			_currentDamageColor = _damageColor;
			_value.color = _defaultColor;
			_gaugeImage.color = _defaultColor;
			_deltaColor = _delta.color;
			_deltaFinalColor = _delta.color;
			_deltaInactiveColor = _delta.color;
			_deltaFinalColor.a = DeltaFinalAlpha;
			_deltaInactiveColor.a = DeltaInactiveAlpha;
			_delta.color = _deltaInactiveColor;
		}

		public override void Update()
		{
			base.Update();
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
				if (_wasRecharging)
					_delta.text = ValueCharged();
				else
					ValueBeforeCharging = _previousValueString;
			}
			else if (_energyRegained)
				_delta.text = ValueGained();
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
				if (_rechargeDisplayTimer == _rechargeDisplayTimerMax)
					deltaColor = _deltaFinalColor;

				_rechargeDisplayTimer -= Time.deltaTime;
				if (_rechargeDisplayTimer <= 0f)
					deltaColor = _deltaInactiveColor;
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

				if (_damageAnimationTimer == _fastTransitionTimerMax)
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
	}

	public class Timer : ScriptableHud
	{
		internal BasicPanel Panel;
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

			public LapSlot(RectTransform template)
			{
				Value = template.Find("Time").GetComponent<Text>();
				_perfectLine = template.Find("PerfectLine").GetComponent<Image>();

				Value.text = "";
				Value.gameObject.SetActive(true);
				_perfectLapStatus = false;

				ChangeColor(GetTintColor(TextAlpha.ThreeQuarters));
			}

			internal void ChangeColor(Color color) => Value.color = color;
		}

		private void InitiateSlots()
		{
			_totalSlots = Mathf.Clamp(Race.MaxLaps, 0, 5);
			for (int i = 0; i < _totalSlots; i++)
			{
				RectTransform slot =
					Instantiate(_lapSlotTemplate.gameObject).GetComponent<RectTransform>();
				slot.SetParent(_lapSlotTemplate.parent);
				slot.localScale = _lapSlotTemplate.localScale;
				slot.anchoredPosition = _lapSlotTemplate.anchoredPosition;

				slot.localPosition += Vector3.up * slot.sizeDelta.y * i;

				_slots.Add(new LapSlot(slot));
			}

			// Emphasis the current lap slot by a bit.
			_slots[0].ChangeColor(GetTintColor(TextAlpha.NineTenths));
		}

		public override void Start()
		{
			base.Start();
			_totalLaps = Race.MaxLaps;
			_totalSections = GetTotalSectionCount();
			Panel = new BasicPanel(CustomComponents.GetById("Base"));
			_lapSlotTemplate = CustomComponents.GetById("LapSlot");
			// I am hiding the components here, not on Unity,
			// because I want to keep them visible on Unity.
			_lapSlotTemplate.Find("Time").gameObject.SetActive(false);
			_lapSlotTemplate.Find("PerfectLine").gameObject.SetActive(false);
			InitiateSlots();
			_currentSlot = _slots[0];

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
			ShiftSlotData();
			_currentLap++;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnShipLapUpdate -= OnLapUpdate;
		}

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
			Panel.Value.text = _bigTimeTextBuilder.ToString(TargetShip.TotalRaceTime);

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
			Panel.Fill(GetRaceCompletionRate(TargetShip, _currentLap, _totalSections));
		}
	}

	public class LapTimer : ScriptableHud
	{
		internal BasicPanel Panel;
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
			Panel = new BasicPanel(CustomComponents.GetById("Base"));
			RectTransform bestTimeSlot = CustomComponents.GetById("LapSlot");
			_bestTimeText = bestTimeSlot.Find("Time").GetComponent<Text>();
			bestTimeSlot.Find("PerfectLine").gameObject.SetActive(false);

			_usingBestTimeDisplay = OptionBestTime != 0;
			_bestTimeText.gameObject.SetActive(_usingBestTimeDisplay);

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
				Panel.Value.text = _bigTimeTextBuilder.ToString(-1f);
				Panel.Fill(0f);
				return;
			}

			Panel.Value.text = _bigTimeTextBuilder.ToString(_currentTime);

			if (TargetShip.CurrentSection is null)
				return;

			Panel.Fill(GetLapCompletionRate(TargetShip, _totalSections));
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

	public class TargetTime : ScriptableHud
	{
		private const string StringTimeTrial = "Time Trial";
		private const string StringSpeedLap = "Speed Lap";
		internal RectTransform Panel;
		private RectTransform _normalDisplay;
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
				StringSpeedLap => _isCampaign || OptionCountdownTimer ? DisplayType.Big : DisplayType.None,
				StringTimeTrial => _isCampaign || OptionCountdownTimer ? DisplayType.Both : DisplayType.Normal,
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
			Panel = CustomComponents.GetById("Base");
			_normalDisplay = CustomComponents.GetById("Normal");
			_normalDisplay.Find("Label").GetComponent<Text>().color = GetTintColor(TextAlpha.ThreeQuarters);
			_normalDisplayValue = _normalDisplay.Find("Value").GetComponent<Text>();
			_normalDisplayValue.color = GetTintColor();
			_bigDisplay = new DoubleGaugePanel(CustomComponents.GetById("Big"));
			_bigDisplay.SetFillStartingSide(DoubleGaugePanel.StartingPoint.Center);

			_gamemodeName = RaceManager.CurrentGamemode.Name;
			_isCampaign = NgCampaign.Enabled;
			SetTimeType(_gamemodeName);
			SetDisplayType(_gamemodeName);
			switch (_displayType)
			{
				case DisplayType.None:
					Panel.gameObject.SetActive(false);
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
					TargetShip.TargetTime / Race.MaxLaps,
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
				_bigDisplay.Value.text = _bigTimeTextBuilder.ToString(-1f);
				_bigDisplay.FillBoth(0f);
				return;
			}
			if (_targetTime <= 0f)
			{
				_bigDisplay.Value.text = _bigTimeTextBuilder.ToString(_currentTime);
				_bigDisplay.FillBoth(1f);
				return;
			}

			float timeLeft = _targetTime - _currentTime;
			float timeMax = _isCampaign ? _awardTimeDifference : _targetTime;
			if (_showingLapTimeAdvantage)
				timeLeft += _averageLapTimeAdvantage;
			timeLeft = timeLeft < 0f ? 0f : timeLeft;
			_bigDisplay.Value.text = _bigTimeTextBuilder.ToString(timeLeft);
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

	public class ZoneTracker : ScriptableHud
	{
		internal DoubleGaugePanel Panel;
		private Text _zoneName;
		private Text _zoneScore;

		public override void Start()
		{
			base.Start();
			Panel = new DoubleGaugePanel(CustomComponents.GetById("Base"));
			_zoneName = CustomComponents.GetById<Text>("Name");
			_zoneName.gameObject.SetActive(true);
			_zoneScore = CustomComponents.GetById<Text>("Score");
			_zoneScore.gameObject.SetActive(true);

			_zoneName.color = GetTintColor(TextAlpha.ThreeEighths);
			_zoneScore.color = GetTintColor(TextAlpha.NineTenths);

			_zoneScore.text = "0";
			Panel.Value.text = "0";
			_zoneName.text = "toxic";

			NgUiEvents.OnZoneProgressUpdate += SetProgress;
			NgUiEvents.OnZoneScoreUpdate += SetScore;
			NgUiEvents.OnZoneNumberUpdate += SetNumber;
			NgUiEvents.OnZoneTitleUpdate += SetTitle;
		}

		private void SetProgress(float progress) =>
			Panel.FillBoth(progress);

		private void SetScore(string score) =>
			_zoneScore.text = score;

		private void SetNumber(string number) =>
			Panel.Value.text = number;

		private void SetTitle(string title) =>
			_zoneName.text = title;

		public override void OnDestroy()
		{
			base.OnDestroy();

			NgUiEvents.OnZoneProgressUpdate -= SetProgress;
			NgUiEvents.OnZoneScoreUpdate -= SetScore;
			NgUiEvents.OnZoneNumberUpdate -= SetNumber;
			NgUiEvents.OnZoneTitleUpdate -= SetTitle;
		}
	}

	public class UpsurgeTracker : ScriptableHud
	{
		internal LayeredDoubleGaugePanel Panel;
		private RectTransform _energyInfo;
		private Text _valueZoneText;
		private Text _valueShieldText;
		private Animator _barrierWarning;
		private static readonly int WarnLeft = Animator.StringToHash("Left");
		private static readonly int WarnMiddle = Animator.StringToHash("Middle");
		private static readonly int WarnRight = Animator.StringToHash("Right");
		private GmUpsurge _gamemode;
		private UpsurgeShip _upsurgeTargetShip;
		private int _valueShield;
		private float _valueZoneTime;
		private const float TransitionSpeed = 8f;
		private const float TransitionTimerMax = 1.5f;
		private float _transitionTimer;
		private float _smallGaugeAlpha;
		private readonly Color _overflowZoneTimeColor = GetTintColor(tintIndex: 2, clarity: 5);
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

		public override void Start()
		{
			base.Start();
			Panel = new LayeredDoubleGaugePanel(CustomComponents.GetById("Base"), true);
			_energyInfo = CustomComponents.GetById("Energy");
			_energyInfo.gameObject.SetActive(true);
			_valueZoneText = _energyInfo.Find("ValueZone").GetComponent<Text>();
			_valueShieldText = _energyInfo.Find("ValueShield").GetComponent<Text>();
			_barrierWarning = CustomComponents.GetById<Animator>("Barrier");
			_barrierWarning.gameObject.SetActive(true);

			_currentSmallGaugeColor = Panel.SmallGaugeColor;
			_smallGaugeAlpha = Panel.SmallGaugeColor.a;

			Color infoLabelColor = GetTintColor(TextAlpha.ThreeEighths);
			Color infoValueColor = GetTintColor(TextAlpha.ThreeQuarters);
			_energyInfo.Find("LabelZone").GetComponent<Text>().color = infoLabelColor;
			_energyInfo.Find("LabelShield").GetComponent<Text>().color = infoLabelColor;
			_energyInfo.Find("ValueZone").GetComponent<Text>().color = infoValueColor;
			_energyInfo.Find("ValueShield").GetComponent<Text>().color = infoValueColor;

			_gamemode = (GmUpsurge) RaceManager.CurrentGamemode;

			UpsurgeShip.OnDeployedBarrier += StartTransition;
			UpsurgeShip.OnBuiltBoostStepsIncrease += StartTransition;
			UpsurgeShip.OnShieldActivated += StartTransition;
			Barrier.OnPlayerBarrierWarned += WarnBarrier;
		}

		private void StartTransition(ShipController ship)
		{
			if (_upsurgeTargetShip == null || ship != _upsurgeTargetShip.TargetShip)
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
			Panel.Value.text = _upsurgeTargetShip.CurrentZone.ToString();
			_valueZoneText.text = "+" + _upsurgeTargetShip.BuiltZones;
			_valueShieldText.text = "+" + _valueShield;

			Panel.FillBoth(_currentZoneWidth);
			Panel.FillSecondGauges(_currentShieldWidth);
			Panel.FillSmallGauges(_currentZoneTimeWidth);
			Panel.ChangeSmallGaugesColor(_currentSmallGaugeColor);
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
			_currentSmallGaugeColor = Panel.SmallGaugeColor;
			_currentZoneTimeWidth = 0f;
			_playingOverflowTransition = false;
		}

		public override void Update()
		{
			base.Update();
			if (_upsurgeTargetShip == null)
			{
				_upsurgeTargetShip = _gamemode.Ships.Find(ship => ship.TargetShip == TargetShip);
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
			UpsurgeShip.OnDeployedBarrier -= StartTransition;
			UpsurgeShip.OnBuiltBoostStepsIncrease -= StartTransition;
			UpsurgeShip.OnShieldActivated -= StartTransition;
			Barrier.OnPlayerBarrierWarned -= WarnBarrier;
		}
	}

	public class Placement : ScriptableHud
	{
		internal FractionPanel Panel;
		private bool _warnOnLastPlace;
		private bool _onWarning;
		private readonly Color _highlightColor = GetTintColor(tintIndex: 1, clarity: 4);
		private float _warningTimer;
		private float _warningSin;
		private bool _playedWarningSound;

		public override void Start()
		{
			base.Start();
			Panel = new FractionPanel(CustomComponents.GetById("Base"));
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

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			StartCoroutine(UpdateData());
		}

		private IEnumerator UpdateData()
		{
			while (true)
			{
				int place = TargetShip.CurrentPlace;
				int maxPlace = Ships.Active.Count;
				_onWarning = _warnOnLastPlace && place == maxPlace;
				Panel.Value.text = IntStrDb.GetNoSingleCharNumber(place);
				Panel.MaxValue.text = IntStrDb.GetNoSingleCharNumber(maxPlace);
				Panel.Fill(maxPlace == 1 ?
					0f : (float) (maxPlace - place) / (maxPlace - 1));
				Panel.ChangeDataPartColor(_onWarning ? _highlightColor : Panel.GaugeColor);

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
			NgRaceEvents.OnCountdownStart -= Initiate;
			StopCoroutine(UpdateData());
		}
	}

	public class LapCounter : ScriptableHud
	{
		internal FractionPanel Panel;

		public override void Start()
		{
			base.Start();
			Panel = new FractionPanel(CustomComponents.GetById("Base"))
			{
				Value = { text = IntStrDb.GetNoSingleCharNumber(0) },
				MaxValue = { text = IntStrDb.GetNoSingleCharNumber(Race.MaxLaps) }
			};
		}

		public override void Update()
		{
			base.Update();
			Panel.Value.text = IntStrDb.GetNoSingleCharNumber(TargetShip.CurrentLap);
			Panel.Fill((float) TargetShip.CurrentLap / Race.MaxLaps);
		}
	}

	public class PositionTracker : ScriptableHud
	{
		private const float AlphaEliminated = 0.5f;
		private int _totalSections;
		private int _halfTotalSections;
		private EPosHudMode _previousMode;
		private bool _initiated;
		private bool _modeChanged;

		internal RectTransform Panel;
		private ShipNode _singleNode;
		private List<ShipNode> _nodes;
		private int[] _racerSectionsTraversed;
		private List<RawValuePair> _racerRelativeSections;

		private class ShipNode
		{
			internal static RectTransform Template;
			internal static float MaxSize;
			// speed of 4 makes it nearly in sync with the placement component
			private const int TransitionSpeed = 4;
			internal int Id;
			private readonly RectTransform _node;
			private readonly Image _nodeImage;
			private float _currentPositionRate;
			private Vector2 _position;

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

			public ShipNode(Color color, int id = -1)
			{
				Id = id;
				_node = Instantiate(Template, Template.parent);
				_node.localScale = Template.localScale;
				_node.anchoredPosition = Template.anchoredPosition;
				_nodeImage = _node.GetComponent<Image>();
				_nodeImage.color = color;
				SetPosition(0.5f, true);
			}
		}

		// is this really the best way for a list of int pairs updating frequently?
		private class RawValuePair
		{
			public readonly int Id;
			public int Value;

			public RawValuePair(int id, int value)
			{
				Id = id;
				Value = value;
			}
		}

		public override void Start()
		{
			base.Start();
			Panel = CustomComponents.GetById("Base");
			ShipNode.Template = Panel.Find("Square").GetComponent<RectTransform>();
			ShipNode.MaxSize = Panel.sizeDelta.x - ShipNode.Template.sizeDelta.x;

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			_singleNode = new ShipNode(GetTintColor(tintIndex: 2, clarity: 1));

			int totalShips = Ships.Loaded.Count;
			_nodes = new List<ShipNode>(totalShips);
			_racerSectionsTraversed = new int[totalShips];
			_racerRelativeSections = new List<RawValuePair>(totalShips);

			foreach (ShipController ship in Ships.Loaded)
			{
				if (ship == TargetShip)
				{
					_nodes.Add(new ShipNode(GetTintColor(clarity: 1), ship.ShipId));
				}
				else
				{
					Color.RGBToHSV(
						Color.Lerp(
								ship.Settings.REF_ENGINECOL_BRIGHT, ship.Settings.REF_ENGINECOL, 0.5f
							) with
							{
								a = 1f
							},
						out float h, out float s, out float v
					);
					s *= 1.1f;

					_nodes.Add(new ShipNode(Color.HSVToRGB(h, s, v), ship.ShipId));
				}
				_racerSectionsTraversed[ship.ShipId] = 0;
				_racerRelativeSections.Add(new RawValuePair(ship.ShipId, 0));
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
			ShipNode.Template.SetSiblingIndex(totalShips + 1);
			ShipNode.Template.gameObject.SetActive(false);
			_nodes[TargetShip.ShipId].SiblingIndex = totalShips;
			_singleNode.SiblingIndex = totalShips - 1;
			_totalSections = GetTotalSectionCount();
			_halfTotalSections = _totalSections / 2;

			StartCoroutine(UpdateSectionsTraversed());

			_initiated = true;
			NgRaceEvents.OnCountdownStart -= Initiate;
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
				_previousMode = Hud.PositionTrackerHudMode;
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
						node.Enabled = false;
					break;
			}
		}

		private IEnumerator UpdateSectionsTraversed()
		{
			while (true)
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
						GetPassingSectionIndex(ship, ship.CurrentLap, _totalSections);
				}

				int playerSection = _racerSectionsTraversed[TargetShip.ShipId];
				for (int id = 0; id < _racerSectionsTraversed.Length; id++)
				{
					_racerRelativeSections[id].Value =
						_racerSectionsTraversed[id] - playerSection;
				}

				yield return new WaitForSeconds(Position.UpdateTime);
			}
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
			_singleNode.Id =
				Ships.FindShipInPlace(TargetShip.CurrentPlace == 1 ? 2 : 1).ShipId;
			if (!Ships.Loaded[_singleNode.Id])
				return;

			_singleNode.SetPosition(ConvertDistanceRate(
				(float) _racerRelativeSections[_singleNode.Id].Value / _halfTotalSections
			));
		}

		private void SetMultipleNodes()
		{
			List<RawValuePair> orderedList =
				_racerRelativeSections.OrderByDescending(p => p.Value).ToList();
			int indexLastShipAlive = orderedList.Count - 1;

			while (Ships.Loaded[orderedList[indexLastShipAlive].Id].Eliminated)
				if (--indexLastShipAlive < 0)
				{
					indexLastShipAlive = 0;
					break;
				}

			int endDistance = Math.Max(
				orderedList[0].Value, // >=0
				Math.Abs(orderedList[indexLastShipAlive].Value) // <= 0
			);

			int siblingIndex = 0;
			bool siblingIndexUpdateFromTop = true;
			foreach (RawValuePair p in orderedList)
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
					AlphaEliminated : 1f;
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
				_nodes[p.Id].SetPosition(
					ConvertDistanceRate((float) p.Value / endDistance)
				);
			}
		}

		private static float ConvertDistanceRate(float distanceRate) =>
			(float) ( ( Math.Sin(distanceRate*Math.PI/2) + 1 ) / 2 );

		public override void OnDestroy()
		{
			base.OnDestroy();
			StopCoroutine(UpdateSectionsTraversed());
		}
	}

	public class Pitlane : ScriptableHud
	{
		internal RectTransform Panel;
		private Animator _panelAnimator;
		private static readonly int Active = Animator.StringToHash("Active");
		private static readonly int PointRight = Animator.StringToHash("Point Right");

		public override void Start()
		{
			base.Start();
			Panel = CustomComponents.GetById("Base");
			_panelAnimator = Panel.GetComponent<Animator>();

			Panel.Find("Left").Find("Text").GetComponent<Text>().color = GetTintColor();
			Panel.Find("Right").Find("Text").GetComponent<Text>().color = GetTintColor();

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

	public class MessageLogger : ScriptableHud
	{
		internal RectTransform Panel;
		private CanvasGroup _timeGroup;
		private Text _timeDiff;
		private Text _lapResult;
		private Text _finalLap;
		private RectTransform _lineTemplate;
		private Text _nowPlaying;
		private Text _wrongWay;
		private bool _initiated;

		private const int LineMax = 3;
		private const float LapResultLineHeight = 22f; // at hud font size 15
		private const float DisplayTimeMax = 3.0f;
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

		private static readonly Dictionary<string, Color> TextColor = new()
		{
			{ "75", GetTintColor(TextAlpha.ThreeQuarters) },
			{ "90", GetTintColor(TextAlpha.NineTenths) },
			{ "100", GetTintColor() },
			{ "red", GetTintColor(TextAlpha.NineTenths, 1, 1) },
			{ "green", GetTintColor(TextAlpha.NineTenths, 5, 1) },
			{ "magenta", GetTintColor(TextAlpha.NineTenths, 11, 1) },
			{ "cyan", GetTintColor(TextAlpha.NineTenths, 7, 1) },
			{ "empty", GetTintColor(TextAlpha.Zero) }
		};
		private static readonly Dictionary<string, Color> DefaultColor = new()
		{
			{ "TimeDiff", TextColor["90"] },
			{ "LapResult", TextColor["90"] },
			{ "FinalLap", TextColor["90"] },
			{ "Line", TextColor["90"] },
			{ "NowPlaying", TextColor["90"] },
			{ "WrongWay", TextColor["100"] }
		};

		private readonly List<Line> _lines = new(LineMax);
		private class Line
		{
			internal readonly Text Value;
			public float Alpha
			{
				set
				{
					Color color = Value.color;
					color.a = value;
					Value.color = color;
				}
				get => Value.color.a;
			}

			public Line(RectTransform template)
			{
				Value = template.GetComponent<Text>();
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

		private bool _facingBackwardExpected;
		private bool _noNewLapRecord;

		public override void Start()
		{
			base.Start();
			string _gamemodeName = RaceManager.CurrentGamemode.Name;
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

			NgRaceEvents.OnCountdownStart += Initiate;

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
			Panel = CustomComponents.GetById("Base");
			RectTransform timeGroupRT = CustomComponents.GetById("TimeGroup");
			_timeGroup = timeGroupRT.GetComponent<CanvasGroup>();
			_timeDiff = timeGroupRT.Find("Difference").GetComponent<Text>();
			_lapResult = timeGroupRT.Find("LapResult").GetComponent<Text>();
			_finalLap = timeGroupRT.Find("FinalLap").GetComponent<Text>();
			_lineTemplate = CustomComponents.GetById("MessageLine");
			Text lineTemplateText = _lineTemplate.GetComponent<Text>();
			_nowPlaying = CustomComponents.GetById<Text>("NowPlaying");
			_wrongWay = CustomComponents.GetById<Text>("WrongWay");

			_timeDiff.color = DefaultColor["TimeDiff"];
			_lapResult.color = DefaultColor["LapResult"];
			_finalLap.color = DefaultColor["FinalLap"];
			lineTemplateText.color = DefaultColor["Line"];
			_nowPlaying.color = DefaultColor["NowPlaying"];
			_wrongWayCurrentColor = DefaultColor["WrongWay"];
			_wrongWayCurrentColor.a = _wrongWayCurrentAlpha;
			_wrongWay.color = _wrongWayCurrentColor;
			_wrongWay.text = "wrong way";

			if (Audio.Levels.MusicVolume == 0f)
				_nowPlaying.gameObject.SetActive(false);

			_timeDiff.text = "";
			_lapResult.text = "";
			_finalLap.text = "";
			lineTemplateText.text = "";

			if (_noNewLapRecord)
				_lineTemplate.parent.GetComponent<RectTransform>().anchoredPosition +=
					Vector2.down * LapResultLineHeight;

			InitiateLines();
			_lineTemplate.gameObject.SetActive(false);

			_initiated = true;
			NgRaceEvents.OnCountdownStart -= Initiate;
		}

		private void InitiateLines()
		{
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
		}

		private void AddMessage(string message, ShipController ship, Color color)
		{
			color =
				color == Color.green ?
				TextColor["green"] :
				color == Color.red ?
					TextColor["red"] :
					DefaultColor["Line"];

			StringKind kind = GetStringKind(message);

			if (kind != StringKind.General)
			{
				color = OptionTimeDiffColour switch
				{
					2 when color == TextColor["green"] => TextColor["cyan"],
					1 when color == TextColor["red"] => TextColor["magenta"],
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
						_timeDiff.text = message + "<color=#0000>-</color>";
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
				_timeDisplayTime = DisplayTimeMax;
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
			_nowPlaying.color = DefaultColor["NowPlaying"];
			_npDisplayTime = DisplayTimeMax;
			_npFadeOutTimeRemaining = 0f;
		}

		private void AddEliminationMessage(ShipController ship)
		{
			if (
				!RaceManager.CurrentGamemode.Configuration.KillFeedEnabled ||
				NgNetworkBase.CurrentNetwork is not null
			)
				return;

			string message =
				ship.LastAttacker is not null ?
					ship.LastAttacker.ShipName + " eliminated " + ship.ShipName :
					ship.ShipName + " eliminated";
			AddMessage(message, ship, Color.red);
		}

		private void InsertMessageLine(string message, Color color)
		{
			Line line;
			for (int i = _lines.Count - 1; i > 0; i--)
			{
				line = _lines[i];
				Line lineBelow = _lines[i - 1];

				line.Value.text = lineBelow.Value.text;
				line.Value.color = lineBelow.Value.color;
				_lineDisplayTime[i] = _lineDisplayTime[i - 1];
				_lineFadeOutTimeRemaining[i] = _lineFadeOutTimeRemaining[i - 1];
			}

			line = _lines[0];
			line.Value.text = message;
			line.Value.color = color;
			_lineDisplayTime[0] = DisplayTimeMax;
			_lineFadeOutTimeRemaining[0] = 0f;
		}

		public override void Update()
		{
			base.Update();
			if (!_initiated)
				return;

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
					_lines[i].Value.text = "";
					_lines[i].Value.color = DefaultColor["Line"];
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
				_timeDiff.color = DefaultColor["TimeDiff"];
				_lapResult.color = DefaultColor["LapResult"];
				_finalLap.color = DefaultColor["FinalLap"];
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
					_nowPlaying.color = DefaultColor["NowPlaying"];
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
				_wrongWayCurrentColor.a = _wrongWayCurrentAlpha;
				_wrongWay.color = _wrongWayCurrentColor;
				_wrongWayFadeTimeRemaining -= Time.deltaTime;
			}
			else
			{
				_wrongWayCurrentColor.a = _wrongWayAlpha;
				_wrongWay.color = _wrongWayCurrentColor;
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
				if (_timeDisplayTime == DisplayTimeMax)
				{
					_timeGroup.alpha = 1f;
					break;
				}

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
				if (_npDisplayTime == DisplayTimeMax)
				{
					_nowPlaying.color = DefaultColor["NowPlaying"];
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

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnMidLineTriggered -= FlushTimeGroupTexts;
			NgUiEvents.OnTriggerMessage -= AddMessage;
			NgRaceEvents.OnShipExploded -= AddEliminationMessage;
			NgUiEvents.OnNewSongPlaying -= AddSong;
		}
	}

	public class PickupDisplay : ScriptableHud
	{
		internal RectTransform Panel;
		private PickupPanel _playerPanel;
		private PickupPanel _warningPanel;
		private float _warningTimer;
		private const float WarningTimeMax = 2.5f;

		public override void Start()
		{
			base.Start();
			Panel = CustomComponents.GetById("Base");
			_playerPanel = new PickupPanel(
				Panel.Find("IconBackground").GetComponent<RectTransform>(),
				Panel.Find("Info").GetComponent<Text>());
			_warningPanel = new PickupPanel(
				Panel.Find("WarningBackground").GetComponent<RectTransform>());

			PickupBase.OnPickupInit += ShowPickup;
			PickupBase.OnPickupDeinit += HidePickup;
			NgUiEvents.OnWeaponWarning += Warn;
		}

		public void ShowPickup(PickupBase pickup, ShipController ship)
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

		public void HidePickup(PickupBase pickup, ShipController ship)
		{
			if (ship != TargetShip)
				return;
			if(_playerPanel.CurrentTransition is not null)
				StopCoroutine(_playerPanel.CurrentTransition);
			_playerPanel.CurrentTransition =
				StartCoroutine(_playerPanel.ColorFade(false));
		}

		public void Warn(Pickup pickup)
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

	public class TurboDisplay : ScriptableHud
	{
		internal RectTransform Panel;
		private PickupPanel _playerPanel;

		public override void Start()
		{
			base.Start();
			Panel = CustomComponents.GetById("Base");
			_playerPanel = new PickupPanel(Panel);

			PickupBase.OnPickupInit += ShowPickup;
			PickupBase.OnPickupDeinit += HidePickup;
		}

		public void ShowPickup(PickupBase pickup, ShipController ship)
		{
			if (ship != TargetShip)
				return;
			_playerPanel.UpdateSprite(ship.CurrentPickupRegister.Name);
			if(_playerPanel.CurrentTransition is not null)
				StopCoroutine(_playerPanel.CurrentTransition);
			_playerPanel.CurrentTransition =
				StartCoroutine(_playerPanel.ColorFade(true));
		}

		public void HidePickup(PickupBase pickup, ShipController ship)
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

	public class Leaderboard : ScriptableHud
	{
		internal Playerboard Panel;

		public override void Start()
		{
			base.Start();
			Panel = new Playerboard(CustomComponents.GetById("Base"),
				RaceManager.CurrentGamemode.Name);

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		private void Initiate()
		{
			/*
			 * CurrentGamemode can be Gamemode or inheritances of it.
			 * Accessing it through RaceManager instead of
			 * assigning it to a field can make it easy to
			 * access common fields.
			 */
			Panel.InitiateLayout(RaceManager.CurrentGamemode.TargetScore);
			Panel.InitiateSlots(Ships.Loaded);
			StartCoroutine(Panel.Update(Ships.Loaded));

			NgRaceEvents.OnCountdownStart -= Initiate;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			StopCoroutine(Panel.Update(Ships.Loaded));
		}
	}

	public class TeamScoreboard : ScriptableHud
	{}

	public class Awards : ScriptableHud
	{
		private const float InitialPanelAlpha = 0.9f;
		private const float ActivePanelAlpha = 1f;
		private const float TemporarilyInactivePanelAlpha = 0.5f;
		private const float InactivePanelAlpha = 0.25f;

		internal RectTransform Panel;
		internal CanvasGroup PanelPlatinum;
		internal Image PanelPlatinumImage;
		internal Text TextPlatinum;
		internal CanvasGroup PanelGold;
		internal Image PanelGoldImage;
		internal Text TextGold;
		internal CanvasGroup PanelSilver;
		internal Image PanelSilverImage;
		internal Text TextSilver;
		internal CanvasGroup PanelBronze;
		internal Image PanelBronzeImage;
		internal Text TextBronze;

		private readonly Color _activePlatinumColor =
			GetTintColor(TextAlpha.ThreeQuarters, 7, 0);
		private readonly Color _activeGoldColor =
			GetTintColor(TextAlpha.ThreeQuarters, 3, 0);
		private readonly Color _activeSilverColor =
			GetTintColor(TextAlpha.ThreeQuarters, 0, 0);
		private readonly Color _activeBronzeColor =
			GetTintColor(TextAlpha.ThreeQuarters, 2, 0);
		private readonly Color _activeTextColor =
			GetPanelColor() with {a = GetTransparency(TextAlpha.Full)};

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
			Panel = CustomComponents.GetById("Base");
			PanelPlatinum = Panel.Find("Platinum").GetComponent<CanvasGroup>();
			PanelPlatinumImage = PanelPlatinum.GetComponent<Image>();
			TextPlatinum = Panel.Find("Platinum").Find("Value").GetComponent<Text>();
			PanelGold = Panel.Find("Gold").GetComponent<CanvasGroup>();
			PanelGoldImage = PanelGold.GetComponent<Image>();
			TextGold = Panel.Find("Gold").Find("Value").GetComponent<Text>();
			PanelSilver = Panel.Find("Silver").GetComponent<CanvasGroup>();
			PanelSilverImage = PanelSilver.GetComponent<Image>();
			TextSilver = Panel.Find("Silver").Find("Value").GetComponent<Text>();
			PanelBronze = Panel.Find("Bronze").GetComponent<CanvasGroup>();
			PanelBronzeImage = PanelBronze.GetComponent<Image>();
			TextBronze = Panel.Find("Bronze").Find("Value").GetComponent<Text>();

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
				PanelPlatinum.GetComponent<RectTransform>().anchoredPosition += Vector2.left * shiftAmount;
				PanelGold.GetComponent<RectTransform>().anchoredPosition += Vector2.left * shiftAmount;
				PanelSilver.GetComponent<RectTransform>().anchoredPosition += Vector2.right * shiftAmount;
				PanelBronze.GetComponent<RectTransform>().anchoredPosition += Vector2.right * shiftAmount;
				_missedPanelAlpha = TemporarilyInactivePanelAlpha;
				NgUiEvents.OnGamemodeUpdateCurrentLapTime += UpdateTime;
				NgUiEvents.OnGamemodeInvalidatedLap += InvalidateLap;
				NgRaceEvents.OnShipLapUpdate += ResetPanelAlpha;
			}

			if (_targetIsTime)
			{
				TextPlatinum.text = FloatToTime.Convert(_platinumTarget, TimeFormat);
				TextGold.text = FloatToTime.Convert(_goldTarget, TimeFormat);
				TextSilver.text = FloatToTime.Convert(_silverTarget, TimeFormat);
				TextBronze.text = FloatToTime.Convert(_bronzeTarget, TimeFormat);
			}
			else
			{
				TextPlatinum.text = "Zone " + _platinumTarget;
				TextGold.text = "Zone " + _goldTarget;
				TextSilver.text = "Zone " + _silverTarget;
				TextBronze.text = "Zone " + _bronzeTarget;
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
				PanelPlatinum.alpha = ActivePanelAlpha;
			}
			else if (_progressTime <= _goldTarget)
			{
				PanelPlatinum.alpha = _missedPanelAlpha;
				PanelGold.alpha = ActivePanelAlpha;
			}
			else if (_progressTime <= _silverTarget)
			{
				PanelGold.alpha = _missedPanelAlpha;
				PanelSilver.alpha = ActivePanelAlpha;
			}
			else if (_progressTime <= _bronzeTarget)
			{
				PanelSilver.alpha = _missedPanelAlpha;
				PanelBronze.alpha = ActivePanelAlpha;
			}
			else
			{
				PanelBronze.alpha = _missedPanelAlpha;
			}
		}

		private void InvalidateLap()
		{
			PanelPlatinum.alpha = _missedPanelAlpha;
			PanelGold.alpha = _missedPanelAlpha;
			PanelSilver.alpha = _missedPanelAlpha;
			PanelBronze.alpha = _missedPanelAlpha;
		}

		private void ResetPanelAlpha(ShipController ship)
		{
			PanelPlatinum.alpha = InitialPanelAlpha;
			PanelGold.alpha = InitialPanelAlpha;
			PanelSilver.alpha = InitialPanelAlpha;
			PanelBronze.alpha = InitialPanelAlpha;
		}

		private void UpdateZone(string number)
		{
			_progressZone = Convert.ToInt32(number);

			if (_progressZone >= _bronzeTarget)
			{
				PanelBronzeImage.color = _activeBronzeColor;
				TextBronze.color = _activeTextColor;
			}
			if (_progressZone >= _silverTarget)
			{
				PanelSilverImage.color = _activeSilverColor;
				TextSilver.color = _activeTextColor;
			}
			if (_progressZone >= _goldTarget)
			{
				PanelGoldImage.color = _activeGoldColor;
				TextGold.color = _activeTextColor;
			}
			if (_progressZone >= _platinumTarget)
			{
				PanelPlatinumImage.color = _activePlatinumColor;
				TextPlatinum.color = _activeTextColor;
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
}
