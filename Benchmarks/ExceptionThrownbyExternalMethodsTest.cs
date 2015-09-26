using System;
using System.IO;
using Microsoft.Pex.Framework;
using Covana.ProblemExtractor;
using Covana.ResultTrackingExtrator;

namespace Benchmarks
{
    public partial class CombinePathUtility
    {
        public static void CombinePath(string path)
        {
            if (System.IO.Path.Combine(path, "quick.png") == "ok")
            {
                Console.WriteLine("combine succeessfully");
            }
            else
            {
                Console.WriteLine("failure");
            }

            Console.WriteLine("after combining " + path + " with quick.png");
        }
    }

    [PexClass(typeof(CombinePathUtility))]
    public partial class ExceptionThrownbyExternalMethodsTest
    {
        [PexMethod]
        public void TestPrintPath(string filename)
        {
            CombinePathUtility.CombinePath(filename);
        }

    }
}