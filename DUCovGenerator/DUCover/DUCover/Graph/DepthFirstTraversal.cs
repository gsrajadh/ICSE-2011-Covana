using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph.Concepts.Traversals;
using QuickGraph.Concepts;
using QuickGraph;

namespace DUCover.Graph
{
    /// <summary>
    /// Performs a depth first traversal
    /// </summary>
    public class DepthFirstTraversal
    {
        private InstructionGraph m_VisitedGraph;
		private Dictionary<IVertex, GraphColor> m_Colors;

		/// <summary>
		/// A depth first search algorithm on a directed graph
		/// </summary>
		/// <param name="g">The graph to traverse</param>
		/// <exception cref="ArgumentNullException">g is null</exception>
		public DepthFirstTraversal(InstructionGraph g)
		{
			if (g == null)
				throw new ArgumentNullException("g");
			m_VisitedGraph = g;
			m_Colors = new Dictionary<IVertex, GraphColor>();
		}

        public void Traverse(InstructionVertex v)
        {
            Stack<InstructionVertex> verticesStack = new Stack<InstructionVertex>();
            verticesStack.Push(v);

            HashSet<InstructionVertex> visitiedVertices = new HashSet<InstructionVertex>();
            while (verticesStack.Count > 0)
            {
                InstructionVertex iv = verticesStack.Pop();
                visitiedVertices.Add(iv);

                foreach (var outelem in this.m_VisitedGraph.OutEdges(iv))
                {
                    Edge outEdge = outelem as Edge;
                    var target = outEdge.Target as InstructionVertex;
                    if (!visitiedVertices.Contains(target))
                    {
                        verticesStack.Push(target);
                    }                    
                }
            }            
        }

        /// <summary>
        /// Checks whether the current vertex has a feasible path without encountering
        /// the other offsets
        /// </summary>
        /// <param name="v"></param>
        /// <param name="otherDefOffsets"></param>
        public bool HasDefClearPathToEnd(InstructionVertex v, HashSet<int> otherDefOffsets)
        {
            Stack<InstructionVertex> verticesStack = new Stack<InstructionVertex>();
            verticesStack.Push(v);

            HashSet<InstructionVertex> visitiedVertices = new HashSet<InstructionVertex>();
            while (verticesStack.Count > 0)
            {
                InstructionVertex iv = verticesStack.Pop();
                visitiedVertices.Add(iv);

                //Reached an end node, showing a feasible path
                if (this.m_VisitedGraph.NumOutEdges(iv) == 0)
                    return true;

                foreach (var outelem in this.m_VisitedGraph.OutEdges(iv))
                {
                    Edge outEdge = outelem as Edge;
                    var target = outEdge.Target as InstructionVertex;
                    //Visit a vertex further only if it is not in the redefined set
                    if (!visitiedVertices.Contains(target) && !otherDefOffsets.Contains(target.Instruction.Offset))
                    {
                        verticesStack.Push(target);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether there is a definition clear path from the beginning of the 
        /// method to the current usage node. here v represents a usage node
        /// </summary>
        /// <param name="v"></param>
        /// <param name="otherDefOffsets"></param>
        public bool HasDefClearPathFromBeginning(InstructionVertex v, HashSet<int> otherDefOffsets)
        {
            Stack<InstructionVertex> verticesStack = new Stack<InstructionVertex>();
            verticesStack.Push(v);

            HashSet<InstructionVertex> visitiedVertices = new HashSet<InstructionVertex>();
            while (verticesStack.Count > 0)
            {
                InstructionVertex iv = verticesStack.Pop();
                visitiedVertices.Add(iv);

                //Reached an end node, showing a feasible path
                if (this.m_VisitedGraph.NumInEdges(iv) == 0)
                    return true;

                foreach (var inelem in this.m_VisitedGraph.InEdges(iv))
                {
                    Edge inEdge = inelem as Edge;
                    var source = inEdge.Source as InstructionVertex;
                    //Visit a vertex further only if it is not in the redefined set
                    if (!visitiedVertices.Contains(source) && !otherDefOffsets.Contains(source.Instruction.Offset))
                    {
                        verticesStack.Push(source);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether the current vertex has a feasible path without encountering
        /// the other offsets to the given other node
        /// </summary>
        /// <param name="v"></param>
        /// <param name="otherDefOffsets"></param>
        public bool HasDefClearPathBetweenNodes(InstructionVertex source, InstructionVertex target, HashSet<int> otherDefOffsets)
        {
            Stack<InstructionVertex> verticesStack = new Stack<InstructionVertex>();
            verticesStack.Push(source);

            HashSet<InstructionVertex> visitiedVertices = new HashSet<InstructionVertex>();
            while (verticesStack.Count > 0)
            {
                InstructionVertex iv = verticesStack.Pop();
                visitiedVertices.Add(iv);
                
                foreach (var outelem in this.m_VisitedGraph.OutEdges(iv))
                {
                    Edge outEdge = outelem as Edge;
                    var edgetarget = outEdge.Target as InstructionVertex;
                    if (target.Equals(edgetarget))
                        return true;

                    //Visit a vertex further only if it is not in the redefined set
                    if (!visitiedVertices.Contains(edgetarget) && !otherDefOffsets.Contains(edgetarget.Instruction.Offset))
                    {
                        verticesStack.Push(edgetarget);
                    }
                }
            }

            return false;
        }
    }
}
