﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chess_pos_db_gui
{
    class Score : IComparable
    {
        private static readonly int knownResultThreashold = 18000;
        private static readonly int cursedDtz0 = 20000;
        private static readonly int dtz0 = 30000;

        public int Value { get; set; }
        public double WinPct { get; set; }

        private static int ValueFromString(string str)
        {
            if (str == null)
            {
                return 0;
            }

            try
            {
                var parts = str.Split(' ');
                if (parts.Length == 2)
                {
                    return int.Parse(parts[1].Trim("()".ToCharArray()));
                }
                else
                {
                    return int.Parse(parts[0]);
                }
            }
            catch
            {
                return 0;
            }
        }

        private static double WinPctFromString(string str)
        {
            if (str == null)
            {
                return Double.NaN;
            }

            try
            {
                return double.Parse(str, CultureInfo.InvariantCulture) / 100.0;
            }
            catch
            {
            }

            return Double.NaN;
        }

        private static double WinPctFromEval(int eval)
        {
            if (Math.Abs(eval) >= knownResultThreashold)
            {
                return eval < 0 ? 0.0 : 1.0;
            }

            return 1.0 / (1.0 + Math.Exp((double)-eval / 100));
        }

        public Score(int v)
        {
            Value = v;
            WinPct = 0;
        }

        public Score(string str)
        {
            Value = ValueFromString(str);
            WinPct = WinPctFromEval(Value);
        }
        public Score(int v, double pct)
        {
            Value = v;
            WinPct = pct;
        }

        public Score(string value, string winpct)
        {
            Value = ValueFromString(value);
            WinPct = WinPctFromString(winpct);
        }

        public override string ToString()
        {
            int abs = Math.Abs(Value);
            string sign = Value < 0 ? "-" : "";
            if (abs > cursedDtz0)
            {
                return "DTZ " + sign + (dtz0 - abs).ToString();
            }
            else if (abs > knownResultThreashold)
            {
                // cursed win/loss
                return "DTZ " + sign + (cursedDtz0 - abs).ToString();
            }

            return Value.ToString();
        }

        public int CompareTo(object b)
        {
            if (b == null) return 1;

            if (!(b is Score bb)) throw new ArgumentException("rhs is not a Score");

            return Value.CompareTo(bb.Value);
        }
    }
}
