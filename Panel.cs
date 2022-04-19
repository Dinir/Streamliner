using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using NgLib;
using NgData;
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
		private static Color _tintColorBuffer;
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
			new[] { 0.32f, 0.40f }, // S4 V0.40
			new[] { 0.32f, 0.73f }, // S4 V4
			new[] { 0.18f, 0.86f }, // S2 V2
			new[] { 0.40f, 0.89f }, // S6 V1
			new[] { 0.60f, 0.95f }  // S.60 V.95
		};
		private static readonly float[] TintAlphaList = {
			1f, 0.9f, 0.750f, 0.500f, 0.375f, 0.250f, 0.000f
		};
		internal enum TextAlpha
		{
			Full, NineTenths, ThreeQuarters, Half, ThreeEighths, Quarter, Zero
		}

		internal static Color GetTintColor(
			TextAlpha transparencyIndex = TextAlpha.Full,
			int tintIndex = -1,
			int brightness = 2
		)
		{
			tintIndex = tintIndex < 0 ? OptionValueTint : tintIndex;
			_tintColorBuffer = Color.HSVToRGB(
				StandardH[tintIndex],
				StandardSV[brightness][0],
				StandardSV[brightness][1]
			);
			_tintColorBuffer.a = TintAlphaList[(int) transparencyIndex];
			return _tintColorBuffer;
		}
		internal static float GetTransparency(TextAlpha transparencyIndex) =>
			TintAlphaList[(int) transparencyIndex];
	}

	internal class BigTimeTextBuilder
	{
		private readonly StringBuilder _sb;
		private const string _emptyTime =
			"-:--.<size=20> </size><size=150>--</size>";
		internal string ToString(float value)
		{
			if (value < 0f)
				return _emptyTime;

			_sb.Clear();
			string minutes = IntStrDb.GetNumber(
				Mathf.FloorToInt(value / 60f));
			string seconds = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value) % 60);
			string hundredths = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value * 100f % 100f));

			// 0:00.<size=20> </size><size=150>00</size>
			// Default font size in the component is 300.
			_sb.Append(minutes);
			_sb.Append(":");
			_sb.Append(seconds);
			_sb.Append(".<size=20> </size><size=150>");
			_sb.Append(hundredths);
			_sb.Append("</size>");

			return _sb.ToString();
		}

		public BigTimeTextBuilder(StringBuilder sb) => _sb = sb;
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
		internal readonly RectTransform Base;
		internal readonly Text Label;
		internal readonly Text Value;
		protected readonly RectTransform GaugeBackground;
		protected readonly Image GaugeBackgroundImage;
		protected readonly RectTransform Gauge;
		protected readonly Image GaugeImage;
		internal readonly Color GaugeColor = GetTintColor();
		protected readonly Vector2 MaxSize;
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

			ChangeColor();
			Fill(0f);
		}

		private void ChangeColor()
		{
			Color gaugeBackgroundColor = GaugeColor;
			gaugeBackgroundColor.a = GetTransparency(TextAlpha.ThreeEighths);

			Label.color = GaugeColor;
			Value.color = GaugeColor;
			GaugeImage.color = GaugeColor;
			GaugeBackgroundImage.color = gaugeBackgroundColor;
		}

		// Identical to the method with no parameters,
		// but you don't want to make that one virtual since
		// it will be used in the constructor.
		public virtual void ChangeColor(Color color)
		{
			Color gaugeBackgroundColor = color;
			gaugeBackgroundColor.a = GetTransparency(TextAlpha.ThreeEighths);

			Label.color = color;
			Value.color = color;
			GaugeImage.color = color;
			GaugeBackgroundImage.color = gaugeBackgroundColor;
		}

		public virtual void ChangeDataPartColor(Color color)
		{
			Value.color = color;
			GaugeImage.color = color;
		}

		public void Fill(float amount)
		{
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

			ChangeAccelColor();
			FillAccel(0f);
		}

		private void ChangeAccelColor() =>
			AccelGaugeImage.color = GetTintColor(TextAlpha.ThreeEighths);

		public void FillAccel(float amount)
		{
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

			ChangeFractionPartColor();
		}

		private void ChangeFractionPartColor()
		{
			MaxValue.color = GaugeColor;
			Separator.color = GaugeColor;
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

		internal enum StartingPoint
		{
			Edge, Center
		}

		public DoubleGaugePanel(RectTransform panelElement) : base(panelElement)
		{
			RightGauge = (RectTransform) GaugeBackground.Find("RightGauge");
			RightGaugeImage = RightGauge.GetComponent<Image>();
			// the bar borders are perpendicular straight lines
			CurrentSize.y = MaxSize.y;

			ChangeRightGaugeColor();
			FillBoth(0f);
		}

		private void ChangeRightGaugeColor() =>
			RightGaugeImage.color = GaugeColor;

		public override void ChangeColor(Color color)
		{
			base.ChangeColor(color);
			RightGaugeImage.color = color;
		}

		public override void ChangeDataPartColor(Color color)
		{
			base.ChangeDataPartColor(color);
			RightGaugeImage.color = color;
		}

		public void FillBoth(float amount)
		{
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
		internal readonly Color SecondGaugeColor = GetTintColor(TextAlpha.ThreeEighths);
		internal Vector2 CurrentSecondSize;
		internal readonly Color SmallGaugeColor = GetTintColor(brightness: 1);
		protected readonly Vector2 SmallMaxSize;
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

			ChangeSecondGaugesColor();
			FillSecondGauges(0f);

			if (UsingSmallGauges)
			{
				SmallGauge = (RectTransform) GaugeBackground.Find("SmallGauge");
				SmallGaugeImage = SecondGauge.GetComponent<Image>();
				SmallRightGauge = (RectTransform) GaugeBackground.Find("SmallRightGauge");
				SmallRightGaugeImage = SecondRightGauge.GetComponent<Image>();
				SmallGauge.gameObject.SetActive(true);
				SmallRightGauge.gameObject.SetActive(true);
				SmallMaxSize = SmallGauge.sizeDelta;
				CurrentSmallSize.y = SmallMaxSize.y;

				ChangeSmallGaugesColor();
				FillSmallGauges(0f);
			}
		}

		private void ChangeSecondGaugesColor()
		{
			SecondGaugeImage.color = SecondGaugeColor;
			SecondRightGaugeImage.color = SecondGaugeColor;
		}

		private void ChangeSmallGaugesColor()
		{
			SmallGaugeImage.color = SmallGaugeColor;
			SmallRightGaugeImage.color = SmallGaugeColor;
		}

		public override void ChangeColor(Color color)
		{
			base.ChangeColor(color);
			color.a = GetTransparency(TextAlpha.ThreeEighths);
			SecondGaugeImage.color = color;
			SecondRightGaugeImage.color = color;
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
			CurrentSecondSize.x = amount * MaxSize.x;
			SecondGauge.sizeDelta = CurrentSecondSize;
			SecondRightGauge.sizeDelta = CurrentSecondSize;
		}

		public void FillSmallGauges(float amount)
		{
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

		private readonly float _oneLetterWidth = 20f;
		private readonly float _oneDotWidth = 4f;
		internal readonly RectTransform Base;
		protected readonly RectTransform TargetRow;
		protected readonly RectTransform EntrySlotTemplate;
		protected readonly RectTransform TemplateGaugeBackground;
		protected readonly RectTransform TemplateGauge;
		private List<EntrySlot> _visibleList;
		private List<RawValuePair> _rawValueList;

		private class EntrySlot
		{
			private readonly RectTransform _base;
			private readonly CanvasGroup _baseCanvasGroup;
			private readonly Text _name;
			private readonly Text _value;
			private readonly RectTransform _gauge;
			private readonly Image _gaugeImage;
			internal int refId;
			protected readonly Vector2 MaxSize;
			internal Vector2 CurrentSize;
			internal readonly Color SlotColor = GetTintColor(TextAlpha.ThreeQuarters);

			public EntrySlot(RectTransform template)
			{
				_base = template;
				_base.gameObject.SetActive(true);
				_baseCanvasGroup = _base.GetComponent<CanvasGroup>();
				_name = _base.Find("Name").GetComponent<Text>();
				_value = _base.Find("Plate").Find("Value").GetComponent<Text>();
				_gauge = (RectTransform)_base.Find("GaugeBackground").Find("Gauge");
				_gaugeImage = _gauge.GetComponent<Image>();
				MaxSize = _gauge.sizeDelta;

				ChangeColor();
				SetName("");
				SetDisplayValue(ValueType.Position, 0);
				FillByPercentage(100f);
			}

			private void ChangeColor()
			{
				_name.color = SlotColor;
				_gaugeImage.color = SlotColor;
				_value.color = GetTintColor(TextAlpha.NineTenths);
			}

			public void ChangeOverallAlpha(TextAlpha transparencyIndex) =>
				_baseCanvasGroup.alpha = GetTransparency(transparencyIndex);

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
				CurrentSize.x = ( value / 100f ) * MaxSize.x;
				CurrentSize.y = CurrentSize.x >= MaxSize.y ?
					MaxSize.y : CurrentSize.x;
				_gauge.sizeDelta = CurrentSize;
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
				) * _oneLetterWidth
				+ (_valueType == ValueType.Score ? _oneDotWidth + _oneLetterWidth * 2f : 0f);

			if (additionalWidth == 0f)
				return;

			Vector2 widthAdjustmentVector2 =
				new Vector2(additionalWidth, 0);
			Vector3 widthAdjustmentVector3 =
				new Vector3(additionalWidth, 0, 0);

			TargetRow.Find("TargetValue").Find("Value").GetComponent<Text>()
				.text = IntStrDb.GetNumber((int) targetScore);
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
				int refId = slot.refId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				slot.SetDisplayValue(_valueType, rawValuePair.Value);
			}
		}

		private void UpdateSlotsEnergy()
		{
			foreach (EntrySlot slot in _visibleList)
			{
				int refId = slot.refId;
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
				int refId = slot.refId;
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