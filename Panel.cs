using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using NgLib;
using NgData;
using NgModding.Huds;
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
			new[] { 0.32f, 0.40f }, // S4   V.40
			new[] { 0.32f, 0.73f }, // S4   V4
			new[] { 0.26f, 0.81f }, // S3   V3
			new[] { 0.18f, 0.86f }, // S2   V2
			new[] { 0.40f, 0.89f }, // S6   V1
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
			int clarity = 3
		) =>
			Color.HSVToRGB(
					StandardH[tintIndex < 0 ? OptionValueTint : tintIndex],
					tintIndex == 0 ? 0 : StandardSV[clarity][0],
					StandardSV[clarity][1]
				) with
				{
					a = TintAlphaList[(int) transparencyIndex]
				};

		internal static Color GetPanelColor(
			int tintIndex = 0
		) =>
			Color.HSVToRGB(
					StandardH[tintIndex],
					tintIndex == 0 ? 0 : StandardSV[0][0],
					tintIndex == 0 ? 0.16f : 0.30f
				) with
				{
					a = TintAlphaList[(int) TextAlpha.ThreeQuarters]
				};

		internal static float GetTransparency(TextAlpha transparencyIndex) =>
			TintAlphaList[(int) transparencyIndex];
	}

	internal class BigTimeTextBuilder
	{
		private readonly StringBuilder _sb;
		// 0:00.<size=20> </size><size=150>00</size>
		// Default font size in the component is 300.
		private const string StringAfterMinute =
			":";
		private const string StringAfterSecond =
			".<size=20> </size><size=150>";
		private const string StringAfterHundredth =
			"</size>";
		private static readonly string EmptyTime =
			"-" + StringAfterMinute +
			"--" + StringAfterSecond +
			"--" + StringAfterHundredth;

		internal string ToString(float value)
		{
			if (value < 0f)
				return EmptyTime;

			_sb.Clear();
			string minutes = IntStrDb.GetNumber(
				Mathf.FloorToInt(value / 60f));
			string seconds = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value) % 60);
			string hundredths = IntStrDb.GetNoSingleCharNumber(
				Mathf.FloorToInt(value * 100f % 100f));

			_sb.Append(minutes);
			_sb.Append(StringAfterMinute);
			_sb.Append(seconds);
			_sb.Append(StringAfterSecond);
			_sb.Append(hundredths);
			_sb.Append(StringAfterHundredth);

			return _sb.ToString();
		}

		public BigTimeTextBuilder(StringBuilder sb) => _sb = sb;
	}

	internal class PickupTextBuilder
	{
		private readonly StringBuilder _sb;
		private const string FormatAutopilotDisengaging =
			"Disengaging in x";
		private const string FormatHellstorm =
			"x rear locks";
		private static readonly int AutopilotDisengagingStringLength =
			FormatAutopilotDisengaging.Length;
		private static readonly int HellstormStringLength =
			FormatHellstorm.Length;

		private const string StringAutopilot3 =  ">  3  <" ;
		private const string StringAutopilot2 =  ">> 2 <<" ;
		private const string StringAutopilot1 = ">>> 1 <<<";
		// <size=150><color=#0000>a</color>rear </size>0<size=150> locks</size>
		// Default font size in the component is 300.
		private const string StringHellstormPrefix =
			"<size=150><color=#0000>a</color>rear </size>";
		private const string StringHellstormSuffix =
			"<size=150> locks</size>";
		private const string StringHellstormSuffixSingle =
			"<size=150> lock<color=#0000>s</color></size>";

		internal string ToAutoPilotText(string text)
		{
			if (
				text.Length < AutopilotDisengagingStringLength ||
				!char.IsDigit(text[text.Length - 1])
			)
				return text;

			return text[text.Length - 1] switch
			{
				'3' => StringAutopilot3,
				'2' => StringAutopilot2,
				'1' => StringAutopilot1,
				_ => text
			};
		}

		internal string ToHellstormText(string text)
		{
			/*
			 * There is also a method called `IsDigit` which only returns
			 * true for radix-10 digits. I don't know if I want to use
			 * that instead.
			 */
			if (
				text.Length < HellstormStringLength ||
				!char.IsNumber(text[0])
			)
				return text;

			_sb.Clear();
			_sb.Append(StringHellstormPrefix);
			_sb.Append(text[0]);
			_sb.Append(text[0] != '1' ?
				StringHellstormSuffix : StringHellstormSuffixSingle);

			return _sb.ToString();
		}

		public PickupTextBuilder(StringBuilder sb) => _sb = sb;
	}

	internal static class PickupIcon
	{
		internal static readonly Dictionary<string, Sprite> Get = new()
		{
			{ "rockets", CustomHudRegistry.GetWeaponSprite("rockets", false) },
			{ "missile", CustomHudRegistry.GetWeaponSprite("missile", false) },
			{ "mines", CustomHudRegistry.GetWeaponSprite("mines", false) },
			{ "plasma", CustomHudRegistry.GetWeaponSprite("plasma", false) },
			{ "energywall", CustomHudRegistry.GetWeaponSprite("energywall", false) },
			{ "cannon", CustomHudRegistry.GetWeaponSprite("cannon", false) },
			{ "shield", CustomHudRegistry.GetWeaponSprite("shield", false) },
			{ "autopilot", CustomHudRegistry.GetWeaponSprite("autopilot", false) },
			{ "emergencypack", CustomHudRegistry.GetWeaponSprite("emergencypack", false) },
			{ "tremor", CustomHudRegistry.GetWeaponSprite("tremor", false) },
			{ "turbo", CustomHudRegistry.GetWeaponSprite("turbo", false) },
			{ "hunter", CustomHudRegistry.GetWeaponSprite("hunter", false) },
			{ "hellstorm", CustomHudRegistry.GetWeaponSprite("hellstorm", false) }
		};
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
			while (count < TrackManager.Instance.data.sections.Count && (bool) (UnityEngine.Object) next.next)
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

		internal static float GetLapCompletionRate(ShipController ship, int totalTrackSections = -1)
		{
			if (totalTrackSections == -1)
				totalTrackSections = GetTotalSectionCount();

			int passingSection = ship.CurrentSection.index - 1;
			if (passingSection < 0)
				passingSection += totalTrackSections;

			return (float) passingSection / totalTrackSections;
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
		internal readonly Vector2 MaxSize;
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
		protected virtual void ChangeColor(Color color)
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
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
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
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
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

		protected override void ChangeColor(Color color)
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
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
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
		internal readonly Color SmallGaugeColor = GetTintColor(clarity: 1);
		internal readonly Vector2 SmallMaxSize;
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

			if (!UsingSmallGauges) return;

			SmallGauge = (RectTransform) GaugeBackground.Find("SmallGauge");
			SmallGaugeImage = SmallGauge.GetComponent<Image>();
			SmallRightGauge = (RectTransform) GaugeBackground.Find("SmallRightGauge");
			SmallRightGaugeImage = SmallRightGauge.GetComponent<Image>();
			SmallGauge.gameObject.SetActive(true);
			SmallRightGauge.gameObject.SetActive(true);
			SmallMaxSize = SmallGauge.sizeDelta;
			CurrentSmallSize.y = SmallMaxSize.y;

			ChangeSmallGaugesColor();
			FillSmallGauges(0f);
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

		public void ChangeSmallGaugesColor(Color color)
		{
			SmallGaugeImage.color = color;
			SmallRightGaugeImage.color = color;
		}

		protected override void ChangeColor(Color color)
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
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
			CurrentSecondSize.x = amount * MaxSize.x;
			SecondGauge.sizeDelta = CurrentSecondSize;
			SecondRightGauge.sizeDelta = CurrentSecondSize;
		}

		public void FillSmallGauges(float amount)
		{
			amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
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

	internal class PickupPanel
	{
		private readonly CanvasGroup _panelGroup;
		private readonly Image _bracketsImage;
		private readonly Image _iconImage;
		private readonly Text _info;
		private readonly PickupTextBuilder _pickupTextBuilder = new(new StringBuilder());
		private string _weaponId;
		private const float AnimationLength = 0.1f;
		private const float AnimationSpeed = 1 / AnimationLength;

		public Coroutine CurrentTransition;

		private static readonly Color _offensiveColor =
			GetTintColor(tintIndex: 2, clarity: 2);
		private static readonly Color _defensiveColor =
			GetTintColor(tintIndex: 8, clarity: 2);
		private static readonly Color _hudDefaultColor = GetTintColor();

		public IEnumerator ColorFade(bool enableIcon, bool offensive = false)
		{
			Color startColor = enableIcon ?
				Color.clear : _iconImage.color;
			Color endColor = enableIcon ?
				offensive ? _offensiveColor : _defensiveColor :
				Color.clear;
			Color transitionColor;
			float t = enableIcon ? 0 : AnimationLength;

			if (enableIcon && !_iconImage.enabled)
			{
				_bracketsImage.enabled = true;
				_iconImage.enabled = true;
				if (_info is not null)
					_info.enabled = true;
			}

			if (enableIcon)
			{
				_panelGroup.alpha = 1f;
				_bracketsImage.color = startColor;
				_iconImage.color = startColor;
				while (t <= AnimationLength)
				{
					t += Time.deltaTime;
					transitionColor = Color.Lerp(startColor, _hudDefaultColor, t * AnimationSpeed);
					_bracketsImage.color = transitionColor;
					_iconImage.color = transitionColor;
					yield return null;
				}
			}
			while (t <= AnimationLength * 2)
			{
				t += Time.deltaTime;
				float secondFadeProgress = (t - AnimationLength) * AnimationSpeed;
				if (!enableIcon)
				{
					_panelGroup.alpha = 1f - secondFadeProgress;
				}
				else
				{
					transitionColor = Color.Lerp(_iconImage.color, endColor, secondFadeProgress);
					_bracketsImage.color = transitionColor;
					_iconImage.color = transitionColor;
				}
				yield return null;
			}
			_bracketsImage.color = endColor;
			_iconImage.color = endColor;

			if (!enableIcon && _iconImage.enabled)
			{
				_panelGroup.alpha = 0f;
				_bracketsImage.enabled = false;
				_iconImage.enabled = false;
				if (_info is not null)
					_info.enabled = false;
			}

			CurrentTransition = null;
		}

		public void UpdateSprite(string weaponId)
		{
			_iconImage.sprite = PickupIcon.Get[weaponId];
			_weaponId = weaponId;
		}

		public void UpdateText(string text)
		{
			if (_info is null || text is null)
				return;

			_info.text = _weaponId switch
			{
				"autopilot" => _pickupTextBuilder.ToAutoPilotText(text),
				"hellstorm" => _pickupTextBuilder.ToHellstormText(text),
				_ => text
			};
		}

		public bool IconEnabled => _iconImage.enabled;

		public void ShowInstant(bool offensive = false)
		{
			_panelGroup.alpha = 1f;
			_bracketsImage.enabled = true;
			_iconImage.enabled = true;
			_bracketsImage.color = offensive ?
				_offensiveColor : _defensiveColor;
			_iconImage.color = offensive ?
				_offensiveColor : _defensiveColor;
		}

		public void ShowWarning()
		{
			_panelGroup.alpha = 1f;
			_bracketsImage.enabled = true;
			_iconImage.enabled = true;
			_bracketsImage.color = _offensiveColor;
			_iconImage.color = _offensiveColor;
		}

		public PickupPanel(RectTransform basePanel)
		{
			_panelGroup = basePanel.GetComponent<CanvasGroup>();
			_bracketsImage = basePanel.Find("Brackets").GetComponent<Image>();
			_iconImage = basePanel.Find("Icon").GetComponent<Image>();

			_panelGroup.alpha = 0f;
			_bracketsImage.enabled = false;
			_iconImage.enabled = false;
		}

		public PickupPanel(RectTransform basePanel, Text infoText)
		{
			_panelGroup = basePanel.GetComponent<CanvasGroup>();
			_bracketsImage = basePanel.Find("Brackets").GetComponent<Image>();
			_iconImage = basePanel.Find("Icon").GetComponent<Image>();
			_info = infoText;
			_info.color = GetTintColor();

			_panelGroup.alpha = 0f;
			_bracketsImage.enabled = false;
			_iconImage.enabled = false;
			_info.enabled = false;
			_info.text = "";
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

		private const float OneLetterWidth = 20f;
		private const float OneDotWidth = 4f;
		internal readonly RectTransform Base;
		protected readonly RectTransform TargetRow;
		protected readonly RectTransform EntrySlotTemplate;
		protected readonly RectTransform TemplateGaugeBackground;
		protected readonly RectTransform TemplateGauge;
		private List<EntrySlot> _visibleList;
		private List<RawValuePair> _rawValueList;

		private class EntrySlot
		{
			private readonly CanvasGroup _baseCanvasGroup;
			private readonly Text _name;
			private readonly Text _value;
			private readonly RectTransform _gauge;
			private readonly Image _gaugeImage;
			internal int RefId;
			private readonly Vector2 _maxSize;
			private Vector2 _currentSize;
			private readonly Color _slotColor = GetTintColor(TextAlpha.ThreeQuarters);

			public EntrySlot(RectTransform template)
			{
				template.gameObject.SetActive(true);
				_baseCanvasGroup = template.GetComponent<CanvasGroup>();
				_name = template.Find("Name").GetComponent<Text>();
				_value = template.Find("Plate").Find("Value").GetComponent<Text>();
				_gauge = (RectTransform)template.Find("GaugeBackground").Find("Gauge");
				_gaugeImage = _gauge.GetComponent<Image>();
				_maxSize = _gauge.sizeDelta;

				ChangeColor();
				SetName("");
				SetDisplayValue(ValueType.Position, 0);
				FillByPercentage(100f);
			}

			private void ChangeColor()
			{
				_name.color = _slotColor;
				_gaugeImage.color = _slotColor;
				_value.color = GetTintColor(TextAlpha.NineTenths);
			}

			public void ChangeOverallAlpha(TextAlpha transparencyIndex) =>
				_baseCanvasGroup.alpha = GetTransparency(transparencyIndex);

			public void SetRefId(int id) => RefId = id;

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
						int inflatedValue = Mathf.RoundToInt(value * 100f);
						string intPart = (inflatedValue / 100).ToString();
						string hundredths =
							IntStrDb.GetNoSingleCharNumber(inflatedValue % 100);
						_value.text = intPart + "." + hundredths;
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
				_currentSize.x = ( value / 100f ) * _maxSize.x;
				_currentSize.y = _currentSize.x >= _maxSize.y ?
					_maxSize.y : _currentSize.x;
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
				) * OneLetterWidth
				+ (_valueType == ValueType.Score ? OneDotWidth + OneLetterWidth * 2f : 0f);

			if (additionalWidth == 0f)
				return;

			Vector2 widthAdjustmentVector2 =
				new Vector2(additionalWidth, 0);
			Vector3 widthAdjustmentVector3 =
				new Vector3(additionalWidth, 0, 0);

			TargetRow.Find("TargetValue").Find("Value").GetComponent<Text>()
				.text = _valueType == ValueType.Score ?
				IntStrDb.GetNumber((int) targetScore) + ".00" :
				IntStrDb.GetNumber((int) targetScore);
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
				int refId = slot.RefId;
				RawValuePair rawValuePair = _rawValueList[refId];
				slot.SetName(rawValuePair.Name);
				slot.SetDisplayValue(_valueType, rawValuePair.Value);
			}
		}

		private void UpdateSlotsEnergy()
		{
			foreach (EntrySlot slot in _visibleList)
			{
				int refId = slot.RefId;
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
				int refId = slot.RefId;
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