// Copyright © 2022 Dinir Nertan
// Licensed under the Open Software License version 3.0

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NgShips;
using static Streamliner.HudRegister;
using Random = UnityEngine.Random;

namespace Streamliner
{
	internal static class Shifter
	{
		internal const int MaxPanelCount = 20;
		internal const int MaxPlayer = 2;

		internal const float DampTime = 0.2f;
		internal const float BaseShiftFactor = 3f;
		internal const float VerticalShiftEmphasis = -2f;
		internal const float MaxVerticalShiftAmount = 127.5f;
		internal const float BaseMaxLandingShakeAmount = 60f;
		internal const float BaseWallBounceShakeAmount = 30f;
		internal const float BaseScrapingShakeAmount = 6f;

		internal const float MinVerticalSpeedDiff = 10f;
		internal const float MaxVerticalSpeedDiff = 20f;
		internal static readonly float VerticalSpeedDiffRange =
			MaxVerticalSpeedDiff - MinVerticalSpeedDiff;

		// single cannon hit roughly decreases the speed by 9.5%
		internal const float SpeedChangeIntensityThreshold = 1 / 0.095f;
		internal const float MaxSpeedChangeIntensity = 2f;
		// make MaxChange - MinCharge : 1 - MinCharge = BaseWallBounce : BaseScraping
		internal static readonly float MinSpeedChangeIntensity = Math.Max(
			MaxSpeedChangeIntensity - ( BaseWallBounceShakeAmount / ( BaseWallBounceShakeAmount - BaseScrapingShakeAmount ) ),
			0
		);
		internal static readonly float SpeedChangeIntensityRange = MaxSpeedChangeIntensity - MinSpeedChangeIntensity;

		// duration / decay speed == total lasting time in seconds
		internal const float ShakeDuration = 60f;
		internal const float ShakeDurationDecaySpeed = 240f;

		internal static float ShiftFactor = 3f;
		internal static float MaxLandingShakeAmount = 60f;
		internal static float WallBounceShakeAmount = 30f;
		internal static float ScrapingShakeAmount = 6f;

		private class AmountData
		{
			internal Vector2 ShiftTarget;
			internal float ShakeAmount;
			internal Vector2 ShakeVector;
			internal float ShakeDuration;

			internal Vector3 CurrentVelocity;
			internal Vector3 PreviousVelocity;
			internal float SpeedChangeIntensity;
		}

		internal static readonly List<ShipController> TargetShips = new(MaxPlayer);
		private static readonly List<AmountData> _amountData = new(MaxPlayer);
		internal static readonly List<List<Panel>> Panels = new(MaxPlayer);
		
		internal static void Flush()
		{
			for (int i = 0; i < MaxPlayer; i++)
			{
				TargetShips.Clear();
				_amountData[i] = new AmountData();
				Panels[i].Clear();
			}
		}

		static Shifter()
		{
			for (int i = 0; i < MaxPlayer; i++)
			{
				_amountData.Add(new AmountData());
				Panels.Add(new(MaxPanelCount));
			}
		}

		internal class Panel
		{
			private readonly RectTransform _rt;
			internal readonly string HudName;
			internal readonly Vector2 OriginPosition;
			internal Vector2 TargetPosition;
			internal Vector2 CurrentSpeed;
			internal Vector2 ShiftedPosition;
			internal Vector2 ShakingPosition;

			internal Vector2 Position => _rt.anchoredPosition;

			internal void SetTargetPosition(Vector2 position) =>
				TargetPosition = OriginPosition + position;
			internal void SetShiftedPosition(Vector2 position) =>
				ShiftedPosition = position;
			internal void SetShakingPosition(Vector2 position) =>
				ShakingPosition = ShiftedPosition + position;
			internal void ResetShakingPosition() =>
				ShakingPosition = ShiftedPosition;

			internal void SetPositionToShifted() =>
				_rt.anchoredPosition = ShiftedPosition;
			internal void SetPositionToShaking() =>
				_rt.anchoredPosition = ShakingPosition;
			internal void ResetPosition() =>
				_rt.anchoredPosition = OriginPosition;

			internal void Hide() => _rt.gameObject.SetActive(false);
			internal void Show() => _rt.gameObject.SetActive(true);

			internal Panel(RectTransform rt, string name)
			{
				_rt = rt;
				HudName = name;
				Vector2 anchoredPosition = rt.anchoredPosition;
				OriginPosition = anchoredPosition;
				ShiftedPosition = anchoredPosition;
			}
		}

		internal static void ApplySettings()
		{
			ShiftFactor = BaseShiftFactor * OptionShiftMultiplier;
			MaxLandingShakeAmount = BaseMaxLandingShakeAmount * OptionShakeMultiplier;
			WallBounceShakeAmount = BaseWallBounceShakeAmount * OptionShakeMultiplier;
			ScrapingShakeAmount = BaseScrapingShakeAmount * OptionScrapeMultiplier;
		}

		internal static void Add(RectTransform panel, int playerIndex, string name) =>
			Panels[playerIndex].Add(new Panel(panel, name));

		private static Vector2 GetUpdatedShiftAmount(Vector2 currentVelocity, bool inVacuum)
		{
			// apply shift
			Vector2 shiftTarget = currentVelocity * ShiftFactor;
			// apply vertical emphasis
			float verticalShiftAmount = shiftTarget.y * (!inVacuum ? VerticalShiftEmphasis : -1f);
			// limit vertical amount from getting too big
			verticalShiftAmount = verticalShiftAmount > MaxVerticalShiftAmount ?
				MaxVerticalShiftAmount : verticalShiftAmount < -MaxVerticalShiftAmount ?
					-MaxVerticalShiftAmount : verticalShiftAmount;

			return shiftTarget with { y = verticalShiftAmount };
		}

		private static void UpdateSpeedChangeIntensity(AmountData amountData)
		{
			float currentSpeed = amountData.CurrentVelocity.z;
			float previousSpeed = amountData.PreviousVelocity.z;

			amountData.SpeedChangeIntensity = 
				(
					previousSpeed > currentSpeed ?
					previousSpeed - currentSpeed : currentSpeed - previousSpeed
				) / (previousSpeed < 1f ? 1f : previousSpeed)
				* SpeedChangeIntensityThreshold;
		}

		internal static void UpdateAmount(ShipController ship)
		{
			if (ship.T is null)
				return;

			AmountData amountData = _amountData[ship.playerIndex];
			ShipSim sim = ship.PysSim;
			amountData.CurrentVelocity = ship.T.InverseTransformDirection(ship.RBody.velocity);
			UpdateSpeedChangeIntensity(amountData);

			// update shift amount

			amountData.ShiftTarget =
				GetUpdatedShiftAmount((Vector2) amountData.CurrentVelocity, ship.InVacuum);

			// update shake amount

			float verticalSpeedDiff = amountData.CurrentVelocity.y - amountData.PreviousVelocity.y;

			// big landing
			if (verticalSpeedDiff > MinVerticalSpeedDiff && !ship.OnMaglock)
			{
				verticalSpeedDiff =
					(verticalSpeedDiff - MinVerticalSpeedDiff) / VerticalSpeedDiffRange;
				verticalSpeedDiff = verticalSpeedDiff < 0 ?
					0 : verticalSpeedDiff > 1f ?
						1f : verticalSpeedDiff;
				float shakeAmount = verticalSpeedDiff * MaxLandingShakeAmount;
				if (amountData.ShakeAmount < shakeAmount)
					amountData.ShakeAmount = shakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}
			// wall crash
			else if (sim.touchingWall)
			{
				if (amountData.ShakeAmount < WallBounceShakeAmount)
					amountData.ShakeAmount = WallBounceShakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}
			// scraping
			else if (sim.isShipScraping || sim.ScrapingShip)
			{
				if (amountData.ShakeAmount < ScrapingShakeAmount)
					amountData.ShakeAmount = ScrapingShakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}
			// speed loss
			else if (
				amountData.SpeedChangeIntensity >= MinSpeedChangeIntensity &&
				amountData.PreviousVelocity.z > amountData.CurrentVelocity.z
			)
			{
				float shakeAmount = amountData.SpeedChangeIntensity - MinSpeedChangeIntensity;
				shakeAmount =
					(shakeAmount >= SpeedChangeIntensityRange ? SpeedChangeIntensityRange : shakeAmount)
					/ SpeedChangeIntensityRange * WallBounceShakeAmount;
				if (amountData.ShakeAmount < shakeAmount)
					amountData.ShakeAmount = shakeAmount;
				amountData.ShakeDuration = ShakeDuration;
			}

			if (amountData.ShakeDuration > 0)
				amountData.ShakeDuration -= Time.deltaTime * ShakeDurationDecaySpeed;
			else
			{
				amountData.ShakeAmount = 0;
				amountData.ShakeDuration = 0;
			}

			amountData.PreviousVelocity = amountData.CurrentVelocity;
		}

		internal static IEnumerator Shift(int playerIndex)
		{
			AmountData amountData = _amountData[playerIndex];
			List<Panel> panels = Panels[playerIndex];

			while (true)
			{
				if (amountData.ShakeDuration > 0)
					amountData.ShakeVector = Random.insideUnitCircle.normalized * amountData.ShakeAmount;
				foreach (Panel p in panels)
				{
					p.SetTargetPosition(amountData.ShiftTarget);
					if (p.Position == p.TargetPosition && amountData.ShakeDuration == 0)
						continue;

					// always update position that's only affected by shifting
					p.SetShiftedPosition(Vector2.SmoothDamp(
						p.ShiftedPosition, p.TargetPosition, ref p.CurrentSpeed, DampTime
					));

					// if shake is in effect, force apply the shake to the shifted position,
					// but don't actually change the stored variable.
					if (amountData.ShakeDuration > 0)
					{
						p.SetShakingPosition(amountData.ShakeVector);
						p.SetPositionToShaking();
					}
					else
					{
						p.ResetShakingPosition();
						p.SetPositionToShifted();
					}
				}

				yield return null;
			}
		}

		internal static void HideHud(ShipController ship)
		{
			if (!TargetShips.Contains(ship)) return;
			foreach (Panel p in Panels[ship.playerIndex]) p.Hide();
		}

		internal static void ShowHud(ShipController ship)
		{
			if (!TargetShips.Contains(ship)) return;
			foreach (Panel p in Panels[ship.playerIndex]) p.Show();
		}

		/*internal static string Dump()
		{
			StringBuilder sb = new();
			sb.AppendLine("# Shifter.Dump()");
			sb.Append("## TargetShips: ");
			for (int i = 0; i < MaxPlayer; i++)
			{
				if (i != 0)
					sb.Append(", ");
				sb.Append(TargetShips[i]?.GetType().Name ?? "null");
				sb.Append(" ");
				sb.Append(TargetShips[i]?.ShipName ?? "null");
			}
			sb.AppendLine();

			sb.Append("## AmountData: ");
			for (int i = 0; i < MaxPlayer; i++)
			{
				if (i != 0)
					sb.Append(", ");
				sb.Append(_amountData[i]?.GetType().Name ?? "null");
				sb.Append(" ");
				sb.Append(_amountData[i]?.ShiftTarget.ToString() ?? "null");
			}
			sb.AppendLine();

			sb.AppendLine("## Panels: ");
			for (int i = 0; i < MaxPlayer; i++)
			{
				sb.Append("Player ");
				sb.Append(i);
				sb.Append(": ");
				sb.AppendLine(Panels[i].Count.ToString());
			}

			return sb.ToString();
		}*/
	}
}
