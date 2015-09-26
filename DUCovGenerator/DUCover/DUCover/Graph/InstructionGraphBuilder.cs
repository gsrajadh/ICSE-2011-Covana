using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.ExtendedReflection.Metadata;

namespace DUCover.Graph
{
    /// <summary>
    /// Builds a set of instruction graphs and dumps them to an XML file
    /// </summary>
    public class InstructionGraphBuilder
    {
        /// <summary>
        /// Generates a set of instruction graphs
        /// </summary>
        public static void GenerateInstructionGraphXML(string filename, List<Method> methodList)
        {
            using (StreamWriter sw = new StreamWriter(filename))
            {
                sw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sw.WriteLine("<cfglist>");
                foreach (var method in methodList)
                {
                    InstructionGraph ig = new InstructionGraph(method);
                    ig.DumpToXMLFile(sw);
                }
                sw.WriteLine("</cfglist>");
            }
        }
    }
}
