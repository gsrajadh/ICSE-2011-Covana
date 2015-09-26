using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PexMe.Core;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;

namespace PexMe.ComponentModel
{
    /// <summary>
    /// Analyzes a type definition. All methods are static
    /// </summary>
    public class TypeAnalyzer
    {

        /// <summary>
        /// If type is an interface, returns all concrete implementing classes
        /// else if type is an abstract class, returns all concrete extending classes
        /// </summary>
        /// <param name="psd"></param>
        /// <param name="type"></param>
        /// <param name="extendingClasses"></param>
        /// <returns></returns>
        public static bool TryGetExtendingClasses(PexMeStaticDatabase psd, TypeEx type, out IIndexable<TypeDefinition> extendingClasses)
        {
            //load inheritance hierarchies if not already done
            psd.LoadInheritanceHierarchies();
            extendingClasses = null;

            TypeStore ts;
            if (!psd.TypeDictionary.TryGetValue(type.Definition, out ts))
                return false;
            
            SafeSet<TypeDefinition> extendingClassesSet = new SafeSet<TypeDefinition>();
            CollectAllExtendingClasses(ts, extendingClassesSet);

            if (extendingClassesSet.Count == 0)
                return false;

            var extendingClassesList = new SafeList<TypeDefinition>();
            foreach (var tdef in extendingClassesSet)
            {
                extendingClassesList.Add(tdef);
            }

            extendingClasses = extendingClassesList;
            return true;
        }

        /// <summary>
        /// recursively collects all extending classes
        /// </summary>
        /// <param name="ts"></param>
        private static void CollectAllExtendingClasses(TypeStore ts, SafeSet<TypeDefinition> extendingClasses)
        {
            if (extendingClasses.Contains(ts.Type))
                return;

            foreach (var innerts in ts.ExtendingTypes)
            {
                if (!innerts.Type.IsAbstract && !innerts.Type.IsInterface)
                    extendingClasses.Add(innerts.Type);

                CollectAllExtendingClasses(innerts, extendingClasses);
            }
        }

        /// <summary>
        /// Returns a method that produce a given type. Static methods are given higher preference than dynamic methods
        /// </summary>
        /// <param name="targetTypeEx"></param>
        /// <param name="producingMethods"></param>
        /// <returns></returns>
        public static bool TryGetProducingMethods(PexMeDynamicDatabase pmd, TypeEx targetTypeEx, out Method producingMethod)
        {
            var currAssembly = pmd.CurrAssembly;

            foreach (var tdef in currAssembly.TypeDefinitions)
            {
                if (IsAPexClass(tdef))
                    continue;

                foreach (var smdef in tdef.DeclaredStaticMethods)
                {
                    if (IsAPexMethod(smdef))
                        continue;

                    if (!smdef.IsVisible(VisibilityContext.Exported))
                        continue;

                    if (TryCheckReturnTypeOfMethod(pmd, tdef, smdef, targetTypeEx, out producingMethod))
                        return true;
                }

                foreach (var mdef in tdef.DeclaredInstanceMethods)
                {
                    if (IsAPexMethod(mdef))
                        continue;

                    if (!mdef.IsVisible(VisibilityContext.Exported))
                        continue;

                    if (TryCheckReturnTypeOfMethod(pmd, tdef, mdef, targetTypeEx, out producingMethod))
                        return true;
                }
            }

            producingMethod = null;
            return false;
        }

        /// <summary>
        /// Checks whether a class is a PexClass
        /// </summary>
        /// <param name="tdef"></param>
        /// <returns></returns>
        public static bool IsAPexClass(TypeDefinition tdef)
        {
            foreach (var attr in tdef.DeclaredAttributes)
            {
                if (attr.SerializableName.ToString().Contains("PexClassAttribute"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a class is a PexClass
        /// </summary>
        /// <param name="mdef"></param>
        /// <returns></returns>
        public static bool IsATestMethod(MethodDefinition mdef)
        {
            foreach (var attr in mdef.DeclaredAttributes)
            {
                //for mstest
                if (attr.SerializableName.ToString().Contains("TestMethodAttribute"))
                {
                    return true;
                }

                //for xunit
                if (attr.SerializableName.ToString().Contains("FactAttribute"))
                {
                    return true;
                }

                //for nunit
                if (attr.SerializableName.ToString().Contains("TestAttribute"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks whether a class is a PexClass
        /// </summary>
        /// <param name="tdef"></param>
        /// <returns></returns>
        public static bool IsATestClass(TypeDefinition tdef)
        {
            foreach (var attr in tdef.DeclaredAttributes)
            {
                if (attr.SerializableName.ToString().Contains("TestClassAttribute"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a class is a PexClass
        /// </summary>
        /// <param name="tdef"></param>
        /// <returns></returns>
        public static bool IsAPexMethod(MethodDefinition mdef)
        {
            foreach (var attr in mdef.DeclaredAttributes)
            {
                if (attr.SerializableName.ToString().Contains("PexMethodAttribute"))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryCheckReturnTypeOfMethod(PexMeDynamicDatabase pmd, TypeDefinition tdef, 
            MethodDefinition mdef, TypeEx targetTypeEx, out Method producingMethod)
        {
            var retType = mdef.ResultType;
            if (retType.ToString() == targetTypeEx.FullName)
            {
                producingMethod = mdef.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(pmd, tdef),
                    MethodOrFieldAnalyzer.GetGenericMethodParameters(pmd, mdef));
                return true;
            }

            //Get the actual type of return type and see whether it is assinable
            TypeEx retTypeEx;
            if (MethodOrFieldAnalyzer.TryGetTypeExFromName(pmd, pmd.CurrAssembly, retType.ToString(), out retTypeEx))
            {
                if (targetTypeEx.IsAssignableTo(retTypeEx))
                {
                    producingMethod = mdef.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(pmd, tdef),
                    MethodOrFieldAnalyzer.GetGenericMethodParameters(pmd, mdef));
                    return true;
                }
            }

            producingMethod = null;
            return false;
        }
    }
}
