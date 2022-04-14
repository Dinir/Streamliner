using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using NgData;
using NgSettings;
using NgUi.RaceUi;
using NgEvents;
using NgGame;
using NgLib;
using NgShips;
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

		private readonly Color _highlightColor = new Color32(0xf2, 0x61, 0x6b, 0xff); // Red S.60 V.95

		private float _currentSpeed;
		private float _previousSpeed;

		private readonly float _animationSpeed = 8f;
		private readonly float _animationTimerMax = 1.5f;
		private float _speedDecreaseAnimationTimer;
		private float _speedIncreaseAnimationTimer;

		public override void Start()
		{
			base.Start();
			Panel = new SpeedPanel(CustomComponents.GetById<RectTransform>("Base"));
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

		private readonly Color _rechargeColor = new Color32(0x88, 0xe3, 0xe0, 0xbf); // Cyan S6 V1
		private readonly Color _lowColor = new Color32(0xe3, 0xb3, 0x88, 0xbf); // Orange S6 V1
		private readonly Color _criticalColor = new Color32(0xe3, 0x88, 0x8e, 0xbf); // Red S6 V1
		private readonly Color _damageColor = new Color32(0xf2, 0x61, 0x6b, 0xff); // Red S.60 V.95
		private readonly Color _damageLowColor = new Color32(0xf2, 0xed, 0x61, 0xff); // Yellow S.60 V.95

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

			Panel = CustomComponents.GetById<RectTransform>("Base");
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

		private readonly List<LapSlot> _slots = new(5);
		private LapSlot _currentSlot;
		private int _totalSlots;
		/// <summary>
		/// Contains the index of the current lap. It's base-1, but period for 0th exists.
		/// </summary>
		private int _currentLap;
		private int _totalLaps;
		private int _totalSections;

		internal readonly StringBuilder CurrentTimeBuilder = new StringBuilder();
		private string ConvertForCurrentTimer(float value)
		{
			CurrentTimeBuilder.Clear();
			string minutes = IntStrDb.GetNumber(
				Mathf.FloorToInt(value / 60f));
			string seconds = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value) % 60);
			string hundredths = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value * 100f % 100f));

			// 0:00.<size=20> </size><size=150>00</size>
			// Default font size in the component is 300.
			CurrentTimeBuilder.Append(minutes);
			CurrentTimeBuilder.Append(":");
			CurrentTimeBuilder.Append(seconds);
			CurrentTimeBuilder.Append(".<size=20> </size><size=150>");
			CurrentTimeBuilder.Append(hundredths);
			CurrentTimeBuilder.Append("</size>");

			return CurrentTimeBuilder.ToString();
		}

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
			Panel = new BasicPanel(CustomComponents.GetById<RectTransform>("Base"));
			LapSlotTemplate = CustomComponents.GetById<RectTransform>("LapSlot");
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
			Panel.Value.text = ConvertForCurrentTimer(TargetShip.TotalRaceTime);

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
	{}

	public class BestTime : ScriptableHud
	{}

	public class ZoneTracker : ScriptableHud
	{}

	public class UpsurgeTracker : ScriptableHud
	{}

	public class ZoneEnergyMeter : ScriptableHud
	{}

	public class Placement : ScriptableHud
	{
		internal FractionPanel Panel;
		private bool _warnOnLastPlace;
		private bool _onWarning;
		private readonly Color _highlightColor = new Color32(0xe3, 0x88, 0x8e, 0xff); // Red S6 V1

		public override void Start()
		{
			base.Start();
			Panel = new FractionPanel(CustomComponents.GetById<RectTransform>("Base"));
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
			Panel = new FractionPanel(CustomComponents.GetById<RectTransform>("Base"))
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
		internal Animator Left;
		internal Animator Right;
		private static readonly int Active = Animator.StringToHash("Active");
		private static readonly int PointRight = Animator.StringToHash("Point Right");

		public override void Start()
		{
			base.Start();
			Panel = CustomComponents.GetById<RectTransform>("Base");
			Left = Panel.Find("Left").GetComponent<Animator>();
			Right = Panel.Find("Right").GetComponent<Animator>();

			Clear();

			Left.SetBool(PointRight, false);
			Right.SetBool(PointRight, true);

			NgTrackData.Triggers.PitlaneIndicator.OnPitlaneIndicatorTriggered += Play;
		}

		private void Clear()
		{
			Left.gameObject.SetActive(false);
			Right.gameObject.SetActive(false);
			Left.GetComponent<Image>().enabled = false;
			Left.GetComponent<RectTransform>()
				.Find("Text").GetComponent<Text>().enabled = false;
			Right.GetComponent<Image>().enabled = false;
			Right.GetComponent<RectTransform>()
				.Find("Text").GetComponent<Text>().enabled = false;
			Left.gameObject.SetActive(true);
			Right.gameObject.SetActive(true);
		}

		private void Play(ShipController ship, int side)
		{
			if (side == -1)
				Left.SetTrigger(Active);
			if (side == 1)
				Right.SetTrigger(Active);
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
			Panel = CustomComponents.GetById<RectTransform>("Base");
			RectTransform TimeKinds = CustomComponents.GetById<RectTransform>("TimeKinds");
			TimeDiff = TimeKinds.Find("Difference").GetComponent<Text>();
			LapResult = TimeKinds.Find("LapResult").GetComponent<Text>();
			FinalLap = TimeKinds.Find("FinalLap").GetComponent<Text>();
			LineTemplate = CustomComponents.GetById<RectTransform>("MessageLine");
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
			Debug.Log(message);
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
			Panel = new Playerboard(CustomComponents.GetById<RectTransform>("Base"));

			NgRaceEvents.OnCountdownStart += Initiate;
		}

		public void Initiate()
		{
			Panel.InitiateLayout();
			Panel.InitiateSlots();
			StartCoroutine(Panel.UpdateData());
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			NgRaceEvents.OnCountdownStart -= Initiate;
			StopCoroutine(Panel.UpdateData());
		}
	}

	public class TeamScoreboard : ScriptableHud
	{}

	public class UpsurgeScoreboard : ScriptableHud
	{}

	public class Awards : ScriptableHud
	{}
}
