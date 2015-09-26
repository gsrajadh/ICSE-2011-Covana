using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.ComponentModel;
using PexMe.Core;
using Microsoft.ExtendedReflection.Logging;

namespace PexMe.ComponentModel.Hardcoded
{
    /// <summary>
    /// Stores the datastructure for predefined classes
    /// </summary>
    internal class PreDefinedGenericClassesStore
    {
        //This is a second level data structure to predefinedClasses, key on the name of the typedefinition
        SafeDictionary<string, SafeList<TypeEx>> definedTypes = new SafeDictionary<string, SafeList<TypeEx>>();

        /// <summary>
        /// Adds a type to predefined classes store
        /// </summary>
        /// <param name="typename"></param>
        /// <param name="typeEx"></param>
        public void AddToPredinedStore(string typename, TypeEx typeEx)
        {
            SafeList<TypeEx> existingDefinedTypes;
            if (!definedTypes.TryGetValue(typename, out existingDefinedTypes))
            {
                existingDefinedTypes = new SafeList<TypeEx>();
                definedTypes.Add(typename, existingDefinedTypes);
            }

            existingDefinedTypes.Add(typeEx);
        }

        /// <summary>
        /// Retrieves some typeEx for the generic typename
        /// </summary>
        /// <returns></returns>
        public bool TryGetSomeTypeEx(out TypeEx typeEx)
        {
            typeEx = null;
            foreach (var definedType in this.definedTypes.Values)
            {
                if (definedType.Count == 0)
                    continue;

                typeEx = definedType[0];
                return true;
            }

            return true;
        }

        /// <summary>
        /// Returns all defiend types for all generic names of that type
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TypeEx> GetAllDefinedTypes(TypeDefinition td)
        {
            SafeList<TypeEx> values;
            if (!this.definedTypes.TryGetValue(td.FullName, out values))
                yield break;

            foreach (var definedType in values)
            {
                yield return definedType;
            }
            yield break;
        }

        /// <summary>
        /// Retrieves the predefined one. uses recentAccessedTypes as a means, if there is more than one match
        /// </summary>
        /// <returns></returns>
        public bool TryGetTypeExForTypename(string typename, out TypeEx typeEx)
        {
            typeEx = null;
            SafeList<TypeEx> existingDefinedTypes;
            if (!definedTypes.TryGetValue(typename, out existingDefinedTypes))
            {
                return false;
            }

            if (existingDefinedTypes.Count == 0)
                return false;

            //There is only one type, just return the typeex in that scenario
            if (existingDefinedTypes.Count == 1)
            {
                typeEx = existingDefinedTypes[0];
                return true;
            }

            //If there is more than one, compute an intersection with the most recent accessed ones and return
            foreach(var existingType in existingDefinedTypes)
            {
                if(PreDefinedGenericClasses.recentAccessedTypes.Contains(existingType.FullName))
                {
                    typeEx = existingType;
                    return true;
                }
            }

            //Backup option, since no match is found
            typeEx = existingDefinedTypes[0];
            return true;
        }

        /// <summary>
        /// returns number of defined types
        /// </summary>
        /// <returns></returns>
        public int NumDefinedTypes(TypeDefinition td)
        {
            SafeList<TypeEx> values;
            if(this.definedTypes.TryGetValue(td.FullName, out values))
            {
                return values.Count;
            }

            return 0;
        }
    }

    /// <summary>
    /// Includes various pre-defined classes for generic types.
    /// </summary>
    public class PreDefinedGenericClasses
    {
        /// <summary>
        /// This dictionary is organized as a first level key on generic typename such as TNode, and second level key on the name of the typedefinition
        /// </summary>
        internal static SafeDictionary<string, PreDefinedGenericClassesStore> predefinedClasses = new SafeDictionary<string, PreDefinedGenericClassesStore>();

        /// <summary>
        /// Stores the most recently accessed types from predefined store based on the generic typename
        /// </summary>
        public static SafeSet<string> recentAccessedTypes = new SafeSet<string>();

        internal static bool bClassLoaded = false;

        /// <summary>
        /// Method is allowed to be invoked only once. Therefore the order of loading classes into the Dictionary is 
        /// quite important.
        /// </summary>
        /// <param name="host"></param>
        public static bool LoadPredefinedGenericClasses(IPexComponent host, string assemblyName)
        {          
            //QuickGraph: TEdge -> Edge<int>
            TypeEx edgeTypeEx;
            PreDefinedGenericClassesStore tedgePdgc = new PreDefinedGenericClassesStore();
            predefinedClasses.Add("TEdge", tedgePdgc);
            if (MethodOrFieldAnalyzer.TryGetTypeExFromName(host, assemblyName, "QuickGraph.Edge`1", out edgeTypeEx))
            {
                tedgePdgc.AddToPredinedStore("QuickGraph.AdjacencyGraph`2", edgeTypeEx);
                tedgePdgc.AddToPredinedStore("QuickGraph.IGraph`2", edgeTypeEx);
            }

            //Dsa: TNode for BinarySearchTree -> Dsa.DataStructures.BinaryTreeNode`1
            PreDefinedGenericClassesStore tnodePdgc = new PreDefinedGenericClassesStore();
            predefinedClasses.Add("TNode", tnodePdgc);
            TypeEx binNodeEx;
            if (MethodOrFieldAnalyzer.TryGetTypeExFromName(host, assemblyName, "Dsa.DataStructures.BinaryTreeNode`1", out binNodeEx))
            {
                tnodePdgc.AddToPredinedStore("Dsa.DataStructures.BinarySearchTree`1", binNodeEx);
                tnodePdgc.AddToPredinedStore("Dsa.DataStructures.CommonBinaryTree`2", binNodeEx);
                tnodePdgc.AddToPredinedStore("System.Collections.Generic.Queue`1", binNodeEx);
            }

            TypeEx avltreeNodeEx = null;
            if (MethodOrFieldAnalyzer.TryGetTypeExFromName(host, assemblyName, "Dsa.DataStructures.AvlTreeNode`1", out avltreeNodeEx))
            {
                tnodePdgc.AddToPredinedStore("Dsa.DataStructures.AvlTree`1", avltreeNodeEx);
                tnodePdgc.AddToPredinedStore("Dsa.DataStructures.CommonBinaryTree`2", avltreeNodeEx);
                tnodePdgc.AddToPredinedStore("System.Collections.Generic.Queue`1", avltreeNodeEx);
            }           

            return true;
        }

        /// <summary>
        /// Returns true if any generic name of this typedefinition has more than one option
        /// </summary>
        /// <param name="td"></param>
        /// <returns></returns>
        public static bool HasMoreOptionsForGenericName(TypeDefinition td)
        {
            foreach (var genericname in td.GenericTypeParameters)
            {
                PreDefinedGenericClassesStore pdgc;
                if (predefinedClasses.TryGetValue(genericname.Name, out pdgc))
                {
                    if (pdgc.NumDefinedTypes(td) > 1)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if any generic name of this typedefinition has more than one option
        /// </summary>
        /// <param name="td"></param>
        /// <returns></returns>
        public static IEnumerable<TypeEx> GetAllDefinedTypes(TypeDefinition td)
        {
            foreach (var genericname in td.GenericTypeParameters)
            {
                PreDefinedGenericClassesStore pdgc;
                if (predefinedClasses.TryGetValue(genericname.Name, out pdgc))
                {
                    foreach(var definedType in pdgc.GetAllDefinedTypes(td))
                        yield return definedType;
                }
            }

            yield break;
        }
        

        /// <summary>
        /// Gets predefined types. Here tdef can be null. In that scenario, if a dictionary exists for the generic
        /// name, than the first element in the dictionary is returned
        /// </summary>
        /// <param name="host"></param>
        /// <param name="genericname"></param>
        /// <param name="tdef"></param>
        /// <param name="genericType"></param>
        /// <returns></returns>
        public static bool TryGetInstantiatedClass(IPexComponent host, string genericname, TypeDefinition tdef, out TypeEx genericType)
        {         
            genericType = null;
            if (!bClassLoaded)
            {
                if (host != null)
                {
                    string assemblyName = null;
                    PexMeDynamicDatabase pmd = host.GetService<IPexMeDynamicDatabase>() as PexMeDynamicDatabase;
                    if (pmd != null)
                        assemblyName = pmd.AssemblyName;
                    else
                    {
                        PexMeStaticDatabase psd = host.GetService<IPexMeStaticDatabase>() as PexMeStaticDatabase;
                        if (psd != null)
                            assemblyName = psd.AssemblyName;
                    }

                    //One of PMD or PSD should be available by this time. If not nothing can be done
                    if (assemblyName == null)
                    {
                        host.Log.LogWarning(WikiTopics.MissingWikiTopic, "Hardcoded", "Could not load predefined generic classes data");
                        return false;
                    }

                    bClassLoaded = true;
                    LoadPredefinedGenericClasses(host, assemblyName);
                }
            }

            PreDefinedGenericClassesStore pdgc;
            if (predefinedClasses.TryGetValue(genericname, out pdgc))
            {
                if (tdef != null)
                {
                    if (pdgc.TryGetTypeExForTypename(tdef.FullName, out genericType))
                        return true;                    
                }               
                
                //tdef can be null in the case of instantiating methods and fields having generic arguments
                if (pdgc.TryGetSomeTypeEx(out genericType))
                    return true;                
            }
            return false;
        }
    }
}
