using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph.Concepts.Traversals;
using QuickGraph.Representations;
using Microsoft.ExtendedReflection.Metadata;
using System.Reflection.Emit;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using QuickGraph;
using PexMe.Core;
using QuickGraph.Concepts;
using System.IO;

namespace DUCover.Graph
{
    public class InstructionGraph        
    {
        Method method;
        private InstructionVertexAndEdgeProvider vertexAndEdgeProvider;
        Dictionary<int, InstructionVertex> vertices = new Dictionary<int, InstructionVertex>();
        private InstructionVertex rootVertex;

        private Dictionary<IVertex, List<Edge>> vertexOutEdges = new Dictionary<IVertex, List<Edge>>();
        private Dictionary<IVertex, List<Edge>> vertexInEdges = new Dictionary<IVertex, List<Edge>>();

        /// <summary>
        /// builds an instruction graph
        /// </summary>
        /// <param name="method"></param>
        public InstructionGraph(Method method)            
        {
            if (method == null)
                throw new ArgumentNullException("method");

            this.method = method;
            this.vertexAndEdgeProvider = new InstructionVertexAndEdgeProvider();
            this.BuildGraphFromMethod(method);
        }

        private void BuildGraphFromMethod(Method method)
        {
            this.PopulateVertices(method);
            this.PopulateEdges(method);
        }

        public Edge AddEdge(
            InstructionVertex source,
            InstructionVertex target
            )
        {
            // look for the vertex in the list
            if (!this.vertexOutEdges.ContainsKey(source))
                throw new Exception("Could not find source vertex");
            if (!this.vertexOutEdges.ContainsKey(target))
                throw new Exception("Could not find target vertex");
                        
            // create edge
            Edge e = new Edge(source, target);
            this.vertexOutEdges[source].Add(e);
            this.vertexInEdges[target].Add(e);
            return e;
        }

        /// <summary>
        /// Add a new InstructionVertex to the graph and returns it.
        /// </summary>
        /// <returns>
        /// Created vertex
        /// </returns>
        public InstructionVertex AddVertex(Instruction instruction)
        {
            InstructionVertex v = new InstructionVertex(instruction);

            if (this.vertices.Count == 0)
                this.rootVertex = v;

            List<Edge> outEdges = new List<Edge>();
            this.vertexOutEdges.Add(v, outEdges);

            List<Edge> inEdges = new List<Edge>();
            this.vertexInEdges.Add(v, inEdges);

            return v;
        }

        /// <summary>
        /// Adds edges to the graph
        /// </summary>
        /// <param name="method"></param>
        private void PopulateEdges(Method method)
        {
            MethodBodyEx body;
            if (!method.TryGetBody(out body) || !body.HasInstructions)
            {
                return;
            }

            int offset = 0;
            Instruction instruction;
            InstructionVertex cv = null;            

            //make the graph
            while (body.TryGetInstruction(offset, out instruction))
            {
                SafeDebug.AssumeNotNull(instruction, "instruction");
                OpCode opCode = instruction.OpCode;

                InstructionVertex iv = this.vertices[offset];
                if (cv != null)
                {
                    this.AddEdge(cv, iv);
                }

                if (MethodOrFieldAnalyzer.BranchOpCodes.Contains(opCode))
                {
                    InstructionVertex alternatev = this.vertices[instruction.BrTargetOffset];
                    this.AddEdge(iv, alternatev);
                    cv = iv;
                }
                else if (opCode == OpCodes.Switch)
                {
                    foreach (var switchoff in instruction.SwitchOffsets)
                    {
                        InstructionVertex alternatev = this.vertices[switchoff];
                        this.AddEdge(iv, alternatev);
                    }
                    cv = iv;
                }
                else if (opCode == OpCodes.Br || opCode == OpCodes.Br_S)
                {
                    InstructionVertex alternatev = this.vertices[instruction.BrTargetOffset];
                    this.AddEdge(iv, alternatev);
                    cv = null;
                }
                else if (opCode == OpCodes.Break)
                {
                    InstructionVertex alternatev = this.vertices[instruction.BrTargetOffset];
                    this.AddEdge(iv, alternatev);
                    cv = null;
                }
                else if (opCode == OpCodes.Ret || opCode == OpCodes.Throw)
                {
                    cv = null;
                }
                else
                {
                    cv = iv;
                }
                offset = instruction.NextOffset;
            }
        }

        /// <summary>
        /// Populates all vertices
        /// </summary>
        /// <param name="method"></param>
        private void PopulateVertices(Method method)
        {
            MethodBodyEx body;
            if (!method.TryGetBody(out body) || !body.HasInstructions)
            {
                return;
            }

            int offset = 0;
            Instruction instruction;
            while (body.TryGetInstruction(offset, out instruction))
            {
                SafeDebug.AssumeNotNull(instruction, "instruction");
                OpCode opCode = instruction.OpCode;

                InstructionVertex iv = this.AddVertex(instruction);                
                this.vertices[offset] = iv;
                offset = instruction.NextOffset;
            }
        }

        /// <summary>
        /// Dumps the graph to an XML file
        /// </summary>
        /// <param name="filename"></param>
        public void DumpToXMLFile(StreamWriter sw)
        {                            
            sw.WriteLine("\t<cfg methodName=\"" + MethodOrFieldAnalyzer.GetMethodSignature(this.method) + "\">");

            //dumping vertices
            sw.WriteLine("\t<vertices>");
            StringBuilder edgeSb = new StringBuilder("\t<edges>\r\n");

            if (this.rootVertex != null)
            {
                sw.WriteLine("\t\t<vertex id=\"" + this.rootVertex.Instruction.Offset + "\" kind=\"Entry\" offset=\"" + this.rootVertex.Instruction.Offset + "\"></vertex>");
                foreach (var vertex in this.vertices.Values)
                {
                    if (vertex != this.rootVertex)
                        sw.WriteLine("\t\t<vertex id=\"" + vertex.Instruction.Offset + "\" kind=\"Normal\" offset=\"" + vertex.Instruction.Offset + "\"></vertex>");

                    //add edges of this vertex to the edge string
                    foreach (var outedge in this.vertexOutEdges[vertex])
                    {
                        var sourceVertex = outedge.Source as InstructionVertex;
                        var targetVertex = outedge.Target as InstructionVertex;

                        edgeSb.Append("\t\t<edge sourceid=\"" + sourceVertex.Instruction.Offset + "\" targetid=\"" + targetVertex.Instruction.Offset + "\"></edge>\r\n");
                    }
                }
            }
   
            sw.WriteLine("\t</vertices>");
            
            edgeSb.Append("\t</edges>\n");

            //dumping edges
            sw.WriteLine(edgeSb.ToString());
            sw.WriteLine("\t</cfg>");
        }

        #region IAdjacencyGraph Members
        public System.Collections.IEnumerable AdjacentVertices(QuickGraph.Concepts.IVertex v)
        {
            foreach(Edge e in this.vertexOutEdges[v])
                yield return e;

            yield break;
        }

        public System.Collections.IEnumerable OutEdges(QuickGraph.Concepts.IVertex v)
        {
            foreach (Edge e in this.vertexOutEdges[v])
                yield return e;

            yield break;
        }

        public int NumOutEdges(QuickGraph.Concepts.IVertex v)
        {
            return this.vertexOutEdges[v].Count;
        }

        public System.Collections.IEnumerable InEdges(QuickGraph.Concepts.IVertex v)
        {
            foreach (Edge e in this.vertexInEdges[v])
                yield return e;

            yield break;
        }

        public int NumInEdges(QuickGraph.Concepts.IVertex v)
        {
            return this.vertexInEdges[v].Count;
        }

        public InstructionVertex GetVertex(int offset)
        {
            return this.vertices[offset];
        }
        #endregion
    }
}
