using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Editing;

/// <summary>削除したステップの delayBeforeMs の扱い</summary>
public enum RemovedDelayHandling {
	/// <summary>次に残るステップの delayBeforeMs へ加算する</summary>
	AddToNextStep,
	/// <summary>破棄する</summary>
	Discard,
}

/// <summary>delayBeforeMs の統計値</summary>
/// <param name="count">対象のステップ数</param>
/// <param name="minimumMs">最小値 ( ms )</param>
/// <param name="maximumMs">最大値 ( ms )</param>
/// <param name="medianMs">中央値 ( ms )。偶数個の場合は中央 2 値の平均</param>
public sealed record DelayStatistics(int count, int minimumMs, int maximumMs, double medianMs);

/// <summary>マクロステップ列への一括編集操作を提供する</summary>
public static class MacroStepEditor {
	/// <summary>マウスボタン押下中でない区間の mouseMove ステップを全て削除する</summary>
	/// <remarks>いずれかのボタン押下中の移動はドラッグとして残す。mouseUp の無い mouseDown 以降は押下中とみなし安全側で残す</remarks>
	/// <param name="steps">編集するステップ列</param>
	/// <param name="delayHandling">削除したステップの delayBeforeMs の扱い</param>
	/// <returns>削除したステップ数</returns>
	public static int RemoveNonDragMouseMoves(List<MacroStep> steps, RemovedDelayHandling delayHandling) {
		var heldButtons = new HashSet<MouseButton>();
		var remaining = new List<MacroStep>(steps.Count);
		var pendingDelayMs = 0;
		var removedCount = 0;
		foreach (var step in steps) {
			if (step is MouseMoveStep && heldButtons.Any() == false) {
				if (delayHandling == RemovedDelayHandling.AddToNextStep) pendingDelayMs += step.delayBeforeMs;
				removedCount++;
				continue;
			}
			step.delayBeforeMs += pendingDelayMs;
			pendingDelayMs = 0;
			remaining.Add(step);
			switch (step) {
				case MouseDownStep down:
					heldButtons.Add(down.button);
					break;
				case MouseUpStep up:
					heldButtons.Remove(up.button);
					break;
			}
		}
		// 末尾の削除分は加算先が無いため破棄する ( マクロ終端の待機は再生結果に影響しない )
		steps.Clear();
		steps.AddRange(remaining);
		return removedCount;
	}

	/// <summary>指定型ステップの delayBeforeMs のうち、しきい値以上のものを指定値へ短縮する</summary>
	/// <param name="steps">編集するステップ列</param>
	/// <param name="thresholdMs">変換対象とする待機時間のしきい値 ( ms )</param>
	/// <param name="newDelayMs">変換後の待機時間 ( ms )</param>
	/// <returns>値が実際に変わったステップ数</returns>
	/// <exception cref="ArgumentOutOfRangeException">しきい値または変換後の値が負の場合</exception>
	public static int UnifyDelays<TStep>(List<MacroStep> steps, int thresholdMs, int newDelayMs) where TStep : MacroStep {
		if (thresholdMs < 0) throw new ArgumentOutOfRangeException(nameof(thresholdMs), "しきい値には 0 以上の値を指定してください");
		if (newDelayMs < 0) throw new ArgumentOutOfRangeException(nameof(newDelayMs), "変換後の値には 0 以上の値を指定してください");
		var changedCount = 0;
		foreach (var step in steps.OfType<TStep>()) {
			if (step.delayBeforeMs < thresholdMs || step.delayBeforeMs == newDelayMs) continue;
			step.delayBeforeMs = newDelayMs;
			changedCount++;
		}
		return changedCount;
	}

	/// <summary>全ステップの待機時間を合計した推定実行時間を計算する</summary>
	/// <remarks>入力送出自体の所要時間は待機時間に比べて十分小さいため含めない</remarks>
	/// <param name="steps">対象のステップ列</param>
	/// <returns>推定実行時間 ( ms )</returns>
	public static long CalculateTotalDurationMs(IReadOnlyList<MacroStep> steps) =>
		steps.Sum(step => (long)step.delayBeforeMs);

	/// <summary>指定型ステップの delayBeforeMs の統計を計算する</summary>
	/// <param name="steps">対象のステップ列</param>
	/// <returns>統計値。対象が 0 件なら null</returns>
	public static DelayStatistics? CalculateDelayStatistics<TStep>(IReadOnlyList<MacroStep> steps) where TStep : MacroStep {
		var delays = steps.OfType<TStep>().Select(step => step.delayBeforeMs).OrderBy(delay => delay).ToList();
		if (delays.Count == 0) return null;
		var median = delays.Count % 2 == 1
			? delays[delays.Count / 2]
			: (delays[delays.Count / 2 - 1] + delays[delays.Count / 2]) / 2.0;
		return new DelayStatistics(delays.Count, delays[0], delays[^1], median);
	}
}
