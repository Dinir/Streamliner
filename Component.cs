using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NgAudio;
using UnityEngine;
using UnityEngine.UI;
using NgData;
using NgSettings;
using NgUi.RaceUi;
using NgEvents;
using NgGame;
using NgLib;
using NgModes;
using NgShips;
using NgSp;
using NgUi.RaceUi.HUD;
using static Streamliner.HudRegister;
using static Streamliner.PresetColorPicker;
using static Streamliner.SectionManager;

namespace Streamliner
{
	public class Speedometer : ScriptableHud
	{
		internal SpeedPanel Panel;
		private float _computedValue;

		private readonly Color _highlightColor = GetTintColor(brightness: 0);

		private float _currentSpeed;
		private float _previousSpeed;

		private readonly float _animationSpeed = 8f;
		private readonly float _animationTimerMax = 1.5f;
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
				color = Color.Lerp(color, _highlightColor, Time.deltaTime * _animationSpeed);
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
		internal RectTransform Panel;
		internal Text Value;
		internal Text Delta;
		internal Image GaugeBackground;
		internal RectTransform Gauge;
		private Vector2 _maxSize;
		private Vector2 _currentSize;
		private float _computedValue;
		private float _adjustedDamageMult;
		private int _valueBeforeCharging;
		internal string ValueBeforeCharging
		{
			get => _valueBeforeCharging.ToString();
			set => _valueBeforeCharging = Convert.ToInt32(value);
		}
		internal string ValueCharged()
		{
			int value = Convert.ToInt32(Value.text) - _valueBeforeCharging;
			return IntStrDb.GetNumber(value);
		}
		internal string ValueGained()
		{
			int value = Convert.ToInt32(Value.text) - Convert.ToInt32(_previousValueString);
			return IntStrDb.GetNumber(value);
		}

		private float _currentEnergy;
		private float _previousEnergy;
		private string _previousValueString;
		private bool _isRecharging;
		private bool _wasRecharging;
		private bool _energyRegained;
		private bool _energyConstantlyDischarges;

		private readonly Color _rechargeColor = GetTintColor(TextAlpha.ThreeQuarters, 7, 3);
		private readonly Color _lowColor = GetTintColor(TextAlpha.ThreeQuarters, 2, 3);
		private readonly Color _criticalColor = GetTintColor(TextAlpha.ThreeQuarters, 1, 3);
		private readonly Color _damageColor = GetTintColor(tintIndex: 1, brightness: 4);
		private readonly Color _damageLowColor = GetTintColor(tintIndex: 3, brightness: 4);

		private Color _defaultColor;
		private Color _currentColor;
		private Color _currentDamageColor;
		private Color _deltaColor;
		private Color _deltaFinalColor;
		private Color _deltaInactiveColor;
		private readonly float _deltaFinalAlpha = 0.9f;
		private readonly float _deltaInactiveAlpha = 0f;

		/*
		 * When would the progress reach 0.99999 (1 - 10^-5) with the speed?
		 * That's log(10^-5) / 60log(1-(s/60)) seconds.
		 * When would the progress reach 0.75 (1 - 0.25) which can be considered gone on eyes?
		 * That's log(0.25) / 60log(1-(s/60)) seconds.
		 *
		 * Speed is decided on the 0.75 time, and Timer is decided on 0.99999 time of that speed.
		 * https://www.desmos.com/calculator/nip7pyehxl
		 */
		private readonly float _fastTransitionSpeed = 8f;
		private readonly float _slowTransitionSpeed = 5f;
		private readonly float _fastTransitionTimerMax = 1.5f;
		private readonly float _slowTransitionTimerMax = 2.2f;
		private readonly float _rechargeDisplayTimerMax = 3.0f;
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
			Value = Panel.Find("Value").GetComponent<Text>();
			Delta = Panel.Find("Delta").GetComponent<Text>();
			GaugeBackground = Panel.Find("GaugeBackground").GetComponent<Image>();
			Gauge = (RectTransform)GaugeBackground.GetComponent<RectTransform>()
				.Find("Gauge");

			// Gauge is stored in its maximum size, store the max width here.
			_maxSize = Gauge.sizeDelta;

			// Initiate the gauge size.
			_currentSize = _maxSize;
			Gauge.sizeDelta = _currentSize;

			// Coloring
			_defaultColor = GetTintColor(TextAlpha.ThreeQuarters);
			GaugeBackground.color = GetTintColor(TextAlpha.ThreeEighths);
			_currentColor = _defaultColor;
			_currentDamageColor = _damageColor;
			Value.color = _defaultColor;
			Gauge.GetComponent<Image>().color = _defaultColor;
			_deltaColor = Delta.color;
			_deltaFinalColor = Delta.color;
			_deltaInactiveColor = Delta.color;
			_deltaFinalColor.a = _deltaFinalAlpha;
			_deltaInactiveColor.a = _deltaInactiveAlpha;
			Delta.color = _deltaInactiveColor;
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
			Gauge.sizeDelta = _currentSize;
			Value.text = GetShieldValueString();

			if (!OptionEnergyChange && OptionLowEnergy == 0 && !OptionRechargeAmount)
				return;

			if (_isRecharging)
			{
				if (_wasRecharging)
					Delta.text = ValueCharged();
				else
					ValueBeforeCharging = _previousValueString;
			}
			else if (_energyRegained)
				Delta.text = ValueGained();
			ColorEnergyComponent();

			_previousEnergy = _currentEnergy;
			_previousValueString = Value.text;
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
				_damageAnimationTimer = _fastTransitionTimerMax;
			// transition
			if (
				OptionLowEnergy != 0 && (
					(_currentEnergy <= 25f && _previousEnergy > 25f) ||
					(_currentEnergy <= 10f && _previousEnergy > 10f) ||
					(_currentEnergy > 25f && _previousEnergy <= 25f) ||
					(_currentEnergy > 10f && _previousEnergy <= 10f)
				) ||
				_isRecharging || _energyRegained
			)
			{
				_transitionAnimationTimer = _slowTransitionTimerMax;
				// Charging takes over damage flash and recharge amount display
				if (_isRecharging || _energyRegained)
				{
					if (OptionRechargeAmount)
						_deltaAnimationTimer = _slowTransitionTimerMax;
					_rechargeDisplayTimer = 0f;
					_damageAnimationTimer = 0f;
				}
			}

			Color color = Value.color;

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
					color, _currentColor, Time.deltaTime * _slowTransitionSpeed);
				_transitionAnimationTimer -= Time.deltaTime;
			}
			else
				_transitionAnimationTimer = 0f;

			// recharging amount transition
			Color deltaColor = Delta.color;
			if (_deltaAnimationTimer > 0f)
			{
				if (_isRecharging || _energyRegained)
				{
					deltaColor = _wasRecharging ?
						Color.Lerp(
							deltaColor, _deltaColor, Time.deltaTime * _slowTransitionSpeed
						) :
						_deltaInactiveColor;
				}
				else
				{
					// Recharging is done here, as the timer is only set when recharging starts.
					// Stop this block and start the display block.
					_rechargeDisplayTimer = _rechargeDisplayTimerMax;
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
					(OptionLowEnergy == 1 && !Audio.WarnOfCriticalEnergy) ?
						_damageColor : _damageLowColor;

				if (_damageAnimationTimer == _fastTransitionTimerMax)
					color = _currentDamageColor;
				color = Color.Lerp(
					color, _currentColor, Time.deltaTime * _fastTransitionSpeed);
				_damageAnimationTimer -= Time.deltaTime;
			}
			else
				_damageAnimationTimer = 0f;

			// Apply the final color
			Value.color = color;
			Gauge.GetComponent<Image>().color = color;
			Delta.color = deltaColor;
		}
	}

	public class Timer : ScriptableHud
	{
		internal BasicPanel Panel;
		internal RectTransform LapSlotTemplate;

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
					_perfectLine.gameObject.SetActive(value: value);
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
					Instantiate(LapSlotTemplate.gameObject).GetComponent<RectTransform>();
				slot.SetParent(LapSlotTemplate.parent);
				slot.localScale = LapSlotTemplate.localScale;
				slot.anchoredPosition = LapSlotTemplate.anchoredPosition;

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
			LapSlotTemplate = CustomComponents.GetById("LapSlot");
			// I am hiding the components here, not on Unity,
			// because I want to keep them visible on Unity.
			LapSlotTemplate.Find("Time").gameObject.SetActive(false);
			LapSlotTemplate.Find("PerfectLine").gameObject.SetActive(false);
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
		private RectTransform _bestTimeSlot;
		private Text _bestTime;
		private int _totalSections;

		private readonly BigTimeTextBuilder _bigTimeTextBuilder = new(new StringBuilder());

		// NgEvents.NgUiEventsOnGamemodeUpdateCurrentLapTime
		// NgEvents.NgUiEvents.OnGamemodeInvalidatedLap
		public override void Start()
		{
			base.Start();
			_totalSections = GetTotalSectionCount();
			Panel = new BasicPanel(CustomComponents.GetById("Base"));
			_bestTimeSlot = CustomComponents.GetById("LapSlot");
			_bestTime = _bestTimeSlot.Find("Time").GetComponent<Text>();
			_bestTimeSlot.Find("PerfectLine").gameObject.SetActive(false);
		}

		public override void Update()
		{
			base.Update();
			UpdateTotalTime();
		}

		private void UpdateTotalTime()
		{
			// Panel.Value.text = _bigTimeTextBuilder.ToString(TargetShip.TotalRaceTime);
		}

		private void UpdateBestTime()
		{
			// _bestTime.text = FloatToTime.Convert(TargetShip.CurrentLapTime, TimeFormat);
		}
	}

	public class TargetTime : ScriptableHud
	{
		private const string StringTimeTrial = "Time Trial";
		private const string StringSpeedLap = "Speed Lap";
		internal RectTransform Panel;
		internal RectTransform NormalDisplay;
		internal Text NormalDisplayValue;
		internal DoubleGaugePanel BigDisplay;

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
			NormalDisplay = CustomComponents.GetById("Normal");
			NormalDisplay.Find("Label").GetComponent<Text>().color = GetTintColor(TextAlpha.ThreeQuarters);
			NormalDisplayValue = NormalDisplay.Find("Value").GetComponent<Text>();
			NormalDisplayValue.color = GetTintColor();
			BigDisplay = new DoubleGaugePanel(CustomComponents.GetById("Big"));
			BigDisplay.SetFillStartingSide(DoubleGaugePanel.StartingPoint.Center);

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
					BigDisplay.Base.gameObject.SetActive(false);
					break;
				case DisplayType.Big:
					NormalDisplay.gameObject.SetActive(false);
					break;
				case DisplayType.Both:
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
					// ei yo are we triggering this at the START of the lap now? wth
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
			if (_usingLeftTimeDisplay)
			{
				if (_gamemodeName != StringSpeedLap) UpdateCurrentTime();
				SetLeftTime();
			}
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

			NormalDisplayValue.text = _bestTime >= 0f ?
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

			BigDisplay.Label.text = _targetTime <= 0f ? "best" : "target";
		}

		private void SetLeftTime()
		{
			if (_currentTime < 0f || _lapInvalidated)
			{
				BigDisplay.Value.text = _bigTimeTextBuilder.ToString(-1f);
				BigDisplay.FillBoth(0f);
				return;
			}
			if (_targetTime <= 0f)
			{
				BigDisplay.Value.text = _bigTimeTextBuilder.ToString(_currentTime);
				BigDisplay.FillBoth(1f);
				return;
			}

			float timeLeft = _targetTime - _currentTime;
			float timeMax = _isCampaign ? _awardTimeDifference : _targetTime;
			if (_showingLapTimeAdvantage)
				timeLeft += _averageLapTimeAdvantage;
			timeLeft = timeLeft < 0f ? 0f : timeLeft;
			BigDisplay.Value.text = _bigTimeTextBuilder.ToString(timeLeft);
			timeLeft = timeLeft > _targetTime ? _targetTime : timeLeft;
			BigDisplay.FillBoth(timeLeft / timeMax);
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
	{}

	public class UpsurgeTracker : ScriptableHud
	{
		internal LayeredDoubleGaugePanel Panel;
		internal RectTransform EnergyInfo;
		internal Text ValueZone;
		internal Text ValueShield;
		internal Animator BarrierWarning;
		internal GmUpsurge Gamemode;
		internal UpsurgeShip UpsurgeTargetShip;
		private int _valueShield;
		private const float TransitionSpeed = 8f;
		private const float TransitionTimerMax = 1.5f;
		private float _transitionTimer;
		private bool _valuesAreFinite = true;
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
			EnergyInfo = CustomComponents.GetById("Energy");
			EnergyInfo.gameObject.SetActive(true);
			ValueZone = EnergyInfo.Find("ValueZone").GetComponent<Text>();
			ValueShield = EnergyInfo.Find("ValueShield").GetComponent<Text>();
			BarrierWarning = CustomComponents.GetById<Animator>("Barrier");
			BarrierWarning.gameObject.SetActive(true);

			Gamemode = (GmUpsurge) RaceManager.CurrentGamemode;

			// change to a simple method that just turns on a bool switch.
			// update should do different things when it's on.
			UpsurgeShip.OnDeployedBarrier += StartTransition;
			UpsurgeShip.OnBuiltBoostStepsIncrease += StartTransition;
			UpsurgeShip.OnShieldActivated += StartTransition;
		}

		private void StartTransition(ShipController ship)
		{
			if (UpsurgeTargetShip == null || ship != UpsurgeTargetShip.TargetShip)
				return;

			_transitionTimer = TransitionTimerMax;
			_valuesAreFinite = false;
		}

		private void UpdateValues()
		{
			if (UpsurgeTargetShip == null)
				return;

			_valueShield = UpsurgeTargetShip.BuiltZones * 20;
			_valueShield = _valueShield < 0 ? 0 : _valueShield > 100 ? 100 : _valueShield;
			_finiteZoneTimeWidth = UpsurgeTargetShip.ZoneTime / 5;
			_finiteZoneWidth = (float) UpsurgeTargetShip.BuiltZones / 10;
			_finiteShieldWidth = (float) _valueShield / 100;
		}

		private void SetValues()
		{
			Panel.Value.text = UpsurgeTargetShip.CurrentZone.ToString();
			ValueZone.text = "+" + UpsurgeTargetShip.BuiltZones;
			ValueShield.text = "+" + _valueShield;

			Panel.FillBoth(_currentZoneWidth);
			Panel.FillSecondGauges(_currentShieldWidth);
			Panel.FillSmallGauges(_currentZoneTimeWidth);
		}

		private void SetZoneTime()
		{
			if (UpsurgeTargetShip == null)
				return;


		}

		public override void Update()
		{
			base.Update();
			if (UpsurgeTargetShip == null)
			{
				UpsurgeTargetShip = Gamemode.Ships.Find(ship => ship.TargetShip == TargetShip);
				return;
			}

			if (_transitionTimer > 0f)
			{
				UpdateValues();

				if (!Mathf.Approximately(_currentZoneTimeWidth, _finiteZoneTimeWidth))
				{
					_currentZoneTimeWidth = Mathf.Lerp(
						_currentZoneTimeWidth, _finiteZoneTimeWidth, Time.deltaTime * TransitionSpeed
					);
				}
				if (!Mathf.Approximately(_currentZoneWidth, _finiteZoneWidth))
				{
					_currentZoneWidth = Mathf.Lerp(
						_currentZoneWidth, _finiteZoneWidth, Time.deltaTime * TransitionSpeed
					);
				}
				if (!Mathf.Approximately(_currentShieldWidth, _finiteShieldWidth))
				{
					_currentShieldWidth = Mathf.Lerp(
						_currentShieldWidth, _finiteShieldWidth, Time.deltaTime * TransitionSpeed
					);
				}
				SetValues();

				_transitionTimer -= Time.deltaTime;
			}
			else if (!_valuesAreFinite)
			{
				_currentZoneTimeWidth = _finiteZoneTimeWidth;
				_currentZoneWidth = _finiteZoneWidth;
				_currentShieldWidth = _finiteShieldWidth;
				SetValues();

				_valuesAreFinite = true;
				_transitionTimer = 0f;
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
		}
	}

	public class ZoneEnergyMeter : ScriptableHud
	{}

	public class Placement : ScriptableHud
	{
		internal FractionPanel Panel;
		private bool _warnOnLastPlace;
		private bool _onWarning;
		private readonly Color _highlightColor = GetTintColor(tintIndex: 1, brightness: 3);
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

		public void Initiate()
		{
			StartCoroutine(UpdateData());
		}

		public IEnumerator UpdateData()
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
	{}

	public class Pitlane : ScriptableHud
	{
		internal RectTransform Panel;
		internal Animator PanelAnimator;
		private static readonly int Active = Animator.StringToHash("Active");
		private static readonly int PointRight = Animator.StringToHash("Point Right");

		public override void Start()
		{
			base.Start();
			Panel = CustomComponents.GetById("Base");
			PanelAnimator = Panel.GetComponent<Animator>();

			Panel.Find("Left").Find("Text").GetComponent<Text>().color = GetTintColor();
			Panel.Find("Right").Find("Text").GetComponent<Text>().color = GetTintColor();

			/*
			 * Using `SetActive()` is ineffective here,
			 * so instead I made an empty animation for the default state.
			 * Now the components won't show up at start
			 * even if I use `SetActive(true)`.
			 */

			NgTrackData.Triggers.PitlaneIndicator.OnPitlaneIndicatorTriggered += Play;
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
					PanelAnimator.SetBool(PointRight, false);
					break;
				case 1:
					PanelAnimator.SetBool(PointRight, true);
					break;
			}

			PanelAnimator.SetTrigger(Active);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgTrackData.Triggers.PitlaneIndicator.OnPitlaneIndicatorTriggered -= Play;
		}
	}

	public class MessageLogger : ScriptableHud
	{
		internal RectTransform Panel;
		internal Text TimeDiff;
		internal Text LapResult;
		internal Text FinalLap;
		internal RectTransform LineTemplate;
		internal const int LineMax = 3;
		internal const float DisplayTimeMax = 3.0f;
		internal Text NowPlaying;
		internal Text WrongWay;

		private readonly Color _tint75 = GetTintColor(TextAlpha.ThreeQuarters);
		private readonly Color _tint90 = GetTintColor(TextAlpha.NineTenths);
		private readonly Color _tint100 = GetTintColor();

		private readonly List<Line> _lines = new(3);

		private class Line
		{
			internal readonly Text Value;
			internal float DisplayTime;

			public Line(RectTransform template)
			{
				Value = template.GetComponent<Text>();
				DisplayTime = DisplayTimeMax;
			}
		}

		private void InitiateLines()
		{
			/*
			 * sizeDelta.y == 30, but 29.75 moves a line of
			 * SpireNbp at the font size of 30 by 30 pixels.
			 */
			float lineHeight = LineTemplate.sizeDelta.y - 0.25f;
			for (int i = 0; i < LineMax; i++)
			{
				RectTransform line =
					Instantiate(LineTemplate.gameObject).GetComponent<RectTransform>();
				line.SetParent(LineTemplate.parent);
				line.localScale = LineTemplate.localScale;
				line.anchoredPosition = LineTemplate.anchoredPosition;

				line.localPosition += Vector3.up * lineHeight * i;

				_lines.Add(new Line(line));
			}
		}

		public override void Start()
		{
			base.Start();
			Initiate();

			NgUiEvents.OnTriggerMessage += Test;
		}

		private void Initiate()
		{
			Panel = CustomComponents.GetById("Base");
			RectTransform TimeKinds = CustomComponents.GetById("TimeKinds");
			TimeDiff = TimeKinds.Find("Difference").GetComponent<Text>();
			LapResult = TimeKinds.Find("LapResult").GetComponent<Text>();
			FinalLap = TimeKinds.Find("FinalLap").GetComponent<Text>();
			LineTemplate = CustomComponents.GetById("MessageLine");
			NowPlaying = CustomComponents.GetById<Text>("NowPlaying");
			WrongWay = CustomComponents.GetById<Text>("WrongWay");

			TimeDiff.color = _tint90;
			LapResult.color = _tint75;
			FinalLap.color = _tint75;
			LineTemplate.GetComponent<Text>().color = _tint75;
			NowPlaying.color = _tint75;
			WrongWay.color = _tint100;

			TimeDiff.text = "";
			LapResult.text = "";
			LineTemplate.GetComponent<Text>().text = "";
			FinalLap.gameObject.SetActive(false);
			WrongWay.gameObject.SetActive(false);

			InitiateLines();
		}

		private void Test(string message, ShipController ship, Color color)
		{
			Debug.Log($"Message: {message}");
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgUiEvents.OnTriggerMessage -= Test;
		}
	}

	public class PickupDisplay : ScriptableHud
	{}

	public class TurboDisplay : ScriptableHud
	{}

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

		public void Initiate()
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
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnCountdownStart -= Initiate;
			StopCoroutine(Panel.Update(Ships.Loaded));
		}
	}

	public class TeamScoreboard : ScriptableHud
	{}

	public class Awards : ScriptableHud
	{}
}
