using System;
using System.IO;
using System.Text;
using ExternalLib;
using FieldAccessExtractor;
using Microsoft.Pex.Framework;
using Covana.CoverageExtractor;
using Covana.ResultTrackingExtrator;


namespace Benchmarks
{
    [PexClass(typeof(ExternalMethodsReturnValueTest))]
    public partial class ExternalMethodsReturnValueTest
    {
        [PexMethod]
        public void ExternalMethodReturnValueTest(int y)
        {

            int value = ExternalObj.Compute(y);

            Console.Out.WriteLine("after computation, value is: " + value);

            if (value > 5)
            {
                Console.WriteLine("value > 5!");
            }
        }
    }
}