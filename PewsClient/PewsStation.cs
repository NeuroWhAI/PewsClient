using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PewsClient
{
    class PewsStation
    {
        /// <summary>
        /// 경도
        /// </summary>
        public double Longitude { get; set; } = 0;

        /// <summary>
        /// 위도
        /// </summary>
        public double Latitude { get; set; } = 0;

        /// <summary>
        /// 진도
        /// </summary>
        public int Mmi { get; set; } = 0;

        /// <summary>
        /// 클러스터 분석을 위한 인근 관측소 번호들.
        /// </summary>
        public List<int> Nodes { get; set; } = new List<int>();
    }
}
