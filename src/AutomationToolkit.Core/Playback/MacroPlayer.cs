using System.Diagnostics;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Playback;

/// <summary>マクロ再生の動作設定</summary>
public sealed class PlaybackOptions {
	/// <summary>再生速度倍率</summary>
	/// <remarks>2.0 なら待機時間が半分の 2 倍速になる</remarks>
	public double speedMultiplier { get; set; } = 1.0;
	/// <summary>ループ回数</summary>
	/// <remarks>0 で無限ループ</remarks>
	public int loopCount { get; set; } = 1;
}

/// <summary>マクロ再生機能を提供する</summary>
public interface IMacroPlayer {
	/// <summary>再生中かどうか</summary>
	bool isPlaying { get; }

	/// <summary>再生状態が変化したときに発生するイベント</summary>
	event EventHandler<bool>? IsPlayingChanged;

	/// <summary>マクロを再生する</summary>
	/// <remarks>停止ホットキーによる中断は通常フローのため、キャンセルされた場合は例外を投げず正常終了する</remarks>
	/// <param name="macro">再生するマクロ</param>
	/// <param name="options">再生の動作設定</param>
	/// <param name="ct">再生を中断するためのキャンセルトークン</param>
	/// <returns>再生の完了を表すタスク</returns>
	Task PlayAsync(Macro macro, PlaybackOptions options, CancellationToken ct);
}

/// <summary>SendInput でマクロを再生する IMacroPlayer の実装</summary>
/// <param name="synthesizer">入力の送出に使う InputSynthesizer</param>
public sealed class MacroPlayer(InputSynthesizer synthesizer) : IMacroPlayer {
	/// <summary>再生中なら 1</summary>
	private int playing;

	/// <inheritdoc/>
	public bool isPlaying => Volatile.Read(ref playing) == 1;

	/// <inheritdoc/>
	public event EventHandler<bool>? IsPlayingChanged;

	/// <inheritdoc/>
	public async Task PlayAsync(Macro macro, PlaybackOptions options, CancellationToken ct) {
		if (options.speedMultiplier <= 0) {
			throw new ArgumentOutOfRangeException(nameof(options), "speedMultiplier は正の値が必要です。");
		}
		if (Interlocked.CompareExchange(ref playing, 1, 0) != 0) {
			throw new InvalidOperationException("すでに再生中です。");
		}
		IsPlayingChanged?.Invoke(this, true);

		// 押しっぱなし事故防止のため、送出済みの Down を追跡して終了時に必ず解放する
		var heldKeys = new HashSet<(ushort virtualKey, ushort scanCode, bool isExtended)>();
		var heldButtons = new HashSet<MouseButton>();

		using var highRes = PrecisionDelay.BeginHighResolutionTimers();
		synthesizer.RefreshVirtualScreenMetrics();
		try {
			await Task.Run(() => RunAsync(macro, options, heldKeys, heldButtons, ct), CancellationToken.None)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException) {
			// 停止ホットキー等による中断は正常終了扱い
		}
		finally {
			ReleaseHeld(heldKeys, heldButtons);
			Volatile.Write(ref playing, 0);
			IsPlayingChanged?.Invoke(this, false);
		}
	}

	/// <summary>全ステップをループ設定に従って順番に送出する</summary>
	/// <param name="macro">再生するマクロ</param>
	/// <param name="options">再生の動作設定</param>
	/// <param name="heldKeys">押下中キーの追跡先</param>
	/// <param name="heldButtons">押下中マウスボタンの追跡先</param>
	/// <param name="ct">再生を中断するためのキャンセルトークン</param>
	/// <returns>再生の完了を表すタスク</returns>
	private async Task RunAsync(
		Macro macro,
		PlaybackOptions options,
		HashSet<(ushort, ushort, bool)> heldKeys,
		HashSet<MouseButton> heldButtons,
		CancellationToken ct) {
		var startTimestamp = Stopwatch.GetTimestamp();
		var cumulativeMs = 0.0;

		for (var loop = 0; options.loopCount == 0 || loop < options.loopCount; loop++) {
			foreach (var step in macro.steps) {
				// 絶対時刻基準で待機し、ステップごとの丸め誤差が累積しないようにする
				cumulativeMs += step.delayBeforeMs / options.speedMultiplier;
				await PrecisionDelay.WaitUntilAsync(
					startTimestamp + PrecisionDelay.MsToTimestampTicks(cumulativeMs), ct)
					.ConfigureAwait(false);

				synthesizer.Send(step);
				TrackHeldState(step, heldKeys, heldButtons);
			}
		}
	}

	/// <summary>ステップの内容に応じて押下中のキー・ボタンの追跡状態を更新する</summary>
	/// <param name="step">送出したステップ</param>
	/// <param name="heldKeys">押下中キーの追跡先</param>
	/// <param name="heldButtons">押下中マウスボタンの追跡先</param>
	private static void TrackHeldState(
		MacroStep step,
		HashSet<(ushort, ushort, bool)> heldKeys,
		HashSet<MouseButton> heldButtons) {
		switch (step) {
			case KeyDownStep kd:
				heldKeys.Add((kd.virtualKey, kd.scanCode, kd.isExtended));
				break;
			case KeyUpStep ku:
				heldKeys.Remove((ku.virtualKey, ku.scanCode, ku.isExtended));
				break;
			case MouseDownStep md:
				heldButtons.Add(md.button);
				break;
			case MouseUpStep mu:
				heldButtons.Remove(mu.button);
				break;
		}
	}

	/// <summary>押しっぱなしになっているキー・マウスボタンをすべて解放する</summary>
	/// <param name="heldKeys">押下中のキーの一覧</param>
	/// <param name="heldButtons">押下中のマウスボタンの一覧</param>
	private void ReleaseHeld(
		HashSet<(ushort virtualKey, ushort scanCode, bool isExtended)> heldKeys,
		HashSet<MouseButton> heldButtons) {
		foreach (var (virtualKey, scanCode, isExtended) in heldKeys) {
			try {
				synthesizer.SendKey(virtualKey, scanCode, isExtended, down: false);
			}
			catch (InvalidOperationException) {
				// 解放は best-effort ( SendInput ブロック時でも他のキーの解放を続ける )
			}
		}
		heldKeys.Clear();

		foreach (var button in heldButtons) {
			try {
				synthesizer.SendMouseButtonUp(button);
			}
			catch (InvalidOperationException) {
			}
		}
		heldButtons.Clear();
	}
}
