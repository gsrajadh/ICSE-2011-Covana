// <copyright file="FixedSizeStackFactory.cs">Copyright ©  2009</copyright>

using System;
using Microsoft.Pex.Framework;
using Benchmarks;

namespace Benchmarks
{
    /// <summary>A factory for Benchmarks.FixedSizeStack instances</summary>
    public static class StackFactory
    {
        /// <summary>A factory for Benchmarks.FixedSizeStack instances</summary>
        [PexFactoryMethod(typeof(Stack))]
        public static Stack Create(object[] objs)
        {
            PexAssume.IsTrue(objs.Length < 15);
            Stack stack = new Stack();
            for (int i = 0; i < objs.Length;i++ )
            {
                stack.Push(objs[i]);
            }
            return stack;
        }
    }
}
