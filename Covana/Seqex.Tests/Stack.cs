using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Seqex.Tests
{
    public class Stack
    {
        int size;
        Object o;
        
        public void Push()
        {
            if (size > 10)
                throw new Exception();
            size++;
        }

        public void Pop()
        {
            size--;
        }

        public void SetNull()
        {
            o = null;
        }

        public void SetNotNull()
        {
            o = new Object();
        }

//        public void SetSize(int i)
//        {
//            size = i;
//        }

        public bool IsNearFull()
        {
            if (o != null)
            {
                if (size == 9)
                    return true;
            }
            return false;

        }

    }
}
