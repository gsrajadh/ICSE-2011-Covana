using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FieldAccessExtractor;
using SeqExplorable;
using Seqex;


namespace Seqex.Tests
{
    [PexClass(typeof(Graph))]
    [InsufficientObjectFactoryObserver]
    [FieldAccessObserver]
    public partial class GraphTest
    {
        [PexMethod]       
        public void TestEdgeSetStart([PexAssumeUnderTest]Edge target, Vertex start)
        {
            target.SetStart(start);
        }

        [PexMethod]       
        public void TestEdgeSetEnd([PexAssumeUnderTest]Edge target, Vertex start)
        {
            target.SetEnd(start);
        }

        [PexMethod]       
        public void TestEdgeClear([PexAssumeUnderTest]Edge target)
        {
            target.Clear();
        }

        [PexMethod]      
        public void TestGraphClear([PexAssumeUnderTest]Graph target)
        {
            target.Clear();
        }

        [PexMethod] 
        public void TestGraphAddVertex([PexAssumeUnderTest]Graph target, Vertex vertex)
        {
            target.AddVertex(vertex);
        }

        [PexMethod]
        public void TestGraphAddEdge([PexAssumeUnderTest]Graph target, Edge edge)
        {
            target.AddEdge(edge);
        }

        [PexMethod] 
        public void TestGraphContains([PexAssumeUnderTest]Graph target, Vertex vertex)
        {
            target.Contains(vertex);
        }
    }
}
