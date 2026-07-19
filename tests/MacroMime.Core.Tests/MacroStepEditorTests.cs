using MacroMime.Core.Editing;
using MacroMime.Core.Models;

namespace MacroMime.Core.Tests;

/// <summary>MacroStepEditor の一括編集操作のテスト</summary>
public class MacroStepEditorTests {
	/// <summary>mouseMove ステップを生成する</summary>
	/// <param name="delayMs">実行前の待機ミリ秒数</param>
	/// <returns>生成したステップ</returns>
	private static MouseMoveStep Move(int delayMs = 0) => new() { x = 10, y = 20, delayBeforeMs = delayMs };

	/// <summary>mouseDown ステップを生成する</summary>
	/// <param name="button">対象のマウスボタン</param>
	/// <param name="delayMs">実行前の待機ミリ秒数</param>
	/// <returns>生成したステップ</returns>
	private static MouseDownStep Down(MouseButton button = MouseButton.Left, int delayMs = 0) =>
		new() { button = button, x = 10, y = 20, delayBeforeMs = delayMs };

	/// <summary>mouseUp ステップを生成する</summary>
	/// <param name="button">対象のマウスボタン</param>
	/// <param name="delayMs">実行前の待機ミリ秒数</param>
	/// <returns>生成したステップ</returns>
	private static MouseUpStep Up(MouseButton button = MouseButton.Left, int delayMs = 0) =>
		new() { button = button, x = 10, y = 20, delayBeforeMs = delayMs };

	/// <summary>keyDown ステップを生成する</summary>
	/// <param name="delayMs">実行前の待機ミリ秒数</param>
	/// <returns>生成したステップ</returns>
	private static KeyDownStep Key(int delayMs = 0) => new() { virtualKey = 65, scanCode = 30, delayBeforeMs = delayMs };

	/// <summary>ボタン非押下区間の mouseMove が全て削除され、削除件数が一致する</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_RemovesMovesOutsideDrag() {
		var steps = new List<MacroStep> { Move(), Move(), Down(), Up(), Move() };

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.Discard);

		Assert.Equal(3, removed);
		Assert.Equal(2, steps.Count);
		Assert.IsType<MouseDownStep>(steps[0]);
		Assert.IsType<MouseUpStep>(steps[1]);
	}

	/// <summary>mouseDown から mouseUp の間の mouseMove はドラッグとして残る</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_KeepsDragMoves() {
		var steps = new List<MacroStep> { Down(), Move(), Move(), Up() };

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.Discard);

		Assert.Equal(0, removed);
		Assert.Equal(4, steps.Count);
	}

	/// <summary>mouseUp 直後の mouseMove は削除される</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_RemovesMoveRightAfterMouseUp() {
		var steps = new List<MacroStep> { Down(), Up(), Move() };

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.Discard);

		Assert.Equal(1, removed);
		Assert.Equal(2, steps.Count);
	}

	/// <summary>mouseUp の無い mouseDown 以降の mouseMove は全て残る</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_KeepsMovesAfterUnmatchedMouseDown() {
		var steps = new List<MacroStep> { Down(), Move(), Move(), Move() };

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.Discard);

		Assert.Equal(0, removed);
		Assert.Equal(4, steps.Count);
	}

	/// <summary>複数ボタン同時押下では、全てのボタンが離れるまで mouseMove が残る</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_TracksMultipleHeldButtons() {
		var steps = new List<MacroStep> {
			Down(MouseButton.Left), Down(MouseButton.Right),
			Up(MouseButton.Left), Move(), // 右ボタンが押下中なので残る
			Up(MouseButton.Right), Move(), // 全て離れたので削除される
		};

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.Discard);

		Assert.Equal(1, removed);
		Assert.Equal(5, steps.Count);
		Assert.IsType<MouseUpStep>(steps[^1]);
	}

	/// <summary>加算モードでは連続する複数 mouseMove の削除で待機時間が合算され、次に残るステップへ加算される</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_AddToNextStep_AccumulatesDelays() {
		var steps = new List<MacroStep> { Key(100), Move(10), Move(20), Move(30), Down(delayMs: 5) };

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.AddToNextStep);

		Assert.Equal(3, removed);
		Assert.Equal(2, steps.Count);
		Assert.Equal(100, steps[0].delayBeforeMs);
		Assert.Equal(65, steps[1].delayBeforeMs);
	}

	/// <summary>加算モードで先頭の mouseMove を削除すると、次に残る最初のステップへ加算される</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_AddToNextStep_LeadingMoves() {
		var steps = new List<MacroStep> { Move(40), Key(10) };

		MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.AddToNextStep);

		var key = Assert.IsType<KeyDownStep>(Assert.Single(steps));
		Assert.Equal(50, key.delayBeforeMs);
	}

	/// <summary>加算モードで末尾の mouseMove を削除すると、加算先が無いため待機時間は破棄される</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_AddToNextStep_TrailingMovesAreDiscarded() {
		var steps = new List<MacroStep> { Key(10), Move(999) };

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.AddToNextStep);

		Assert.Equal(1, removed);
		var key = Assert.IsType<KeyDownStep>(Assert.Single(steps));
		Assert.Equal(10, key.delayBeforeMs);
	}

	/// <summary>破棄モードでは削除した mouseMove の待機時間が次のステップへ加算されない</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_Discard_DoesNotAccumulateDelays() {
		var steps = new List<MacroStep> { Move(100), Move(200), Down(delayMs: 5) };

		MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.Discard);

		var down = Assert.IsType<MouseDownStep>(Assert.Single(steps));
		Assert.Equal(5, down.delayBeforeMs);
	}

	/// <summary>空のステップ列では何も起きず 0 件が返る</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_EmptySteps_ReturnsZero() {
		var steps = new List<MacroStep>();

		Assert.Equal(0, MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.AddToNextStep));
		Assert.Empty(steps);
	}

	/// <summary>mouseMove を含まないステップ列では削除も待機時間の変化も起きない</summary>
	[Fact]
	public void RemoveNonDragMouseMoves_NoMoves_KeepsStepsUnchanged() {
		var steps = new List<MacroStep> { Key(10), Down(delayMs: 20), Up(delayMs: 30) };

		var removed = MacroStepEditor.RemoveNonDragMouseMoves(steps, RemovedDelayHandling.AddToNextStep);

		Assert.Equal(0, removed);
		Assert.Equal([10, 20, 30], steps.Select(step => step.delayBeforeMs));
	}

	/// <summary>加算モードでは削除したステップの待機時間が次のステップへ加算される</summary>
	[Fact]
	public void RemoveStepsAt_AddToNextStep_AddsDelayToNextStep() {
		var steps = new List<MacroStep> { Key(10), Move(40), Down(delayMs: 5) };

		var removed = MacroStepEditor.RemoveStepsAt(steps, [1], RemovedDelayHandling.AddToNextStep);

		Assert.Equal(1, removed);
		Assert.Equal(2, steps.Count);
		Assert.Equal(10, steps[0].delayBeforeMs);
		Assert.Equal(45, steps[1].delayBeforeMs);
	}

	/// <summary>破棄モードでは削除したステップの待機時間が次のステップへ加算されない</summary>
	[Fact]
	public void RemoveStepsAt_Discard_DoesNotAddDelay() {
		var steps = new List<MacroStep> { Key(10), Move(40), Down(delayMs: 5) };

		var removed = MacroStepEditor.RemoveStepsAt(steps, [1], RemovedDelayHandling.Discard);

		Assert.Equal(1, removed);
		Assert.Equal([10, 5], steps.Select(step => step.delayBeforeMs));
	}

	/// <summary>加算モードで連続した複数行を削除すると、待機時間が次に残るステップまで累積して加算される</summary>
	[Fact]
	public void RemoveStepsAt_AddToNextStep_ContiguousRows_AccumulatesDelays() {
		var steps = new List<MacroStep> { Key(10), Move(40), Move(30), Down(delayMs: 5) };

		var removed = MacroStepEditor.RemoveStepsAt(steps, [1, 2], RemovedDelayHandling.AddToNextStep);

		Assert.Equal(2, removed);
		Assert.Equal([10, 75], steps.Select(step => step.delayBeforeMs));
	}

	/// <summary>離れた複数行を順不同で指定してもそれぞれ削除される</summary>
	[Fact]
	public void RemoveStepsAt_UnorderedIndices_RemovesEachRow() {
		var steps = new List<MacroStep> { Key(10), Move(40), Key(20), Move(30), Key(5) };

		var removed = MacroStepEditor.RemoveStepsAt(steps, [3, 1], RemovedDelayHandling.AddToNextStep);

		Assert.Equal(2, removed);
		Assert.Equal([10, 60, 35], steps.Select(step => step.delayBeforeMs));
	}

	/// <summary>加算モードで末尾のステップを削除すると、加算先が無いため待機時間は破棄される</summary>
	[Fact]
	public void RemoveStepsAt_AddToNextStep_TrailingStepIsDiscarded() {
		var steps = new List<MacroStep> { Key(10), Move(999) };

		MacroStepEditor.RemoveStepsAt(steps, [1], RemovedDelayHandling.AddToNextStep);

		var key = Assert.IsType<KeyDownStep>(Assert.Single(steps));
		Assert.Equal(10, key.delayBeforeMs);
	}

	/// <summary>先頭のステップを削除すると次のステップが先頭になり、加算モードで待機時間が加算される</summary>
	[Fact]
	public void RemoveStepsAt_FirstStep_AddsDelayToNewFirstStep() {
		var steps = new List<MacroStep> { Move(40), Key(10) };

		MacroStepEditor.RemoveStepsAt(steps, [0], RemovedDelayHandling.AddToNextStep);

		var key = Assert.IsType<KeyDownStep>(Assert.Single(steps));
		Assert.Equal(50, key.delayBeforeMs);
	}

	/// <summary>全ての行を削除するとステップ列が空になる</summary>
	[Fact]
	public void RemoveStepsAt_AllRows_ClearsSteps() {
		var steps = new List<MacroStep> { Key(10), Move(40) };

		var removed = MacroStepEditor.RemoveStepsAt(steps, [0, 1], RemovedDelayHandling.AddToNextStep);

		Assert.Equal(2, removed);
		Assert.Empty(steps);
	}

	/// <summary>範囲外の位置を含むと ArgumentOutOfRangeException が発生し、ステップ列は変化しない</summary>
	[Fact]
	public void RemoveStepsAt_IndexOutOfRange_Throws() {
		var steps = new List<MacroStep> { Key(10) };

		Assert.Throws<ArgumentOutOfRangeException>(() => MacroStepEditor.RemoveStepsAt(steps, [-1], RemovedDelayHandling.Discard));
		Assert.Throws<ArgumentOutOfRangeException>(() => MacroStepEditor.RemoveStepsAt(steps, [0, 1], RemovedDelayHandling.Discard));
		Assert.Single(steps);
	}

	/// <summary>中間位置へ挿入すると指定位置に格納され、挿入件数が返る</summary>
	[Fact]
	public void InsertStepsAt_InsertsAtMiddle() {
		var steps = new List<MacroStep> { Key(10), Down(delayMs: 20) };
		var inserted = Move(30);

		var count = MacroStepEditor.InsertStepsAt(steps, 1, [inserted]);

		Assert.Equal(1, count);
		Assert.Equal(3, steps.Count);
		Assert.Same(inserted, steps[1]);
	}

	/// <summary>複数件の挿入では順序が保たれる</summary>
	[Fact]
	public void InsertStepsAt_InsertsMultipleStepsInOrder() {
		var steps = new List<MacroStep> { Key(10) };
		var first = Down();
		var second = Up();

		var count = MacroStepEditor.InsertStepsAt(steps, 1, [first, second]);

		Assert.Equal(2, count);
		Assert.Same(first, steps[1]);
		Assert.Same(second, steps[2]);
	}

	/// <summary>位置 0 を指定すると先頭へ挿入される</summary>
	[Fact]
	public void InsertStepsAt_InsertsAtHead() {
		var steps = new List<MacroStep> { Key(10) };
		var inserted = Move();

		MacroStepEditor.InsertStepsAt(steps, 0, [inserted]);

		Assert.Same(inserted, steps[0]);
	}

	/// <summary>ステップ数と同じ位置を指定すると末尾へ追加され、空のステップ列にも挿入できる</summary>
	[Fact]
	public void InsertStepsAt_InsertsAtTail() {
		var steps = new List<MacroStep>();
		var first = Key(10);
		var second = Move();

		MacroStepEditor.InsertStepsAt(steps, 0, [first]);
		MacroStepEditor.InsertStepsAt(steps, steps.Count, [second]);

		Assert.Same(first, steps[0]);
		Assert.Same(second, steps[1]);
	}

	/// <summary>空のステップ列を挿入すると 0 件が返り、元の列は変化しない</summary>
	[Fact]
	public void InsertStepsAt_EmptyNewStepsReturnsZero() {
		var steps = new List<MacroStep> { Key(10) };

		var count = MacroStepEditor.InsertStepsAt(steps, 1, []);

		Assert.Equal(0, count);
		Assert.Single(steps);
	}

	/// <summary>範囲外の挿入位置では ArgumentOutOfRangeException が発生し、ステップ列は変化しない</summary>
	[Theory]
	[InlineData(-1)]
	[InlineData(2)]
	public void InsertStepsAt_ThrowsWhenIndexOutOfRange(int insertIndex) {
		var steps = new List<MacroStep> { Key(10) };

		Assert.Throws<ArgumentOutOfRangeException>(() => MacroStepEditor.InsertStepsAt(steps, insertIndex, [Move()]));
		Assert.Single(steps);
	}

	/// <summary>挿入しても既存ステップと挿入ステップの待機時間は変化しない</summary>
	[Fact]
	public void InsertStepsAt_KeepsDelayBeforeMs() {
		var steps = new List<MacroStep> { Key(10), Down(delayMs: 20) };

		MacroStepEditor.InsertStepsAt(steps, 1, [Move(30)]);

		Assert.Equal([10, 30, 20], steps.Select(step => step.delayBeforeMs));
	}

	/// <summary>しきい値ちょうどの待機時間は変換対象になる</summary>
	[Fact]
	public void UnifyDelays_ThresholdBoundary_IsConverted() {
		var steps = new List<MacroStep> { Down(delayMs: 50) };

		var changed = MacroStepEditor.UnifyDelays<MouseDownStep>(steps, thresholdMs: 50, newDelayMs: 30);

		Assert.Equal(1, changed);
		Assert.Equal(30, steps[0].delayBeforeMs);
	}

	/// <summary>しきい値未満の待機時間は変換されない</summary>
	[Fact]
	public void UnifyDelays_BelowThreshold_IsUnchanged() {
		var steps = new List<MacroStep> { Down(delayMs: 49) };

		var changed = MacroStepEditor.UnifyDelays<MouseDownStep>(steps, thresholdMs: 50, newDelayMs: 30);

		Assert.Equal(0, changed);
		Assert.Equal(49, steps[0].delayBeforeMs);
	}

	/// <summary>mouseDown を対象にした変換では他の種類のステップの待機時間が変わらない</summary>
	[Fact]
	public void UnifyDelays_TargetsOnlySpecifiedStepType() {
		var steps = new List<MacroStep> { Down(delayMs: 100), Up(delayMs: 100), Move(100), Key(100) };

		var changed = MacroStepEditor.UnifyDelays<MouseDownStep>(steps, thresholdMs: 50, newDelayMs: 50);

		Assert.Equal(1, changed);
		Assert.Equal([50, 100, 100, 100], steps.Select(step => step.delayBeforeMs));
	}

	/// <summary>既に変換後の値と同じ待機時間は変換件数に含まれない</summary>
	[Fact]
	public void UnifyDelays_AlreadyAtNewDelay_IsNotCounted() {
		var steps = new List<MacroStep> { Down(delayMs: 50), Down(delayMs: 60) };

		var changed = MacroStepEditor.UnifyDelays<MouseDownStep>(steps, thresholdMs: 50, newDelayMs: 50);

		Assert.Equal(1, changed);
	}

	/// <summary>変換後の値がしきい値より大きい場合 ( 増やす方向 ) でも変換される</summary>
	[Fact]
	public void UnifyDelays_NewDelayAboveThreshold_Works() {
		var steps = new List<MacroStep> { Down(delayMs: 60) };

		var changed = MacroStepEditor.UnifyDelays<MouseDownStep>(steps, thresholdMs: 50, newDelayMs: 80);

		Assert.Equal(1, changed);
		Assert.Equal(80, steps[0].delayBeforeMs);
	}

	/// <summary>空のステップ列では 0 件が返る</summary>
	[Fact]
	public void UnifyDelays_EmptySteps_ReturnsZero() {
		Assert.Equal(0, MacroStepEditor.UnifyDelays<MouseUpStep>([], thresholdMs: 20, newDelayMs: 20));
	}

	/// <summary>負のしきい値または変換後の値で ArgumentOutOfRangeException が発生する</summary>
	[Fact]
	public void UnifyDelays_NegativeArguments_Throws() {
		Assert.Throws<ArgumentOutOfRangeException>(() => MacroStepEditor.UnifyDelays<MouseDownStep>([], -1, 50));
		Assert.Throws<ArgumentOutOfRangeException>(() => MacroStepEditor.UnifyDelays<MouseDownStep>([], 50, -1));
	}

	/// <summary>全ステップの待機時間の合計が推定実行時間として返る</summary>
	[Fact]
	public void CalculateTotalDurationMs_SumsAllDelays() {
		var steps = new List<MacroStep> { Key(100), Move(20), Down(delayMs: 30), Up(delayMs: 50) };

		Assert.Equal(200, MacroStepEditor.CalculateTotalDurationMs(steps));
	}

	/// <summary>空のステップ列では推定実行時間が 0 になる</summary>
	[Fact]
	public void CalculateTotalDurationMs_EmptySteps_ReturnsZero() {
		Assert.Equal(0, MacroStepEditor.CalculateTotalDurationMs([]));
	}

	/// <summary>int の範囲を超える合計でもオーバーフローせずに計算できる</summary>
	[Fact]
	public void CalculateTotalDurationMs_LargeDelays_DoesNotOverflow() {
		var steps = new List<MacroStep> { Key(int.MaxValue), Key(int.MaxValue) };

		Assert.Equal(int.MaxValue * 2L, MacroStepEditor.CalculateTotalDurationMs(steps));
	}

	/// <summary>奇数個の統計では中央値がソート後の中央要素になる</summary>
	[Fact]
	public void CalculateDelayStatistics_OddCount_MedianIsMiddleValue() {
		var steps = new List<MacroStep> { Down(delayMs: 10), Down(delayMs: 300), Down(delayMs: 50) };

		var statistics = MacroStepEditor.CalculateDelayStatistics<MouseDownStep>(steps);

		Assert.NotNull(statistics);
		Assert.Equal(3, statistics.count);
		Assert.Equal(10, statistics.minimumMs);
		Assert.Equal(300, statistics.maximumMs);
		Assert.Equal(50, statistics.medianMs);
	}

	/// <summary>偶数個の統計では中央値が中央 2 値の平均になる</summary>
	[Fact]
	public void CalculateDelayStatistics_EvenCount_MedianIsAverageOfMiddleValues() {
		var steps = new List<MacroStep> { Down(delayMs: 21), Down(delayMs: 100), Down(delayMs: 10), Down(delayMs: 5) };

		var statistics = MacroStepEditor.CalculateDelayStatistics<MouseDownStep>(steps);

		Assert.NotNull(statistics);
		Assert.Equal(15.5, statistics.medianMs);
	}

	/// <summary>対象が 1 件のみの場合は最小・最大・中央値が全て同じ値になる</summary>
	[Fact]
	public void CalculateDelayStatistics_SingleStep_AllValuesEqual() {
		var steps = new List<MacroStep> { Up(delayMs: 42) };

		var statistics = MacroStepEditor.CalculateDelayStatistics<MouseUpStep>(steps);

		Assert.NotNull(statistics);
		Assert.Equal(new DelayStatistics(1, 42, 42, 42), statistics);
	}

	/// <summary>対象の種類のステップが存在しない場合は null が返る</summary>
	[Fact]
	public void CalculateDelayStatistics_NoTargetSteps_ReturnsNull() {
		var steps = new List<MacroStep> { Move(10), Key(20) };

		Assert.Null(MacroStepEditor.CalculateDelayStatistics<MouseDownStep>(steps));
	}

	/// <summary>mouseDown の統計に mouseUp の待機時間が混入しない</summary>
	[Fact]
	public void CalculateDelayStatistics_FiltersByStepType() {
		var steps = new List<MacroStep> { Down(delayMs: 10), Up(delayMs: 9999) };

		var statistics = MacroStepEditor.CalculateDelayStatistics<MouseDownStep>(steps);

		Assert.NotNull(statistics);
		Assert.Equal(10, statistics.maximumMs);
	}
}
