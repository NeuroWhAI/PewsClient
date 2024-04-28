using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PewsClient
{
    static class Util
    {
        public static double LonToX(double longitude)
        {
            return (longitude - 124.5) * 113;
        }

        public static double LatToY(double latitude)
        {
            return (38.9 - latitude) * 138.4;
        }

        public static double XToLon(double x)
        {
            return x / 113 + 124.5;
        }

        public static double YToLat(double y)
        {
            return -y / 138.4 + 38.9;
        }
    }

    static class DoubleExtension
    {
        public static bool AlmostEqualTo(this double value1, double value2)
        {
            return Math.Abs(value1 - value2) < 0.0000001;
        }
    }
}
