using UnityEngine;
using UnityEngine.UI;
using NgData;
using NgTrackData;
using NgShips;
using static Streamliner.HudRegister;
using static Streamliner.Panel.PresetColorPicker;

namespace Streamliner.Panel
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
			1f, 0.9f, 0.750f, 0.500f, 0.375f, 0.250f
		};
		internal enum TextAlpha
		{
			Full, NineTenths, ThreeQuarters, Half, ThreeEighths, Quarter
		}
		private static Color _tintColorBuffer;
		internal static Color GetTintColor(TextAlpha transparencyIndex = TextAlpha.Full)
		{
			_tintColorBuffer = TintColorList[OptionValueTint];
			_tintColorBuffer.a = TintAlphaList[(int)transparencyIndex];
			return _tintColorBuffer;
		}
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
}