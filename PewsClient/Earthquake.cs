using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PewsClient
{
    static class Earthquake
    {
        private static readonly double[] MinGal =
        {
            0,
            0,
            0.6864655,
            2.2555295,
            7.453054,
            25.105024,
            67.273619,
            144.4519545,
            310.478539,
            666.9502665,
            1433.143831,
            3079.2881,
        };

        private static readonly string[] MmiString =
        {
            "Ⅰ-", "Ⅰ", "Ⅱ", "Ⅳ", "Ⅳ", "Ⅴ", "Ⅵ", "Ⅶ", "Ⅷ", "Ⅸ", "Ⅹ", "Ⅺ", "Ⅻ", "Ⅻ+"
        };

        public static int ConvertPgaToMMI(double pga)
        {
            // NOTE: 기상청 MMI scale
            // %g=9.80665cm/s^2

            if (pga <= 0)
                return 0;
            if (pga < 0.07)
                return 1;
            if (pga < 0.23)
                return 2;
            if (pga < 0.76)
                return 3;
            if (pga < 2.56)
                return 4;
            if (pga < 6.86)
                return 5;
            if (pga < 14.73)
                return 6;
            if (pga < 31.66)
                return 7;
            if (pga < 68.01)
                return 8;
            if (pga < 146.14)
                return 9;
            if (pga < 314)
                return 10;
            return 11;
        }

        public static int ConvertPgvToMMI(double pgv)
        {
            // NOTE: 기상청 MMI scale
            // V=cm/s

            if (pgv <= 0)
                return 0;
            if (pgv < 0.03)
                return 1;
            if (pgv < 0.07)
                return 2;
            if (pgv < 0.19)
                return 3;
            if (pgv < 0.54)
                return 4;
            if (pgv < 1.46)
                return 5;
            if (pgv < 3.7)
                return 6;
            if (pgv < 9.39)
                return 7;
            if (pgv < 23.85)
                return 8;
            if (pgv < 60.61)
                return 9;
            if (pgv < 154)
                return 10;
            return 11;
        }

        public static string MMIToString(int mmi)
        {
            if (mmi < 0)
                return MmiString.First();
            if (mmi >= MmiString.Length)
                return MmiString.Last();

            return MmiString[mmi];
        }
        
        public static double MMIToMinimumGal(int mmi)
        {
            if (mmi < 0)
                return MinGal.First();
            if (mmi >= MinGal.Length)
                return MinGal.Last();

            return MinGal[mmi];
        }

        public static string GetKnowHowFromMMI(int mmi)
        {
            StringBuilder str = new StringBuilder("");
            str.AppendLine($"[진도(MMI) {MMIToString(mmi)} 특징]");


            if (mmi <= 0)
            {
                str.AppendLine("무감.");
            }
            else if (mmi == 1)
            {
                str.AppendLine("미세한 진동. 특수한 조건에서 극히 소수 느낌.");
            }
            else if (mmi == 2)
            {
                str.AppendLine("실내에서 극히 소수 느낌.");
            }
            else if (mmi == 3)
            {
                str.AppendLine("실내에서 소수 느낌. 매달린 물체가 약하게 움직임.");
            }
            else if (mmi == 4)
            {
                str.AppendLine("실내에서 다수 느낌. 실외에서는 감지하지 못함.");
                str.AppendLine("일부의 사람들이 잠에서 깸. 사물이 떨리는 소리가 들림.");
            }
            else if (mmi == 5)
            {
                str.AppendLine("건물 전체가 흔들림. 물체의 파손, 뒤집힘, 추락.");
                str.AppendLine("가벼운 물체의 위치 이동. 다수의 사람들이 잠에서 깸.");
            }
            else if (mmi == 6)
            {
                str.AppendLine("똑바로 걷기 어려움. 약한 건물의 회벽이 떨어지거나 금이 감.");
                str.AppendLine("무거운 물체의 이동 또는 뒤집힘.");
            }
            else if (mmi == 7)
            {
                str.AppendLine("서 있기 곤란함. 운전 중에도 지진을 느낌.");
                str.AppendLine("회벽이 무너지고 느슨한 적재물과 담장이 무너짐.");
                str.AppendLine("보통의 건물들에 경미한 손상.");
            }
            else if (mmi == 8)
            {
                str.AppendLine("차량운전 곤란. 일부 건물 붕괴.");
                str.AppendLine("사면이나 지표의 균열.탑·굴뚝 붕괴.");
            }
            else if (mmi == 9)
            {
                str.AppendLine("견고한 건물의 피해가 심하거나 붕괴.");
                str.AppendLine("지표의 균열이 발생하고 지하 파이프관 파손.");
            }
            else if (mmi == 10)
            {
                str.AppendLine("대다수 견고한 건물과 구조물 파괴.");
                str.AppendLine("지표균열, 대규모 사태, 아스팔트 균열.");
            }
            else if (mmi == 11)
            {
                str.AppendLine("철로가 심하게 휨. 구조물 거의 파괴. 지하 파이프관 작동 불가능.");
            }
            else if (mmi == 12)
            {
                str.AppendLine("천재지변. 모든 것이 완파된다.");
                str.AppendLine("지면이 파도 형태로 움직임.물체가 공중으로 튀어오름.");
                str.AppendLine("큰 바위가 굴러 떨어짐.강의 경로가 바뀜.");
            }


            return str.ToString().TrimEnd();
        }

        public static string GetKnowHowFromMScale(double richterMScale)
        {
            if (richterMScale < 0.0)
                return "";


            StringBuilder str = new StringBuilder("");
            str.AppendLine($"[국내 규모{richterMScale.ToString("F1")} 지진 발생시 행동요령]");


            if (richterMScale < 2.6)
            {
                // 민감한 사람은 느낄 수 있음.

                str.AppendLine("비교적 좁은 범위에서 민감한 사람이 진동을 감지할 수 있습니다.");
                str.AppendLine("우려되는 피해는 없으며 침착하시고 소식에 귀를 기울여주시기 바랍니다.");
            }
            else if (richterMScale < 3.2)
            {
                // 가만히 있던 사람은 느낄 수 있음.

                str.AppendLine("비교적 좁은 범위에서 다수의 사람들이 진동을 감지할 수 있습니다.");
                str.AppendLine("우려되는 피해는 없으며 침착하시고 소식에 귀를 기울여주시기 바랍니다.");
            }
            else if (richterMScale < 4.0)
            {
                // 좁은 범위에서 많은 사람이 느끼고 사물이 움직일 수 있음.

                str.AppendLine("넓은 범위에서 많은 사람들이 진동을 감지할 수 있습니다.");
                str.AppendLine("떨어지기 쉬운 물건을 정비하시고 어려울 경우 떨어져 계셔야 합니다.");
                str.AppendLine("크게 우려되는 피해는 없으며 소식에 귀를 기울여주시기 바랍니다.");
            }
            else if (richterMScale < 5.0)
            {
                // 넓은 범위에서 많은 사람이 느끼고 낮은 확률로 피해가 발생할 수 있음.

                str.AppendLine("넓은 범위에서 대부분의 사람들이 진동을 감지할 수 있으며");
                str.AppendLine("비교적 좁은 범위에서 강한 진동이 감지됩니다.");
                str.AppendLine("물건이 떨어질 수 있으므로 조심하시고");
                str.AppendLine("소식에 귀를 기울여주시기 바랍니다.");
            }
            else if (richterMScale < 6.0)
            {
                // 매우 넓은 범위에서 많은 사람이 느끼고 매우 높은 확률로 피해가 발생함.

                str.AppendLine("넓은 범위에서 강한 진동이 감지되며");
                str.AppendLine("비교적 좁은 범위에서 큰 피해가 발생할 수 있습니다.");
                str.AppendLine("전기/가스를 끄고 문을 열어두어 몸을 보호할 수 있는 곳에 숨어있다가");
                str.AppendLine("진동이 잦아들면 머리를 보호하며 바깥의 넓은 곳으로 대피하시기 바랍니다.");
                str.AppendLine("크게 규모 3~4의 여진이 뒤따를 수 있으니 주의하시기 바랍니다.");
                str.AppendLine("더 큰 지진의 전진일 수 있으므로 안전한 곳으로 대피하시기를 권장합니다.");
            }
            else if (richterMScale < 7.0)
            {
                // 큰 피해가 발생하며 원자력 발전소를 걱정할 정도.

                str.AppendLine("넓은 범위에서 강한 진동과 함께 순간적으로 큰 피해가 발생할 수 있습니다.");
                str.AppendLine("전기/가스를 끄고 문을 열어두어 몸을 보호할 수 있는 곳에 숨어있다가");
                str.AppendLine("진동이 잦아들면 머리를 보호하며 바깥의 넓은 곳으로 대피하시기 바랍니다.");
                str.AppendLine("산사태가 발생할 수 있으니 주의하시길 바랍니다.");
                str.AppendLine("크게 규모 4~5의 여진이 뒤따를 수 있으니 주의하시기 바랍니다.");
            }
            else
            {
                // 매우 큰 지진.

                str.AppendLine("매우 넓은 범위에서 강한 진동과 함께 괴멸적인 피해가 발생할 수 있습니다.");
                str.AppendLine("전기/가스를 끄고 문을 열어두어 몸을 보호할 수 있는 곳에 숨어있다가");
                str.AppendLine("진동이 잦아들면 머리를 보호하며 바깥의 넓은 곳으로 대피하시기 바랍니다.");
                str.AppendLine("산사태가 발생할 수 있으니 주의하시길 바랍니다.");
                str.AppendLine("큰 여진이 뒤따를 수 있으니 주의하시기 바랍니다.");
                str.AppendLine("해저 지진의 경우 해일이 발생할 수 있으므로 즉시 높은 곳으로 이동하시기 바랍니다.");
            }


            return str.ToString().TrimEnd();
        }
    }
}
