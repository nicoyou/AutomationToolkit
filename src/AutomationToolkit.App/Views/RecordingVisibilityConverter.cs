using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AutomationToolkit.App.ViewModels;

namespace AutomationToolkit.App.Views;

/// <summary>録画中のときだけ Visible を返すコンバーター</summary>
public sealed class RecordingVisibilityConverter : IValueConverter
{
    /// <summary>動作状態を表示可否へ変換する</summary>
    /// <param name="value">変換元の AppState</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">コンバーターのパラメータ</param>
    /// <param name="culture">変換に使うカルチャ</param>
    /// <returns>録画中なら Visible、それ以外は Collapsed</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AppState.Recording ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>逆変換は未対応</summary>
    /// <param name="value">変換元の値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">コンバーターのパラメータ</param>
    /// <param name="culture">変換に使うカルチャ</param>
    /// <returns>常に例外を投げるため戻り値はない</returns>
    /// <exception cref="NotSupportedException">常に発生する</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
