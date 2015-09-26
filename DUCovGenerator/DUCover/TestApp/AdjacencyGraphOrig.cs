using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Seqex.Tests;

namespace PaperExamples.GraphTest
{
    public class AdjacencyGraphOrig
    {
        private VertexEdgesDictionary m_VertexOutEdges;
        private EdgeCollection m_Edges;

        public AdjacencyGraphOrig()
        {
            m_VertexOutEdges = new VertexEdgesDictionary();
            m_Edges = new EdgeCollection();
        }

        /// <summary>
        /// Vertex Out edges dictionary
        /// </summary>
        protected VertexEdgesDictionary VertexOutEdges
        {
            get
            {
                return m_VertexOutEdges;
            }
        }

        /// <summary>copyDir
        /// Add a new vertex to the graph and returns it.
        /// 
        /// Complexity: 1 insertion.
        /// </summary>
        /// <returns>Create vertex</returns>
        public void AddVertex(Vertex v)
        {            
            VertexOutEdges.Add(v, new EdgeCollection());
            //return v;
        }

        public Edge AddEdge(
            Vertex source,
            Vertex target
            )
        {
            // look for the vertex in the list
            if (!VertexOutEdges.ContainsKey(source))
                throw new VertexNotFoundException("Could not find source vertex");
            if (!VertexOutEdges.ContainsKey(target))
                throw new VertexNotFoundException("Could not find target vertex");
                        

            // create edge
            Edge e = new Edge();
            e.SetStart(source);
            e.SetEnd(target);
            VertexOutEdges[source].Add(e);
            m_Edges.Add(e);

            return e;
        }

        public int GetNumEdges()
        {
            return this.m_Edges.Count;
        }
    }
}
