using System.Diagnostics;
using AutomationToolkit.Core.Interop;

namespace AutomationToolkit.Core.Playback;

/// <summary>Task.Delay の分解能を超える精度の待機を提供する</summary>
/// <remarks>残り時間が大きいうちは Task.Delay で粗く待ち、最後の約 20ms は Stopwatch スピンで待つハイブリッド方式</remarks>
public static class PrecisionDelay
{
    /// <summary>再生セッション中だけシステムタイマーを 1ms 分解能に引き上げる</summary>
    /// <returns>Dispose で分解能を元に戻すスコープ</returns>
    public static IDisposable BeginHighResolutionTimers()
    {
        NativeMethods.timeBeginPeriod(1);
        return new TimePeriodScope();
    }

    /// <summary>Dispose でシステムタイマーの分解能を元に戻すスコープ</summary>
    private sealed class TimePeriodScope : IDisposable
    {
        /// <summary>すでに Dispose 済みかどうか</summary>
        private bool _disposed;

        /// <summary>システムタイマーの分解能を元に戻す</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                NativeMethods.timeEndPeriod(1);
            }
        }
    }

    /// <summary>ミリ秒を Stopwatch のタイムスタンプ刻みへ変換する</summary>
    /// <param name="milliseconds">変換するミリ秒数</param>
    /// <returns>Stopwatch のタイムスタンプ刻み</returns>
    public static long MsToTimestampTicks(double milliseconds)
        => (long)(milliseconds * Stopwatch.Frequency / 1000.0);

    /// <summary>指定した Stopwatch タイムスタンプの絶対時刻まで待機する</summary>
    /// <param name="targetTimestamp">待機終了時刻の Stopwatch タイムスタンプ</param>
    /// <param name="ct">待機を中断するためのキャンセルトークン</param>
    /// <returns>待機の完了を表すタスク</returns>
    public static async Task WaitUntilAsync(long targetTimestamp, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var remainingMs = (targetTimestamp - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
            if (remainingMs <= 0)
            {
                return;
            }
            if (remainingMs <= 20)
            {
                break;
            }
            // 分解能ぶんの余裕を残して粗く待つ
            await Task.Delay((int)remainingMs - 16, ct).ConfigureAwait(false);
        }

        while (Stopwatch.GetTimestamp() < targetTimestamp)
        {
            ct.ThrowIfCancellationRequested();
            Thread.SpinWait(64);
        }
    }
}
