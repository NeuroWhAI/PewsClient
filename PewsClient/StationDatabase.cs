using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PewsClient
{
    class StationDatabase
    {
        public struct Station
        {
            public static readonly Station Empty = new Station
            {
                Name = string.Empty,
                Location = string.Empty,
                Y = 0,
                X = 0,
            };

            public string Name;
            public string Location;
            public double Y, X;
        }

        public int StationCount => m_stations.Count;

        private List<Station> m_stations = new List<Station>();
        private readonly double AroundRadius = 24;

        public void LoadDatabase(string csvFile)
        {
            m_stations.Clear();

            if (!File.Exists(csvFile))
            {
                return;
            }

            using (var sr = new StreamReader(new FileStream(csvFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string[] data = line.Split(',');
                    if (data.Length < 4)
                    {
                        continue;
                    }

                    if (double.TryParse(data[2], out double lat)
                        && double.TryParse(data[3], out double lon))
                    {
                        var stn = new Station
                        {
                            Name = data[0],
                            Location = data[1],
                            Y = LatToY(lat),
                            X = LonToX(lon),
                        };

                        m_stations.Add(stn);
                    }
                }
            }
        }

        public Station GetStationInfoAround(double latitude, double longitude)
        {
            double y = LatToY(latitude);
            double x = LonToX(longitude);

            double minDistanceSqr = -1;
            int minIndex = -1;

            for (int i = 0; i < m_stations.Count; ++i)
            {
                var stn = m_stations[i];

                double subY = stn.Y - y;
                double subX = stn.X - x;

                double distanceSqr = subX * subX + subY * subY;
                if (distanceSqr < AroundRadius * AroundRadius)
                {
                    if (minIndex < 0)
                    {
                        minIndex = i;
                        minDistanceSqr = distanceSqr;
                    }
                    else if (minDistanceSqr > distanceSqr)
                    {
                        minIndex = i;
                        minDistanceSqr = distanceSqr;
                    }
                }
            }

            if (minIndex < 0)
            {
                return Station.Empty;
            }
            else
            {
                return m_stations[minIndex];
            }
        }

        private static double LonToX(double longitude)
        {
            return (longitude - 124.5) * 113;
        }

        private static double LatToY(double latitude)
        {
            return (38.9 - latitude) * 138.4;
        }
    }
}
