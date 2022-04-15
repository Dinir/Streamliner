using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NgLib;
using NgData;
using NgGame;
using NgModes;
using NgTrackData;
using NgShips;
using NgUi.RaceUi.HUD;
using static Streamliner.HudRegister;
using static Streamliner.PresetColorPicker;
using static UnityEngine.Object;

namespace Streamliner
{
	internal static class PresetColorPicker
	{
		private static readonly Color[] TintColorList = new Color[] {
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
			1f, 0.9f, 0.750f, 0.500f, 0.375f, 0.250f, 0.000f
		};
		internal enum TextAlpha
		{
			Full, NineTenths, ThreeQuarters, Half, ThreeEighths, Quarter, Zero
		}
		private static Color _tintColorBuffer;
		internal static Color GetTintColor(TextAlpha transparencyIndex = TextAlpha.Full)
		{
			_tintColorBuffer = TintColorList[OptionValueTint];
			_tintColorBuffer.a = TintAlphaList[(int) transparencyIndex];
			return _tintColorBuffer;
		}
		internal static float GetTransparency(TextAlpha transparencyIndex) =>
			TintAlphaList[(int) transparencyIndex];
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
			while (count < TrackManager.Instance.data.sections.Count && (bool) (Object) next.next)
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
		internal readonly RectTransform _base;
		private readonly Text _label;
		internal readonly Text Value;
		protected readonly RectTransform GaugeBackground;
		private readonly RectTransform _gauge;
		internal readonly Color GaugeColor = GetTintColor();
		protected readonly Vector2 MaxSize;
		internal Vector2 CurrentSize;

		public BasicPanel(RectTransform panelElement)
		{
			_base = panelElement;
			_label = panelElement.Find("Label").GetComponent<Text>();
			Value = panelElement.Find("Value").GetComponent<Text>();
			GaugeBackground = panelElement.Find("GaugeBackground").GetComponent<RectTransform>();
			_gauge = (RectTransform)GaugeBackground.Find("Gauge");
			MaxSize = _gauge.sizeDelta;

			ChangeColor();
			Fill(0f);
		}

		private void ChangeColor()
		{
			Color gaugeBackgroundColor = GaugeColor;
			gaugeBackgroundColor.a = GetTransparency(TextAlpha.ThreeEighths);

			_label.color = GaugeColor;
			Value.color = GaugeColor;
			_gauge.GetComponent<Image>().color = GaugeColor;
			GaugeBackground.GetComponent<Image>().color = gaugeBackgroundColor;
		}

		// Identical to the method with no parameters,
		// but you don't want to make that one virtual since
		// it will be used in the constructor.
		public virtual void ChangeColor(Color color)
		{
			Color gaugeBackgroundColor = color;
			gaugeBackgroundColor.a = GetTransparency(TextAlpha.ThreeEighths);

			_label.color = color;
			Value.color = color;
			_gauge.GetComponent<Image>().color = color;
			GaugeBackground.GetComponent<Image>().color = gaugeBackgroundColor;
		}

		public virtual void ChangeDataPartColor(Color color)
		{
			Value.color = color;
			_gauge.GetComponent<Image>().color = color;
		}

		public void Fill(float amount)
		{
			CurrentSize.x = amount * MaxSize.x;
			// This is only useful because the bar border is
			// a 45deg slanted straight line.
			CurrentSize.y = CurrentSize.x >= MaxSize.y ?
				MaxSize.y : CurrentSize.x;
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
	internal class SpeedPanel : BasicPanel
	{
		private readonly RectTransform _accelGauge;
		private Vector2 _currentAccelSize;

		public SpeedPanel(RectTransform panelElement) : base(panelElement)
		{
			_accelGauge = (RectTransform)GaugeBackground.Find("AccelGauge");

			ChangeAccelColor();
			FillAccel(0f);
		}

		private void ChangeAccelColor() =>
			_accelGauge.GetComponent<Image>().color = GetTintColor(TextAlpha.Quarter);

		public void FillAccel(float amount)
		{
			_currentAccelSize.x = amount * MaxSize.x;
			_currentAccelSize.y = _currentAccelSize.x >= MaxSize.y ?
				MaxSize.y : _currentAccelSize.x;
			_accelGauge.sizeDelta = _currentAccelSize;
		}
	}

	internal class FractionPanel : BasicPanel
	{
		internal readonly Text MaxValue;

		public FractionPanel(RectTransform panelElement) : base(panelElement)
		{
			MaxValue = panelElement.Find("MaxValue").GetComponent<Text>();

			ChangeFractionPartColor();
		}

		private void ChangeFractionPartColor()
		{
			MaxValue.color = GetTintColor();
			_base.Find("Separator").GetComponent<Text>().color = GetTintColor();
		}

		public override void ChangeDataPartColor(Color color)
		{
			base.ChangeDataPartColor(color);
			MaxValue.color = color;
			_base.Find("Separator").GetComponent<Text>().color = color;
		}
	}

	internal class Playerboard
	{
		private readonly Gamemode gamemode = RaceManager.CurrentGamemode;

		private enum ValueType
		{
			Position, Score, Energy
		}
		private ValueType _valueType;
		private void SetValueType(string name)
		{
			_valueType = name switch
			{
				"Eliminator" => ValueType.Score,
				"Rush Hour" => ValueType.Energy,
				_ => ValueType.Position
			};
		}

		private readonly float _oneLetterWidth = 20f;
		private readonly float _oneDotWidth = 4f;
		private readonly RectTransform _base;
		private readonly RectTransform _targetRow;
		private readonly RectTransform _entrySlotTemplate;
		private readonly RectTransform _templateGaugeBackground;
		private readonly RectTransform _templateGauge;
		private List<EntrySlot> _visibleList;
		private List<RawValuePair> _rawValueList;

		private class EntrySlot
		{
			private readonly RectTransform _base;
			private readonly Text _name;
			private readonly Text _value;
			private readonly RectTransform _gauge;
			internal int refId;
			private readonly float _gaugeMaxWidth;
			private Vector2 _currentSize;
			private readonly Color _slotColor = GetTintColor(TextAlpha.ThreeQuarters);

			public EntrySlot(RectTransform template)
			{
				_base = template;
				_base.gameObject.SetActive(true);
				_name = _base.Find("Name").GetComponent<Text>();
				_value = _base.Find("Plate").Find("Value").GetComponent<Text>();
				_gauge = (RectTransform)_base.Find("GaugeBackground").Find("Gauge");
				_currentSize = _gauge.sizeDelta;
				_gaugeMaxWidth = _currentSize.x;

				ChangeColor();
				SetName("");
				SetDisplayValue(ValueType.Position, 0);
				FillByPercentage(100f);
			}

			private void ChangeColor()
			{
				_name.color = _slotColor;
				_gauge.GetComponent<Image>().color = _slotColor;
				_value.color = GetTintColor(TextAlpha.NineTenths);
			}

			public void ChangeOverallAlpha(TextAlpha transparencyIndex) =>
				_base.GetComponent<CanvasGroup>().alpha = GetTransparency(transparencyIndex);

			public void SetRefId(int id) => refId = id;

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
						float score = (int) (value * 100.0) * 0.01f;
						_value.text = score.ToString(CultureInfo.InvariantCulture);
						break;
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
				_currentSize.x = ( value / 100f ) * _gaugeMaxWidth;
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

		public Playerboard(RectTransform panelElement)
		{
			SetValueType(gamemode.Name);

			_base = panelElement;
			_targetRow = panelElement.Find("TargetRow").GetComponent<RectTransform>();
			_entrySlotTemplate = panelElement.Find("EntrySlot").GetComponent<RectTransform>();
			_templateGaugeBackground =
				_entrySlotTemplate.Find("GaugeBackground").GetComponent<RectTransform>();
			_templateGauge = (RectTransform) _templateGaugeBackground.Find("Gauge");
			_entrySlotTemplate.gameObject.SetActive(false);
		}

		public void InitiateLayout()
		{
			float targetScore = gamemode.TargetScore;

			// Disable components that are irrelevant to the game mode.
			if (targetScore == 0f)
			{
				_targetRow.gameObject
					.SetActive(false);
				_entrySlotTemplate.localPosition = Vector3.zero;
			}
			_templateGaugeBackground.gameObject
				.SetActive(_valueType == ValueType.Energy);

			/*
			 * The reason the thresholds are staying at 2*10^n instead of 10^n is
			 * just because the font has a very narrow width for number 1.
			 * Energy type is included because it starts with 100, which is bigger than 2*10^1.
			 */
			float additionalWidth =
				(_valueType == ValueType.Energy ? 1f :
					targetScore < 20f ? 0f : targetScore < 200f ? 1f : 2f
				) * _oneLetterWidth
				+ (_valueType == ValueType.Score ? _oneDotWidth + _oneLetterWidth * 2f : 0f);

			if (additionalWidth == 0f)
				return;

			Vector2 widthAdjustmentVector2 =
				new Vector2(additionalWidth, 0);
			Vector3 widthAdjustmentVector3 =
				new Vector3(additionalWidth, 0, 0);

			_targetRow.Find("TargetValue").Find("Value").GetComponent<Text>()
				.text = IntStrDb.GetNumber((int) targetScore);
			_targetRow.Find("Target").GetComponent<RectTransform>()
				.localPosition -= widthAdjustmentVector3;
			_targetRow.Find("TargetValue").GetComponent<RectTransform>()
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
				widthAdjustmentVector2 / _templateGaugeBackground.localScale;
			RectTransform name = _entrySlotTemplate.Find("Name").GetComponent<RectTransform>();

			_templateGaugeBackground.sizeDelta -= gaugeAdjustmentVector;
			_templateGauge.sizeDelta -= gaugeAdjustmentVector;
			name.localPosition -= widthAdjustmentVector3;
			name.sizeDelta -= widthAdjustmentVector2 / name.localScale;
			_entrySlotTemplate.Find("Plate").GetComponent<RectTransform>()
					.sizeDelta += widthAdjustmentVector2;
		}

		public void InitiateSlots()
		{
			_visibleList = new List<EntrySlot>(Ships.Loaded.Count);
			_rawValueList = new List<RawValuePair>(Ships.Loaded.Count);
			for (int i = 0; i < Ships.Loaded.Count; i++)
			{
				RectTransform slot =
					Instantiate(_entrySlotTemplate.gameObject).GetComponent<RectTransform>();
				slot.SetParent(_entrySlotTemplate.parent);
				slot.localScale = _entrySlotTemplate.localScale;
				slot.anchoredPosition = _entrySlotTemplate.anchoredPosition;

				slot.localPosition += Vector3.down * slot.sizeDelta.y * i;

				ShipController loadedShip = Ships.Loaded[i];
				_visibleList.Add(new EntrySlot(slot));
				_visibleList[i].SetRefId(loadedShip.ShipId);
				_visibleList[i].SetName(loadedShip.ShipName);

				_rawValueList.Add(new RawValuePair(
					loadedShip.ShipId, loadedShip.ShipName, float.NegativeInfinity));
			}
		}

		public IEnumerator Update()
		{
			while (true)
			{
				UpdateData();
				UpdateRefIdByOrder();
				UpdateSlots();

				yield return new WaitForSeconds(Position.UpdateTime);
			}
		}

		/// <summary>
		/// Update stored data in <c>_rawValueList</c> with corresponding ones in the loaded ships.
		/// </summary>
		public void UpdateData()
		{
			foreach (RawValuePair rawValuePair in _rawValueList)
			{
				ShipController ship = Ships.Loaded[rawValuePair.Id];
				switch (_valueType)
				{
					case ValueType.Energy:
						rawValuePair.Value = ship.ShieldIntegrity;
						break;
					case ValueType.Score:
						rawValuePair.Value = ship.Score;
						break;
					case ValueType.Position:
					default:
						rawValuePair.Value = ship.ShieldIntegrity < 0f ?
							Ships.Loaded.Count + 1 : ship.CurrentPlace;
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
				int refId = slot.refId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				slot.SetDisplayValue(ValueType.Position, rawValuePair.Value);
			}
		}

		private void UpdateSlotsEnergy()
		{
			foreach (EntrySlot slot in _visibleList)
			{
				int refId = slot.refId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				slot.SetDisplayValue(ValueType.Energy, rawValuePair.Value);
				slot.FillByPercentage(rawValuePair.Value);
				slot.ChangeOverallAlpha(
					Ships.Loaded[refId].ShieldIntegrity < 0f ?
						TextAlpha.Quarter : TextAlpha.Full
				);
			}
		}

		private void UpdateSlotsPosition()
		{
			foreach (EntrySlot slot in _visibleList)
			{
				int refId = slot.refId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				if (rawValuePair.Value <= Ships.Loaded.Count)
				{
					slot.SetDisplayValue(ValueType.Position, rawValuePair.Value);
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