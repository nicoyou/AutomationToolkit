using System.Diagnostics;
using AutomationToolkit.Core.Hooks;
using AutomationToolkit.Core.Interop;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Recording;

/// <summary>マクロ録画機能を提供する</summary>
public interface IMacroRecorder
{
    /// <summary>録画中かどうか</summary>
    bool IsRecording { get; }

    /// <summary>ステップを記録するたびに現在のステップ数を通知するイベント</summary>
    event EventHandler<int>? StepRecorded;

    /// <summary>録画状態が変化したときに発生するイベント</summary>
    event EventHandler<bool>? IsRecordingChanged;

    /// <summary>録画を開始する</summary>
    /// <param name="options">録画の動作設定。null なら既定値</param>
    void Start(RecordingOptions? options = null);

    /// <summary>録画を停止し、記録したステップからマクロを生成する</summary>
    /// <returns>記録したステップを持つマクロ</returns>
    Macro Stop();
}

/// <summary>LowLevelInputHook のイベントをマクロステップ列に変換する録画エンジン</summary>
/// <remarks>
/// 再生による合成入力は記録しない。
/// ホットキーのメインキーはフック側で飲み込まれるため届かないが、
/// Ctrl や Alt など修飾キーの押下・解放の残骸は Stop 時にトリムする
/// </remarks>
public sealed class MacroRecorder : IMacroRecorder
{
    /// <summary>録画状態とステップ列の更新を直列化するロック</summary>
    private readonly Lock _gate = new();

    /// <summary>記録中のステップ列</summary>
    private readonly List<MacroStep> _steps = [];

    /// <summary>録画中かどうか</summary>
    private bool _isRecording;

    /// <summary>現在の録画の動作設定</summary>
    private RecordingOptions _options = new();

    /// <summary>マウス移動の間引き判定器</summary>
    private MouseMoveThinner? _thinner;

    /// <summary>最後にステップを記録した時刻の Stopwatch タイムスタンプ</summary>
    private long _lastEmitTicks;

    /// <summary>間引きで保留中のマウス移動。直後のクリックの直前位置として採用する</summary>
    private (int X, int Y, long Ticks)? _pendingMove;

    /// <inheritdoc/>
    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _isRecording;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<int>? StepRecorded;

    /// <inheritdoc/>
    public event EventHandler<bool>? IsRecordingChanged;

    /// <summary>入力フックのイベントを購読する</summary>
    /// <param name="hook">入力イベントの供給元となる入力フック</param>
    public MacroRecorder(LowLevelInputHook hook) => hook.InputReceived += OnInput;

    /// <inheritdoc/>
    public void Start(RecordingOptions? options = null)
    {
        lock (_gate)
        {
            if (_isRecording)
            {
                throw new InvalidOperationException("すでに録画中です。");
            }
            _options = options ?? new RecordingOptions();
            _steps.Clear();
            _pendingMove = null;
            _thinner = new MouseMoveThinner(
                _options.MoveMinDistancePx,
                PrecisionDelay_MsToTicks(_options.MoveMinIntervalMs));
            _lastEmitTicks = Stopwatch.GetTimestamp();
            _isRecording = true;
        }
        IsRecordingChanged?.Invoke(this, true);
    }

    /// <inheritdoc/>
    public Macro Stop()
    {
        Macro macro;
        lock (_gate)
        {
            if (!_isRecording)
            {
                throw new InvalidOperationException("録画中ではありません。");
            }
            _isRecording = false;
            // 末尾の未確定 move はクリックに繋がらないノイズなので捨てる
            _pendingMove = null;

            var steps = new List<MacroStep>(_steps);
            TrimHotkeyArtifacts(steps);
            macro = new Macro
            {
                CreatedUtc = DateTimeOffset.UtcNow,
                Steps = steps,
            };
            _steps.Clear();
        }
        IsRecordingChanged?.Invoke(this, false);
        return macro;
    }

    /// <summary>入力イベントをステップとして記録する</summary>
    /// <remarks>フックの配信タスク上で順序どおりに呼ばれる</remarks>
    /// <param name="e">記録する入力イベント</param>
    private void OnInput(RawInputEvent e)
    {
        lock (_gate)
        {
            if (!_isRecording || e.IsInjected)
            {
                return;
            }

            switch (e.Kind)
            {
                case RawInputKind.MouseMove:
                    if (!_options.RecordMouseMoves)
                    {
                        break;
                    }
                    if (_thinner!.ShouldEmit(e.X, e.Y, e.TimestampTicks))
                    {
                        _pendingMove = null;
                        EmitMove(e.X, e.Y, e.TimestampTicks);
                    }
                    else
                    {
                        // すぐには記録しないが、直後にクリックが来たら直前位置として採用する
                        _pendingMove = (e.X, e.Y, e.TimestampTicks);
                    }
                    break;

                case RawInputKind.MouseDown:
                    FlushPendingMove();
                    Emit(new MouseDownStep { Button = e.Button, X = e.X, Y = e.Y }, e.TimestampTicks);
                    _thinner!.MarkEmitted(e.X, e.Y, e.TimestampTicks);
                    break;

                case RawInputKind.MouseUp:
                    FlushPendingMove();
                    Emit(new MouseUpStep { Button = e.Button, X = e.X, Y = e.Y }, e.TimestampTicks);
                    _thinner!.MarkEmitted(e.X, e.Y, e.TimestampTicks);
                    break;

                case RawInputKind.MouseWheel:
                    FlushPendingMove();
                    Emit(new MouseWheelStep
                    {
                        X = e.X,
                        Y = e.Y,
                        Delta = e.WheelDelta,
                        IsHorizontal = e.IsHorizontalWheel,
                    }, e.TimestampTicks);
                    _thinner!.MarkEmitted(e.X, e.Y, e.TimestampTicks);
                    break;

                case RawInputKind.KeyDown:
                    Emit(new KeyDownStep
                    {
                        VirtualKey = e.VirtualKey,
                        ScanCode = e.ScanCode,
                        IsExtended = e.IsExtended,
                    }, e.TimestampTicks);
                    break;

                case RawInputKind.KeyUp:
                    Emit(new KeyUpStep
                    {
                        VirtualKey = e.VirtualKey,
                        ScanCode = e.ScanCode,
                        IsExtended = e.IsExtended,
                    }, e.TimestampTicks);
                    break;
            }
        }
    }

    /// <summary>保留中のマウス移動があれば記録する</summary>
    private void FlushPendingMove()
    {
        if (_pendingMove is (var x, var y, var ticks))
        {
            _pendingMove = null;
            EmitMove(x, y, ticks);
        }
    }

    /// <summary>カーソル移動のステップを記録し、間引きの基準位置を更新する</summary>
    /// <param name="x">カーソルの X 座標</param>
    /// <param name="y">カーソルの Y 座標</param>
    /// <param name="ticks">イベント発生時刻の Stopwatch タイムスタンプ</param>
    private void EmitMove(int x, int y, long ticks)
    {
        Emit(new MouseMoveStep { X = x, Y = y }, ticks);
        _thinner!.MarkEmitted(x, y, ticks);
    }

    /// <summary>前ステップからの待機時間を計算してステップを記録する</summary>
    /// <param name="step">記録するステップ</param>
    /// <param name="timestampTicks">イベント発生時刻の Stopwatch タイムスタンプ</param>
    private void Emit(MacroStep step, long timestampTicks)
    {
        step.DelayBeforeMs = (int)Math.Max(0,
            Math.Round((timestampTicks - _lastEmitTicks) * 1000.0 / Stopwatch.Frequency));
        _lastEmitTicks = timestampTicks;
        _steps.Add(step);
        StepRecorded?.Invoke(this, _steps.Count);
    }

    /// <summary>ミリ秒を Stopwatch のタイムスタンプ刻みへ変換する</summary>
    /// <param name="ms">変換するミリ秒数</param>
    /// <returns>Stopwatch のタイムスタンプ刻み</returns>
    private static long PrecisionDelay_MsToTicks(int ms)
        => (long)(ms * (Stopwatch.Frequency / 1000.0));

    /// <summary>録画開始・停止ホットキーの残骸をトリムする</summary>
    /// <remarks>
    /// 先頭側は対応する KeyDown が記録されていない KeyUp を開始ホットキーの離しとして除去し、
    /// 末尾側は対応する KeyUp がない修飾キーの KeyDown を停止ホットキーの押し込みとして除去する
    /// </remarks>
    /// <param name="steps">トリム対象のステップ列</param>
    internal static void TrimHotkeyArtifacts(List<MacroStep> steps)
    {
        var seenDownVks = new HashSet<ushort>();
        for (var i = 0; i < steps.Count;)
        {
            if (steps[i] is KeyDownStep down)
            {
                seenDownVks.Add(down.VirtualKey);
                i++;
            }
            else if (steps[i] is KeyUpStep up && !seenDownVks.Contains(up.VirtualKey))
            {
                // タイミングを保つため、除去したステップの待機時間は次のステップへ引き継ぐ
                if (i + 1 < steps.Count)
                {
                    steps[i + 1].DelayBeforeMs += steps[i].DelayBeforeMs;
                }
                steps.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }

        while (steps.Count > 0
            && steps[^1] is KeyDownStep trailing
            && IsModifierVk(trailing.VirtualKey)
            && !steps.Any(s => s is KeyUpStep u && u.VirtualKey == trailing.VirtualKey))
        {
            steps.RemoveAt(steps.Count - 1);
        }
    }

    /// <summary>修飾キーの仮想キーコードかどうかを判定する</summary>
    /// <param name="vk">判定する仮想キーコード</param>
    /// <returns>修飾キーなら true</returns>
    private static bool IsModifierVk(ushort vk) => vk is
        Win32.VK_SHIFT or Win32.VK_CONTROL or Win32.VK_MENU or
        Win32.VK_LWIN or Win32.VK_RWIN or
        Win32.VK_LSHIFT or Win32.VK_RSHIFT or
        Win32.VK_LCONTROL or Win32.VK_RCONTROL or
        Win32.VK_LMENU or Win32.VK_RMENU;
}
