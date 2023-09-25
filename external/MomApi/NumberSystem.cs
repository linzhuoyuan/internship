using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantmom.Api
{
    public static class NumberSystem
    {
        public static string IntToi32(this long xx)
        {
            var a = "";
            while (xx >= 1)
            {
                int index = Convert.ToInt16(xx - (xx / 32) * 32);
                a = Base64Code[index] + a;
                xx /= 32;
            }
            return a;
        }

        public static long i32ToInt(this string xx)
        {
            long a = 0;
            var power = xx.Length - 1;

            for (var i = 0; i <= power; i++)
            {
                a += _Base64Code[xx[power - i]] * Convert.ToInt64(Math.Pow(32, i));
            }

            return a;
        }


        public static string IntToi64(this long xx)
        {
            var a = "";
            while (xx >= 1)
            {
                int index = Convert.ToInt16(xx - (xx / 64) * 64);
                a = Base64Code[index] + a;
                xx /= 64;
            }
            return a;
        }

        public static long i64ToInt(this string xx)
        {
            long a = 0;
            var power = xx.Length - 1;

            for (var i = 0; i <= power; i++)
            {
                var c = xx[power - i];
                if (!ValidCode.Contains(c)) 
                {
                    break;
                }
                a += _Base64Code[c] * Convert.ToInt64(Math.Pow(64, i));
            }

            return a;
        }

        private static readonly Dictionary<int, char> Base64Code = new() {
            {00 ,'0'}, {01 ,'1'}, {02 ,'2'}, {03 ,'3'}, {04 ,'4'}, {05 ,'5'}, {06 ,'6'}, {07 ,'7'}, {08 ,'8'}, {09 ,'9'},
            {10 ,'A'}, {11 ,'B'}, {12 ,'C'}, {13 ,'D'}, {14 ,'E'}, {15 ,'F'}, {16 ,'G'}, {17 ,'H'}, {18 ,'I'}, {19 ,'J'},
            {20 ,'K'}, {21 ,'L'}, {22 ,'M'}, {23 ,'N'}, {24 ,'O'}, {25 ,'P'}, {26 ,'Q'}, {27 ,'R'}, {28 ,'S'}, {29 ,'T'},
            {30 ,'U'}, {31 ,'V'}, {32 ,'W'}, {33 ,'X'}, {34 ,'Y'}, {35 ,'Z'}, {36 ,'a'}, {37 ,'b'}, {38 ,'c'}, {39 ,'d'},
            {40 ,'e'}, {41 ,'f'}, {42 ,'g'}, {43 ,'h'}, {44 ,'i'}, {45 ,'j'}, {46 ,'k'}, {47 ,'l'}, {48 ,'m'}, {49 ,'n'},
            {50 ,'o'}, {51 ,'p'}, {52 ,'q'}, {53 ,'r'}, {54 ,'s'}, {55 ,'t'}, {56 ,'u'}, {57 ,'v'}, {58 ,'w'}, {59 ,'x'},
            {60 ,'y'}, {61 ,'z'}, {62 ,'{'}, {63 ,'}'},
        };

        private static readonly HashSet<char> ValidCode = new() {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
            'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
            'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
            'y', 'z', '{', '}'
        };

        private static Dictionary<char, int> _Base64Code
        {
            get
            {
                return Enumerable.Range(0, Base64Code.Count).ToDictionary(i => Base64Code[i], i => i);
            }
        }
    }
}