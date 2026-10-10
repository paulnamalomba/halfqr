using System.Globalization;

namespace HalfQR.QrEngine.Rendering;

internal static class SvgNumberFormat
{
    public static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
