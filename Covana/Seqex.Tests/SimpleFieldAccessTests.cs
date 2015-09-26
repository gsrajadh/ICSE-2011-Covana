using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Seqex.Tests
{
    public class ArgumentClass
    {
        int ArgField;
        
        public void SetArgField(int i)
        {
            ArgField = i;
        }

        public int GetArgField()
        {
            return ArgField;
        }

    }

    public class SimpleField
    {
        int Size;
      
        public void ModifyArg(ArgumentClass c)
        {
            c.SetArgField(10);
        }

        public int ReadArg(ArgumentClass c)
        {
            return c.GetArgField();
        }
    }

    [TestClass, PexClass]
    public partial class SimpleFieldAccessTests
    {
        [PexMethod]
        public void TestInfoFlow(int[] A, int[] count, int[] old_count)
        {
            int N = 20;
            for (int i = 0; i < N; i++)
            {
                old_count[i] = count[i];
            }
            for (int i = 0; i < N; i++) 
            {
                if (A[i] != 0)
                    count[i]++;
            }
            for (int i = 0; i < N; i++)
            {
                if (A[i] != 0)
                    PexAssert.IsTrue(count[i] == old_count[i]+1);
            }            
        }
    }
}
