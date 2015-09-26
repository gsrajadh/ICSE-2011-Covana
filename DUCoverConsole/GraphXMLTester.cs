using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.ExtendedReflection.Metadata;
using DUCover.Graph;

namespace DUCoverConsole
{
    /// <summary>
    /// Graph XML Tester
    /// </summary>
    public class GraphXMLTester
    {
        public static void TestInstructionGraphXML(string assemblyname, string filename)
        {
            //Load the assembly
            AssemblyEx assembly;
            ReflectionHelper.TryLoadAssemblyEx(assemblyname, out assembly);

            //Loading the list of methods
            List<Method> methodList = new List<Method>();
            foreach (var tdef in assembly.TypeDefinitions)
            {
                foreach (var mdef in tdef.DeclaredInstanceMethods)
                {
                    try
                    {
                        var method = mdef.Instantiate(new TypeEx[0] { }, new TypeEx[0] { });
                        if (method != null)
                        {
                            methodList.Add(method);
                        }
                    }
                    catch (Exception)
                    {
                        
                    }
                   
                }
            }

            InstructionGraphBuilder.GenerateInstructionGraphXML(filename, methodList);
        }
    }
}
