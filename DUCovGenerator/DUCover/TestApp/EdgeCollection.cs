using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Seqex.Tests;

namespace PaperExamples.GraphTest
{
    /// <summary>
    /// A collection of elements of type Edge
    /// </summary>
    public class EdgeCollection : System.Collections.CollectionBase
    {
        /// <summary>
        /// Initializes a new empty instance of the EdgeCollection class.
        /// </summary>
        public EdgeCollection()
        {
            // empty
        }

        /// <summary>
        /// Initializes a new instance of the EdgeCollection class, containing elements
        /// copied from an array.
        /// </summary>
        /// <param name="items">
        /// The array whose elements are to be added to the new EdgeCollection.
        /// </param>
        public EdgeCollection(Edge[] items)
        {
            this.AddRange(items);
        }

        /// <summary>
        /// Initializes a new instance of the EdgeCollection class, containing elements
        /// copied from another instance of EdgeCollection
        /// </summary>
        /// <param name="items">
        /// The EdgeCollection whose elements are to be added to the new EdgeCollection.
        /// </param>
        public EdgeCollection(EdgeCollection items)
        {
            this.AddRange(items);
        }

        /// <summary>
        /// Adds the elements of an array to the end of this EdgeCollection.
        /// </summary>
        /// <param name="items">
        /// The array whose elements are to be added to the end of this EdgeCollection.
        /// </param>
        public virtual void AddRange(Edge[] items)
        {
            foreach (Edge item in items)
            {
                this.List.Add(item);
            }
        }

        /// <summary>
        /// Adds the elements of another EdgeCollection to the end of this EdgeCollection.
        /// </summary>
        /// <param name="items">
        /// The EdgeCollection whose elements are to be added to the end of this EdgeCollection.
        /// </param>
        public virtual void AddRange(EdgeCollection items)
        {
            foreach (Edge item in items)
            {
                this.List.Add(item);
            }
        }

        /// <summary>
        /// Adds an instance of type Edge to the end of this EdgeCollection.
        /// </summary>
        /// <param name="value">
        /// The Edge to be added to the end of this EdgeCollection.
        /// </param>
        public virtual void Add(Edge value)
        {
            this.List.Add(value);
        }

        /// <summary>
        /// Determines whether a specfic Edge value is in this EdgeCollection.
        /// </summary>
        /// <param name="value">
        /// The Edge value to locate in this EdgeCollection.
        /// </param>
        /// <returns>
        /// true if value is found in this EdgeCollection;
        /// false otherwise.
        /// </returns>
        public virtual bool Contains(Edge value)
        {
            return this.List.Contains(value);
        }

        /// <summary>
        /// Return the zero-based index of the first occurrence of a specific value
        /// in this EdgeCollection
        /// </summary>
        /// <param name="value">
        /// The Edge value to locate in the EdgeCollection.
        /// </param>
        /// <returns>
        /// The zero-based index of the first occurrence of the _ELEMENT value if found;
        /// -1 otherwise.
        /// </returns>
        public virtual int IndexOf(Edge value)
        {
            return this.List.IndexOf(value);
        }

        /// <summary>
        /// Inserts an element into the EdgeCollection at the specified index
        /// </summary>
        /// <param name="index">
        /// The index at which the Edge is to be inserted.
        /// </param>
        /// <param name="value">
        /// The Edge to insert.
        /// </param>
        public virtual void Insert(int index, Edge value)
        {
            this.List.Insert(index, value);
        }

        /// <summary>
        /// Gets or sets the Edge at the given index in this EdgeCollection.
        /// </summary>
        public virtual Edge this[int index]
        {
            get
            {
                return (Edge)this.List[index];
            }
            set
            {
                this.List[index] = value;
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific Edge from this EdgeCollection.
        /// </summary>
        /// <param name="value">
        /// The Edge value to remove from this EdgeCollection.
        /// </param>
        public virtual void Remove(Edge value)
        {
            this.List.Remove(value);
        }

        /// <summary>
        /// Type-specific enumeration class, used by EdgeCollection.GetEnumerator.
        /// </summary>
        public class Enumerator : System.Collections.IEnumerator
        {
            private System.Collections.IEnumerator wrapped;

            /// <summary>
            /// Create a new enumerator on the collection
            /// </summary>
            /// <param name="collection">collection to enumerate</param>
            public Enumerator(EdgeCollection collection)
            {
                this.wrapped = ((System.Collections.CollectionBase)collection).GetEnumerator();
            }

            /// <summary>
            /// The current element. 
            /// </summary>
            public Edge Current
            {
                get
                {
                    return (Edge)(this.wrapped.Current);
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    return (Object)(this.wrapped.Current);
                }
            }

            /// <summary>
            /// Moves cursor to next element.
            /// </summary>
            /// <returns>true if current is valid, false otherwize</returns>
            public bool MoveNext()
            {
                return this.wrapped.MoveNext();
            }

            /// <summary>
            /// Resets the cursor to the position before the first element.
            /// </summary>
            public void Reset()
            {
                this.wrapped.Reset();
            }
        }

        /// <summary>
        /// Returns an enumerator that can iterate through the elements of this EdgeCollection.
        /// </summary>
        /// <returns>
        /// An object that implements System.Collections.IEnumerator.
        /// </returns>        
        public new virtual EdgeCollection.Enumerator GetEnumerator()
        {
            return new EdgeCollection.Enumerator(this);
        }
    }
}
