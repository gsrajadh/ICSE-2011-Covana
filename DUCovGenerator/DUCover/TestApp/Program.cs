using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PaperExamples.GraphTest;
using Seqex.Tests;
using MicroBenchmarks.GraphTest;
//using NUnitTests;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            MyClass mc = new MyClass();
            mc.NullCheckMethod();
            mc.GetMySClass();
            mc.DefineIJK();
            mc.GetMySClass();
            mc.NullCheckMethod();
            mc.AccessI();
            mc.AccessJ();
            mc.AccessIK();
            mc.AccessOtherClass();                       

            /*MySubClass msc = new MySubClass();
            msc.MySubI = 30;
            Console.WriteLine(msc.MySubI);*/
            try
            {
                AdjacencyGraphOrig ag1 = new AdjacencyGraphOrig();
                ag1.AddEdge(null, null);
            }
            catch (Exception ex)
            {

            }

            AdjacencyGraphOrig ag = new AdjacencyGraphOrig();
            Vertex v1 = new Vertex();
            ag.AddVertex(v1);
            Vertex v2 = new Vertex();
            ag.AddVertex(v2);
            Edge edge = ag.AddEdge(v1, v2);
            TopologicalSort ts = new TopologicalSort(ag);
            ts.Compute();

            //new SampleTests().test1();
            //new DUCoverProgram().DUCoverTerminate();
        }
    }
}
