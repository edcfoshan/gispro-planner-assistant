namespace GisProAddinWorkbench.AddIn.Services;

internal static class CoordinateTransforms
{
    private const double Pi = Math.PI;
    private const double Axis = 6378245.0;
    private const double Offset = 0.00669342162296594323;
    private const double XPi = Pi * 3000.0 / 180.0;

    internal static (double Longitude, double Latitude) Gcj02ToWgs84(double longitude, double latitude)
    {
        if (IsOutOfChina(longitude, latitude))
        {
            return (longitude, latitude);
        }

        var (dLon, dLat) = Delta(longitude, latitude);
        return (longitude - dLon, latitude - dLat);
    }

    internal static (double Longitude, double Latitude) Wgs84ToGcj02(double longitude, double latitude)
    {
        if (IsOutOfChina(longitude, latitude))
        {
            return (longitude, latitude);
        }

        var (dLon, dLat) = Delta(longitude, latitude);
        return (longitude + dLon, latitude + dLat);
    }

    internal static (double Longitude, double Latitude) Bd09ToWgs84(double longitude, double latitude)
    {
        var (gcjLon, gcjLat) = Bd09ToGcj02(longitude, latitude);
        return Gcj02ToWgs84(gcjLon, gcjLat);
    }

    internal static (double Longitude, double Latitude) Bd09ToGcj02(double longitude, double latitude)
    {
        var x = longitude - 0.0065;
        var y = latitude - 0.006;
        var z = Math.Sqrt((x * x) + (y * y)) - (0.00002 * Math.Sin(y * XPi));
        var theta = Math.Atan2(y, x) - (0.000003 * Math.Cos(x * XPi));
        return (z * Math.Cos(theta), z * Math.Sin(theta));
    }

    internal static (double Longitude, double Latitude) Gcj02ToBd09(double longitude, double latitude)
    {
        var z = Math.Sqrt((longitude * longitude) + (latitude * latitude)) + (0.00002 * Math.Sin(latitude * XPi));
        var theta = Math.Atan2(latitude, longitude) + (0.000003 * Math.Cos(longitude * XPi));
        return (z * Math.Cos(theta) + 0.0065, z * Math.Sin(theta) + 0.006);
    }

    internal static (double Longitude, double Latitude) Wgs84ToBd09(double longitude, double latitude)
    {
        var (gcjLon, gcjLat) = Wgs84ToGcj02(longitude, latitude);
        return Gcj02ToBd09(gcjLon, gcjLat);
    }

    private static (double Longitude, double Latitude) Delta(double longitude, double latitude)
    {
        var dLat = TransformLatitude(longitude - 105.0, latitude - 35.0);
        var dLon = TransformLongitude(longitude - 105.0, latitude - 35.0);
        var radLat = latitude / 180.0 * Pi;
        var magic = Math.Sin(radLat);
        magic = 1 - Offset * magic * magic;
        var sqrtMagic = Math.Sqrt(magic);
        dLat = (dLat * 180.0) / (((Axis * (1 - Offset)) / (magic * sqrtMagic)) * Pi);
        dLon = (dLon * 180.0) / ((Axis / sqrtMagic) * Math.Cos(radLat) * Pi);
        return (dLon, dLat);
    }

    private static double TransformLatitude(double longitude, double latitude)
    {
        var result = -100.0 + (2.0 * longitude) + (3.0 * latitude) + (0.2 * latitude * latitude) +
                     (0.1 * longitude * latitude) + (0.2 * Math.Sqrt(Math.Abs(longitude)));
        result += ((20.0 * Math.Sin(6.0 * longitude * Pi)) + (20.0 * Math.Sin(2.0 * longitude * Pi))) * 2.0 / 3.0;
        result += ((20.0 * Math.Sin(latitude * Pi)) + (40.0 * Math.Sin(latitude / 3.0 * Pi))) * 2.0 / 3.0;
        result += ((160.0 * Math.Sin(latitude / 12.0 * Pi)) + (320 * Math.Sin(latitude * Pi / 30.0))) * 2.0 / 3.0;
        return result;
    }

    private static double TransformLongitude(double longitude, double latitude)
    {
        var result = 300.0 + longitude + (2.0 * latitude) + (0.1 * longitude * longitude) +
                     (0.1 * longitude * latitude) + (0.1 * Math.Sqrt(Math.Abs(longitude)));
        result += ((20.0 * Math.Sin(6.0 * longitude * Pi)) + (20.0 * Math.Sin(2.0 * longitude * Pi))) * 2.0 / 3.0;
        result += ((20.0 * Math.Sin(longitude * Pi)) + (40.0 * Math.Sin(longitude / 3.0 * Pi))) * 2.0 / 3.0;
        result += ((150.0 * Math.Sin(longitude / 12.0 * Pi)) + (300.0 * Math.Sin(longitude / 30.0 * Pi))) * 2.0 / 3.0;
        return result;
    }

    private static bool IsOutOfChina(double longitude, double latitude)
    {
        return longitude is < 72.004 or > 137.8347 || latitude is < 0.8293 or > 55.8271;
    }
}
