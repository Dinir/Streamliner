using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NgLib;
using NgData;
using NgGame;
using NgModes;
using NgTrackData;
using NgShips;
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
		internal Color GaugeColor;
		private readonly float _gaugeMaxWidth;
		internal Vector2 CurrentSize;

		public BasicPanel(RectTransform panelElement)
		{
			_base = panelElement;
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
	internal class SpeedPanel : BasicPanel
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
			switch (name)
			{
				case "Eliminator":
					_valueType = ValueType.Score;
					break;
				case "Rush Hour":
					_valueType = ValueType.Energy;
					break;
				default:
					_valueType = ValueType.Position;
					break;
			}
		}

		private readonly float _oneLetterWidth = 20f;
		private readonly RectTransform _base;
		private readonly RectTransform _targetRow;
		private readonly RectTransform _entrySlotTemplate;
		private readonly RectTransform _templateGaugeBackground;
		private readonly RectTransform _templateGauge;
		private readonly List<EntrySlot> _list = new();
		private readonly List<RawValuePair> _rawValueList = new();
		private bool _reordering;

		private class EntrySlot
		{
			private readonly RectTransform _base;
			private readonly Text _name;
			private readonly Text _value;
			private readonly RectTransform _gauge;
			internal int Id;
			internal float RawValue;
			internal float PreviousRawValue;
			private readonly float _gaugeMaxWidth;
			private Vector2 _currentSize;
			private readonly Color _slotColor = GetTintColor(TextAlpha.ThreeQuarters);

			public EntrySlot(RectTransform template)
			{
				_base = template;
				_base.gameObject.SetActive(value: true);
				_name = _base.Find("Name").GetComponent<Text>();
				_value = _base.Find("Plate").Find("Value").GetComponent<Text>();
				_gauge = (RectTransform)_base.Find("GaugeBackground").Find("Gauge");
				_currentSize = _gauge.sizeDelta;
				_gaugeMaxWidth = _currentSize.x;

				ChangeColor();
				Name = "";
				Position = 0;
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

			public string Name
			{
				get => _name.text;
				set => _name.text = value;
			}

			public int Position
			{
				get => (int) RawValue;
				set
				{
					PreviousRawValue = RawValue;
					RawValue = value;
					_value.text = IntStrDb.GetNoSingleCharNumber(value);
				}
			}

			public float Score
			{
				get => RawValue;
				set
				{
					PreviousRawValue = RawValue;
					RawValue = value;
					_value.text = IntStrDb.GetNumber(Mathf.FloorToInt(value));
				}
			}

			public float Energy
			{
				get => RawValue;
				set
				{
					PreviousRawValue = RawValue;
					RawValue = value;
					_value.text = IntStrDb.GetNumber(
						Mathf.Clamp(Mathf.FloorToInt(value), 0, 99)
					);
				}
			}

			private void FillByPercentage(float value)
			{
				_currentSize.x = ( value / 100f ) * _gaugeMaxWidth;
				_gauge.sizeDelta = _currentSize;
			}

			public void FillByValue()
			{
				FillByPercentage(RawValue);
			}
		}

		private class RawValuePair
		{
			internal readonly int Id;
			internal float Value;

			public RawValuePair(int id, float value)
			{
				Id = id;
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
			_entrySlotTemplate.gameObject.SetActive(value: false);

			InitiateLayout();
			InitiateSlots();
		}

		private void InitiateLayout()
		{
			float targetScore = gamemode.TargetScore;

			_templateGaugeBackground.gameObject.
				SetActive(value: _valueType == ValueType.Energy);

			if (targetScore == 0f)
			{
				_targetRow.gameObject.SetActive(value: false);
			}
			else
			{
				_targetRow.Find("TargetValue").Find("Value").GetComponent<Text>().text =
					IntStrDb.GetNumber((int)targetScore);

				if (targetScore < 100f) return;

				Vector2 oneLetterWidthVector2 = new Vector2(_oneLetterWidth, 0);
				Vector3 oneLetterWidthVector3 = new Vector3(_oneLetterWidth, 0, 0);

				_targetRow.Find("Target").GetComponent<RectTransform>().localPosition -=
					oneLetterWidthVector3;
				_targetRow.Find("TargetValue").GetComponent<RectTransform>().sizeDelta +=
					oneLetterWidthVector2;
				_templateGaugeBackground.sizeDelta -=
					oneLetterWidthVector2 / _templateGaugeBackground.localScale;
				_templateGauge.sizeDelta -=
					oneLetterWidthVector2 / _templateGauge.localScale;
				_entrySlotTemplate.Find("Name").GetComponent<RectTransform>().sizeDelta -=
					oneLetterWidthVector2 /
					_entrySlotTemplate.Find("Name").GetComponent<RectTransform>().localScale;
				_entrySlotTemplate.Find("Plate").GetComponent<RectTransform>().sizeDelta +=
					oneLetterWidthVector2;
			}
		}

		private void InitiateSlots()
		{
			for (int i = 0; i < Ships.Loaded.Count; i++)
			{
				RectTransform slot =
					Instantiate(_entrySlotTemplate.gameObject).GetComponent<RectTransform>();
				slot.SetParent(_entrySlotTemplate.parent);
				slot.localScale = _entrySlotTemplate.localScale;
				slot.anchoredPosition = _entrySlotTemplate.anchoredPosition;

				slot.localPosition += Vector3.down * slot.sizeDelta.y * i;

				_list.Add(new EntrySlot(slot));

				_list[i].Name = Ships.Loaded[i].ShipName;
				switch (_valueType)
				{
					case ValueType.Score:
						_list[i].Score = Ships.Loaded[i].Score;
						break;
					case ValueType.Energy:
						_list[i].Energy = Ships.Loaded[i].ShieldIntegrity;
						break;
					case ValueType.Position:
					default:
						_list[i].Position = Ships.Loaded[i].CurrentPlace;
						break;
				}

				_rawValueList.Add(new RawValuePair(i, _list[i].RawValue));
			}
		}

		public void UpdateSlotsScore()
		{
			bool startReordering = false;
			for (int i = 0; i < _rawValueList.Count; i++)
			{
				if (Mathf.Approximately(
					    _rawValueList[_list[i].Id].Value,
					    Ships.Loaded[_list[i].Id].Score))
					continue;
				startReordering = !_reordering;
				_rawValueList[_list[i].Id].Value = Ships.Loaded[_list[i].Id].Score;
				_list[i].Score = Ships.Loaded[_list[i].Id].Score;
			}
			_reordering = false;
			if (startReordering)
				ReorderSlots(_rawValueList.OrderByDescending(p => p.Value).ToList());
		}

		public void UpdateSlotsEnergy()
		{
			bool startReordering = false;
			for (int i = 0; i < _rawValueList.Count; i++)
			{
				if (Mathf.Approximately(
					    _rawValueList[_list[i].Id].Value,
					    Ships.Loaded[_list[i].Id].ShieldIntegrity))
					continue;
				startReordering = !_reordering;
				_rawValueList[_list[i].Id].Value = Ships.Loaded[_list[i].Id].ShieldIntegrity;
				_list[i].Energy = Ships.Loaded[_list[i].Id].ShieldIntegrity;
				_list[i].FillByValue();
				if (_list[i].PreviousRawValue <= 0f)
					_list[i].ChangeOverallAlpha(TextAlpha.Quarter);
			}
			_reordering = false;
			if (startReordering)
				ReorderSlots(_rawValueList.OrderByDescending(p => p.Value).ToList());
		}

		public void UpdateSlotsPosition()
		{
			bool startReordering = false;
			for (int i = 0; i < _rawValueList.Count; i++)
			{
				if ((int) _rawValueList[_list[i].Id].Value ==
					Ships.Loaded[_list[i].Id].CurrentPlace)
					continue;
				startReordering = !_reordering;
				if (Ships.Loaded[_list[i].Id].ShieldIntegrity > 0f)
				{
					_rawValueList[_list[i].Id].Value = Ships.Loaded[_list[i].Id].CurrentPlace;
					_list[i].Position = Ships.Loaded[_list[i].Id].CurrentPlace;
					_list[i].ChangeOverallAlpha(TextAlpha.Full);
				}
				else
				{
					_rawValueList[_list[i].Id].Value = int.MaxValue;
					_list[i].ChangeOverallAlpha(TextAlpha.Zero);
				}
			}
			_reordering = false;
			if (startReordering)
				ReorderSlots(_rawValueList.OrderBy(p => p.Value).ToList());
		}

		private void ReorderSlots(List<RawValuePair> orderedList)
		{
			_reordering = true;
			for (int i = 0; i < _list.Count; i++)
			{
				_list[i].Id = orderedList[i].Id;
				_list[i].Name = Ships.Loaded[_list[i].Id].ShipName;
				_list[i].PreviousRawValue =
					_valueType == ValueType.Position ? int.MaxValue : float.NegativeInfinity;
			}
		}
	}
}