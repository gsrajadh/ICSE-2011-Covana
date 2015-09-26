using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Seqex.Tests
{
    public class Vertex
    {
        public Vertex()
        {
        }
    }

    public class Edge
    {
        private Vertex _start, _end;
        public Edge() { }
        public Vertex Start { get { return this._start; } }
        public Vertex End { get { return this._end; } }
        public void SetStart(Vertex start)
        {
            if (start == null) throw new ArgumentException();
            this._start = start;
        }
        public void SetEnd(Vertex end)
        {
            if (end == null) throw new ArgumentException();
            this._end = end;
        }
        public void Clear()
        {
            this._start = null;
            this._end = null;
        }
    }

    public class Graph
    {
        Vertex[] vertices;
        Edge[] edges;

        //List<Vertex> verticesList;
        //List<Edge> edgesList;

        public Graph()
        {
            this.vertices = new Vertex[4];
            this.edges = new Edge[4];
            //this.verticesList = new List<Vertex>();
            //this.edgesList = new List<Edge>();
        }

        public void Clear()
        {
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = null;
            for (int i = 0; i < edges.Length; i++)
                edges[i] = null;
            //this.verticesList.Clear();
            //this.edgesList.Clear();
        }

        public void AddVertex(Vertex vertex)
        {
            if (vertex == null) throw new ArgumentException();
            //this.verticesList.Add(vertex);
            for (int i = 0; i < vertices.Length; i++)
                if (vertices[i] == null)
                {
                    vertices[i] = vertex;
                    return;
                }
            throw new ArgumentException("full");
        }

        public void AddEdge(Edge edge)
        {
            if (edge == null) throw new ArgumentException();
            var start = edge.Start;
            var end = edge.End;
            if (start == null) throw new ArgumentException();
            if (end == null) throw new ArgumentException();
            if (!Contains(start)) throw new ArgumentException();
            if (!Contains(end)) throw new ArgumentException();
            //this.edgesList.Add(edge);
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i] == null)
                {
                    edges[i] = edge;
                    return;
                }
            }
            throw new ArgumentException("full");
        }

        public bool Contains(Vertex vertex)
        {
            if (vertex == null) throw new ArgumentException();
            //if (verticesList.Contains(vertex)) return true;
            for (int i = 0; i < vertices.Length; i++)
                if (vertices[i] == vertex)
                    return true;
            return false;
        }
    }
}
