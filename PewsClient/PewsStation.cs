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
        /// 이름
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 지역
        /// </summary>
        public string Location { get; set; } = string.Empty;

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
        public int Mmi { get; private set; } = 0;

        /// <summary>
        /// 최대 진도
        /// </summary>
        public int MaxMmi { get; private set; } = 1;

        /// <summary>
        /// 클러스터 분석을 위한 인근 관측소 번호들.
        /// </summary>
        public List<int> Nodes { get; set; } = new List<int>();

        private DateTime m_mmiLife = DateTime.MinValue;

        public void UpdateMmi(int newMmi, int phase, TimeSpan lifetime)
        {
            Mmi = newMmi;

            // 이번 진도가 최대 진도를 넘었거나 지진 속보가 없을 때 초기화 시간이 되었다면 최대 진도 갱신.
            if (newMmi > MaxMmi || (phase != 2 && DateTime.UtcNow >= m_mmiLife))
            {
                MaxMmi = newMmi;
                m_mmiLife = DateTime.UtcNow + lifetime;
            }
        }

        public void ResetMaxMmi()
        {
            MaxMmi = 1;
            m_mmiLife = DateTime.MinValue;
        }
    }
}
