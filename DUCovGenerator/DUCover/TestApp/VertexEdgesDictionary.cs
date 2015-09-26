using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Seqex.Tests;

namespace PaperExamples.GraphTest
{
    /// <summary>
    /// A dictionary with keys of type Vertex and values of type EdgeCollection
    /// </summary>
    public class VertexEdgesDictionary : System.Collections.DictionaryBase
    {
        /// <summary>
        /// Initializes a new empty instance of the VertexEdgesDictionary class
        /// </summary>
        public VertexEdgesDictionary()
        {
            // empty
        }

        /// <summary>
        /// Gets or sets the EdgeCollection associated with the given Vertex
        /// </summary>
        /// <param name="key">
        /// The Vertex whose value to get or set.
        /// </param>
        public virtual EdgeCollection this[Vertex key]
        {
            get
            {
                return (EdgeCollection)this.Dictionary[key];
            }
            set
            {
                this.Dictionary[key] = value;
            }
        }

        /// <summary>
        /// Adds an element with the specified key and value to this VertexEdgesDictionary.
        /// </summary>
        /// <param name="key">
        /// The Vertex key of the element to add.
        /// </param>
        /// <param name="value">
        /// The EdgeCollection value of the element to add.
        /// </param>
        public virtual void Add(Vertex key, EdgeCollection value)
        {
            this.Dictionary.Add(key, value);
        }

        /// <summary>
        /// Determines whether this VertexEdgesDictionary contains a specific key.
        /// </summary>
        /// <param name="key">
        /// The Vertex key to locate in this VertexEdgesDictionary.
        /// </param>
        /// <returns>
        /// true if this VertexEdgesDictionary contains an element with the specified key;
        /// otherwise, false.
        /// </returns>
        public virtual bool Contains(Vertex key)
        {
            return this.Dictionary.Contains(key);
        }

        /// <summary>
        /// Determines whether this VertexEdgesDictionary contains a specific key.
        /// </summary>
        /// <param name="key">
        /// The Vertex key to locate in this VertexEdgesDictionary.
        /// </param>
        /// <returns>
        /// true if this VertexEdgesDictionary contains an element with the specified key;
        /// otherwise, false.
        /// </returns>
        public virtual bool ContainsKey(Vertex key)
        {
            return this.Dictionary.Contains(key);
        }

        /// <summary>
        /// Determines whether this VertexEdgesDictionary contains a specific value.
        /// </summary>
        /// <param name="value">
        /// The EdgeCollection value to locate in this VertexEdgesDictionary.
        /// </param>
        /// <returns>
        /// true if this VertexEdgesDictionary contains an element with the specified value;
        /// otherwise, false.
        /// </returns>
        public virtual bool ContainsValue(EdgeCollection value)
        {
            foreach (EdgeCollection item in this.Dictionary.Values)
            {
                if (item == value)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the element with the specified key from this VertexEdgesDictionary.
        /// </summary>
        /// <param name="key">
        /// The Vertex key of the element to remove.
        /// </param>
        public virtual void Remove(Vertex key)
        {
            this.Dictionary.Remove(key);
        }

        /// <summary>
        /// Gets a collection containing the keys in this VertexEdgesDictionary.
        /// </summary>
        public virtual System.Collections.ICollection Keys
        {
            get
            {
                return this.Dictionary.Keys;
            }
        }

        /// <summary>
        /// Gets a collection containing the values in this VertexEdgesDictionary.
        /// </summary>
        public virtual System.Collections.ICollection Values
        {
            get
            {
                return this.Dictionary.Values;
            }
        }
    }
}
