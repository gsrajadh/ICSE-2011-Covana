using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Pex.Framework;
using FieldAccessExtractor;
using Seqex;
using SeqExplorable;

namespace Seqex.Tests
{
    [PexClass(typeof(Stack))]
    [InsufficientObjectFactoryObserver]
    [FieldAccessObserver]
    public partial class StackFieldAccessTests
    {
        
        [PexMethod]
        public void TestIsNearFull([PexAssumeUnderTest]Stack target)
        {
            target.IsNearFull();
        }

        [PexMethod]
        public void TestSetNotNull([PexAssumeUnderTest]Stack target)
        {
            target.SetNotNull();
        }
        
        [PexMethod]
        public void TestSetNull([PexAssumeUnderTest]Stack target)
        {
            target.SetNull();
        }

        [PexMethod]
        public void TestPop([PexAssumeUnderTest]Stack target)
        {
            target.Pop();
        }

//        [PexMethod]
//        public void TestSetSize([PexAssumeUnderTest]Stack target, int i)
//        {
//            target.SetSize(i);
//        }

        [PexMethod]
        public void TestPush([PexAssumeUnderTest]Stack target)
        {
            target.Push();
        }        

    }
}
