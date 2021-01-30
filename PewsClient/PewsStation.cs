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
        public double Longitude
        {
            get => m_longitude;
            set
            {
                m_longitude = value;
                X = Util.LonToX(value);
            }
        }
        private double m_longitude = 0;

        /// <summary>
        /// 위도
        /// </summary>
        public double Latitude
        {
            get => m_latitude;
            set
            {
                m_latitude = value;
                Y = Util.LatToY(value);
            }
        }
        private double m_latitude = 0;

        /// <summary>
        /// 지도 이미지에서의 X 위치.
        /// </summary>
        public double X { get; private set; } = 0;

        /// <summary>
        /// 지도 이미지에서의 Y 위치.
        /// </summary>
        public double Y { get; private set; } = 0;

        /// <summary>
        /// 렌더링을 위한 MMI 값.
        /// 0~10은 그대로 각 진도를 의미하지만 12~14는 세분화 된 진도 1을 의미.
        /// </summary>
        public int RawMmi { get; set; } = 0;

        /// <summary>
        /// 진도 데이터를 빠르게 접근하기 위한 배열.
        /// </summary>
        public int[] MmiData { get; } = new int[2] { 0, 1 };
        public static readonly int IndexMmi = 0;
        public static readonly int IndexMaxMmi = 1;

        /// <summary>
        /// 진도
        /// </summary>
        public int Mmi
        {
            get => MmiData[0];
            private set => MmiData[0] = value;
        }

        /// <summary>
        /// 최대 진도
        /// </summary>
        public int MaxMmi
        {
            get => MmiData[1];
            private set => MmiData[1] = value;
        }

        /// <summary>
        /// 클러스터 분석을 위한 인근 관측소 번호들.
        /// </summary>
        public List<int> Nodes { get; set; } = new List<int>();

        private DateTime m_mmiLife = DateTime.MinValue;

        public void UpdateMmi(int newMmi, TimeSpan lifetime)
        {
            Mmi = newMmi;

            // 이번 진도가 최대 진도 이상이거나 초기화 시간이 되었다면 최대 진도와 초기화 시간 갱신.
            if (newMmi >= MaxMmi || DateTime.UtcNow >= m_mmiLife)
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
