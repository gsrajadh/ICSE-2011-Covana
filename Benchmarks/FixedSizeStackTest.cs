using System;
using System.Collections.Generic;
using Microsoft.Pex.Framework;
using Covana.ProblemExtractor;

namespace Benchmarks
{
    public class Stack
    {
        private List<object> items;

        public Stack()
        {
            items = new List<object>();
        }


        public int Count
        {
            get { return items.Count; }
        }

        public void Push(object item)
        {
            items.Add(item);
        }

        public object Pop()
        {
            if (items.Count > 0)
            {
                object result = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                return result;
            }
            throw new Exception("empty");
        }
    }

    public class FixedSizeStack
    {
        private Stack stack;

        public FixedSizeStack(Stack stack)
        {
            this.stack = stack;
        }

        public void Push(object item)
        {
            if (stack.Count == 10)
            {
                throw new Exception("full");
            }

            stack.Push(item);
        }
    }

    [PexClass(typeof(FixedSizeStack))]
    public partial class FixedSizeStackTest
    {
        [PexMethod]
        public void TestPush(FixedSizeStack stack, object item)
        {
            stack.Push(item);
        }
    }
}