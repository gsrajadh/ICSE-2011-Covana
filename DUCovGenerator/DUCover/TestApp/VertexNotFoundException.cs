using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PaperExamples.GraphTest
{
    class VertexNotFoundException : Exception
    {
        /// <summary>
        /// Build a new exception
        /// </summary>
        /// <param name="name">vertex name</param>
        public VertexNotFoundException(String name)
            : base(name)
        { }
    }
}
