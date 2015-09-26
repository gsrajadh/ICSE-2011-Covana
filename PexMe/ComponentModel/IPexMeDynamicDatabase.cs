using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.Pex.Engine;
using Microsoft.ExtendedReflection.Coverage;
using PexMe.TermHandler;

namespace PexMe.Core
{
    /// <summary>
    /// Interface for PexMeDatabase
    /// </summary>
    public interface IPexMeDynamicDatabase
        : IService, IComponent
    {       
        /// <summary>
        /// Adds a method that is monitored by the current dynamic execution
        /// </summary>
        void AddMonitoredMethod(Method method);

        /// <summary>
        /// Adds a monitored field to the database. Updates two kinds of hashmaps
        /// a. Field to Method mapper, which gives what the methods modifying a given field
        /// b. Method to Field mapper, which gives what fields are modified by each method (later used to identify a minimized set of methods)
        /// </summary>
        /// <param name="tm"></param>
        /// <param name="method"></param>
        /// <param name="f"></param>
        /// <param name="indices"></param>
        /// <param name="fieldValue"></param>
        /// <param name="intialValue"></param>
        void AddMonitoredField(TermManager tm, Method method, Field f, Term[] indices, Term fieldValue, Term intialValue);

        /// <summary>
        /// Makes a mapping relationship between a calling method and a called method
        /// </summary>
        /// <param name="callingMethod"></param>
        /// <param name="calledMethod"></param>
        void AddMethodMapping(Method callingMethod, Method calledMethod);

        /// <summary>
        /// Adds a field to an unsuccessful code location
        /// </summary>
        /// <param name="location"></param>
        /// <param name="fields"></param>
        /// <param name="terms"></param>
        void AddFieldsOfUncoveredCodeLocations(CodeLocation location, SafeList<Field> fields, FieldModificationType fmt, 
            Term condition, string terms, int fitnessval, TypeEx explorableType, SafeList<TypeEx> allFieldTypes);

        /// <summary>
        /// Accumulates the maximum coverage
        /// </summary>
        /// <param name="newCoverage"></param>
        void AccumulateMaxCoverage(TaggedBranchCoverageBuilder<PexGeneratedTestName> newCoverage);

        /// <summary>
        /// Adds a controllable type
        /// </summary>
        /// <param name="type"></param>
        void AddControllableType(string type);

        /// <summary>
        /// Adds a pex generated factory method
        /// </summary>
        /// <param name="type"></param>
        /// <param name="factoryMethod"></param>
        void AddPexGeneratedFactoryMethod(string type, string factoryMethod);
    }
}
