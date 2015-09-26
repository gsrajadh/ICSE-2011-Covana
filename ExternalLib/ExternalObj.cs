using System;
using Microsoft.Pex.Framework;

namespace ExternalLib
{
    public class ExternalObj
    {
        public static int Compute(int y)
        {
            if (y < 0)
            {
                return y * -3;
            }
            return y * 3;
        }

        public static void PrintValue(int value)
        {
            Console.WriteLine("value is: " + value);
        }

        public static string ReadStatus(string s)
        {
            return s.Substring(s.IndexOf(":") + 2);

        }


    }


}