using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.Pex.Engine.ComponentModel;
using PexMe.Common;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Utilities.Safe.Text;
using PexMe.PersistentStore;
using PexMe.ObjectFactoryObserver;
using Microsoft.ExtendedReflection.Coverage;
using Microsoft.Pex.Engine;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using PexMe.FactoryRecommender;
using PexMe.ComponentModel;
using PexMe.TermHandler;
using System.Reflection.Emit;

namespace PexMe.Core
{    
    /// <summary>
    /// Represents a list of uncovered code locations, which are from the same location and the same term
    /// </summary>
    public class UncoveredCodeLocationStoreList
    {
        public CodeLocation Location;
        public string ExplorableType;
        public int TermIndex;
        public SafeList<UncoveredCodeLocationStore> StoreList = new SafeList<UncoveredCodeLocationStore>();
    }

    /// <summary>
    /// A central storage for all data during explorations. 
    /// This instance lives across all path explorations and PUTs.
    /// </summary>    
    public class PexMeDynamicDatabase
        : PexComponentBase, IPexMeDynamicDatabase
    {
        public PexMeDynamicDatabase()
        {
            PexMeDumpReader.TryLoadDynamicDatabase(this);
        }

        SafeSet<string> monitoredMethods = new SafeSet<string>();   //TODO: Can be later replace to SafeInt using global index of methods
        public SafeSet<string> MonitoredMethods
        {
            get
            {
                return monitoredMethods;
            }
        }


        internal string assemblyName;
        /// <summary>
        /// Stores the name of the assembly
        /// </summary>        
        public string AssemblyName
        {
            get
            {
                if (assemblyName == null)
                {
                    assemblyName = this.Services.CurrentAssembly.Assembly.Assembly.ShortName;
                }
                return assemblyName;
            }

            set
            {
                this.assemblyName = value;
            }
        }

        /// <summary>
        /// Stores the name of the assembly
        /// </summary>        
        public AssemblyEx CurrAssembly
        {
            get
            {
                return this.Services.CurrentAssembly.Assembly.Assembly;
            }
        }
        
        /// <summary>
        /// Stores the mapping from methods to fields
        /// </summary>

        SafeDictionary<Method, MethodStore> methodDic;
        public SafeDictionary<Method, MethodStore> MethodDictionary
        {
            get
            {
                return methodDic;
            }

            set
            {
                this.methodDic = value;
            }
        }

        /// <summary>
        /// Gets a method store from a method signature
        /// </summary>
        /// <param name="msignature"></param>
        /// <param name="ms"></param>
        /// <returns></returns>
        public bool TryGetMethodStoreFromSignature(string msignature, out MethodStore ms)
        {
            foreach (var key in this.MethodDictionary.Keys)
            {
                string keysig = MethodOrFieldAnalyzer.GetMethodSignature(key);
                if (keysig == msignature)
                {
                    ms = this.MethodDictionary[key];
                    return true;
                }
            }

            ms = null;
            return false;
        }

        /// <summary>
        /// Stores the mapping from fields to methods
        /// </summary>
        SafeDictionary<Field, FieldStore> fieldDic;
        public SafeDictionary<Field, FieldStore> FieldDictionary
        {
            get
            {
                return fieldDic;
            }

            set
            {
                this.fieldDic = value;
            }
        }

        /// <summary>
        /// Set of object types that can be controlled from outside
        /// </summary>
        SafeSet<string> controllableTypes = new SafeSet<string>();
        public SafeSet<string> ControllableTypes
        {
            get {
                return controllableTypes;
            }
        }

        /// <summary>
        /// Adds a controllable type
        /// </summary>
        /// <param name="type"></param>
        public void AddControllableType(string type)
        {
            this.controllableTypes.Add(type);
        }


        /// <summary>
        /// Types for which factory methods are requested. Mainly for debugging
        /// </summary>
        SafeDictionary<string, SafeList<string>> pexGeneratedFactories = new SafeDictionary<string, SafeList<string>>();
        public SafeDictionary<string, SafeList<string>> PexGeneratedFactories
        {
            get
            {
                return pexGeneratedFactories;
            }
        }

        /// <summary>
        /// Stores methods that are ever explored 
        /// </summary>
        public HashSet<string> AllExploredMethods = new HashSet<string>();
        public Method CurrentPUTMethod = null;

        /// <summary>
        /// Stores the explorations that are kept in pending
        /// </summary>
        public HashSet<string> PendingExplorationMethods = new HashSet<string>();
        
        /// <summary>
        /// Adds a controllable type
        /// </summary>
        /// <param name="type"></param>
        public void AddPexGeneratedFactoryMethod(string type, string factoryMethod)
        {
            SafeList<string> suggestedFactoryMethods;
            if (!this.pexGeneratedFactories.TryGetValue(type, out suggestedFactoryMethods))
            {
                suggestedFactoryMethods = new SafeList<string>();
                this.pexGeneratedFactories[type] = suggestedFactoryMethods;
            }

            suggestedFactoryMethods.Add(factoryMethod);
        }
        
        /// <summary>
        /// Maintains the uncovered locations in the dictionary. Each uncovered location
        /// can have multiple locations
        /// </summary>
        SafeDictionary<string, UncoveredCodeLocationStoreList> unCoveredLocationDic =
            new SafeDictionary<string, UncoveredCodeLocationStoreList>();
        public SafeDictionary<string, UncoveredCodeLocationStoreList> UncoveredLocationDictionary
        {
            get
            {
                return unCoveredLocationDic;
            }
        }

        /// <summary>
        /// stores all suggestions for factories. stores only strings especially for the purpose of 
        /// serialization
        /// </summary>
        System.Collections.Generic.Dictionary<string, FactorySuggestionStore> factorySuggestionDic = null;            
        public System.Collections.Generic.Dictionary<string, FactorySuggestionStore> FactorySuggestionsDictionary
        {
            get
            {
                return this.factorySuggestionDic;
            }

            set
            {
                this.factorySuggestionDic = value;
            }
        }

        /// <summary>
        /// Stores the last executed method call sequence within the factory method
        /// </summary>
        public SafeList<Method> LastExecutedFactoryMethodCallSequence;
        public bool DefectDetectingSequence; //Denotes whether this sequence is a defect detecting sequence

        /// <summary>
        /// Stores the last executed method call sequence in the code under test
        /// </summary>
        public SafeList<Method> LastExecutedCUTMethodCallSequence;

        /// <summary>
        /// Stores the last observed condition string
        /// </summary>
        public string LastObservedConditionString;

        /// <summary>
        /// Stores the current aggregated coverage information
        /// </summary>
        private TaggedBranchCoverageBuilder<PexGeneratedTestName> coverageBuilderMaxAggregator;

        /// <summary>
        /// Initializes the PexMe Database
        /// </summary>
        protected override void Initialize()
        {
            //this.Log.LogMessage(PexMeLogCategories.MethodBegin, "Begin of PexMeDatabase.Initialize() method");
            coverageBuilderMaxAggregator = new TaggedBranchCoverageBuilder<PexGeneratedTestName>();
        }      
               

        /// <summary>
        /// Adds a method that is monitored by the current dynamic execution
        /// </summary>
        public void AddMonitoredMethod(Method method)
        {
            monitoredMethods.Add(method.FullName);
        }

        /// <summary>
        /// Filters out covered code locations
        /// </summary>
        public void FilterOutCoveredCodeLocations()
        {
            //Finding out covered locations
            //Also Finding out other locations in system libraries
            //that do not contribute to increase in the covered. Those
            //libraries should also be removed
            var coveredLocations = new SafeSet<UncoveredCodeLocationStoreList>();
            foreach (var uclStoreList in this.UncoveredLocationDictionary.Values)
            {
                var cl = uclStoreList.Location;
                if (this.IsBranchOffsetCoveredInMethod(cl.Method, cl.Offset))
                {
                    coveredLocations.Add(uclStoreList);
                }
                else
                {
                    //Check whether the uncoverd location is in system library
                    var currAssembly = this.Services.CurrentAssembly.Assembly.Assembly;
                    bool bDeleteUCList = true;

                    foreach (var ucstore in uclStoreList.StoreList)
                    {
                        //Check the CUTMethodCall sequence in the ucstore to decide 
                        //whether to continue with this
                        foreach (var method in ucstore.CUTMethodCallSequence)
                        {
                            var methoddef = method.Definition;
                            if (methoddef.Module.Assembly == currAssembly && !this.AreAllOffsetsCoveredInMethod(method))
                            {
                                bDeleteUCList = false;
                                break;
                            }
                        }

                        if (!bDeleteUCList)
                            break;
                    }

                    if (bDeleteUCList)
                        coveredLocations.Add(uclStoreList);
                }
            }         

            foreach (var covered in coveredLocations)
            {
                var key = UncoveredCodeLocationStore.GetKey(covered.Location.ToString(), covered.ExplorableType, covered.TermIndex);
                this.UncoveredLocationDictionary.Remove(key);
            }
        }

        /// <summary>
        /// Checks whether all blocks within the method are covered
        /// </summary>
        /// <param name="methoddef"></param>
        /// <returns></returns>
        public bool AreAllOffsetsCoveredInMethod(Method method)
        {
            var methoddef = method.Definition;
            MethodDefinitionBodyInstrumentationInfo info;
            if (!methoddef.TryGetBodyInstrumentationInfo(out info))
                return false;
                       
            CoverageDomain domain;
            int[] hits;
            if (!this.coverageBuilderMaxAggregator.TryGetMethodHits(methoddef, out domain, out hits))
                return false;

            MethodBodyEx body;
            if (!method.TryGetBody(out body) || !body.HasInstructions)
                return false;
            
            int offset = 0;
            Instruction instruction;
            while (body.TryGetInstruction(offset, out instruction))
            {
                //For a branching instruction, check whether both sides are covered
                if (MethodOrFieldAnalyzer.BranchOpCodes.Contains(instruction.OpCode))
                {
                    if (NumTimesOffsetCovered(offset, info, hits) <= 1)
                        return false;
                }
                offset = instruction.NextOffset;            
            }
            return true;
        }

        /// <summary>
        /// Checks whether a given offset is covered
        /// </summary>
        public bool IsBranchOffsetCoveredInMethod(MethodDefinition methoddef, int offset)
        {
            MethodDefinitionBodyInstrumentationInfo info;
            if (methoddef.TryGetBodyInstrumentationInfo(out info))
            {
                int coveredBranchesCount = 0;
                CoverageDomain domain;
                int[] hits;
                if (this.coverageBuilderMaxAggregator.TryGetMethodHits(methoddef, out domain, out hits))
                {
                    coveredBranchesCount = NumTimesOffsetCovered(offset, info, hits);
                }

                if (coveredBranchesCount > 1)//the location has been covered
                {
                    return true;
                }
            }

            return false;
        }

        private static int NumTimesOffsetCovered(int offset, MethodDefinitionBodyInstrumentationInfo info, 
            int[] hits)
        {
            int coveredBranchesCount = 0;
            foreach (var outgoingBranchLabel in info.GetOutgoingBranchLabels(offset))
            {
                //check whether any of the branches of this location are not covered
                //Here outgoing branch labels are again offsets only.
                if (outgoingBranchLabel < hits.Length && hits[outgoingBranchLabel] > 0)
                {
                    coveredBranchesCount++;
                }
            }
            return coveredBranchesCount;
        }

        #region IPexMeDatabase Members
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
        public void AddMonitoredField(TermManager tm, Method method, Field f, Term[] indices, Term fieldValue, Term initialValue)
        {
            string arrayIndex = "";
            using (PexMeTermRewriter pexmeRewriter = new PexMeTermRewriter(tm))
            {
                fieldValue = pexmeRewriter.VisitTerm(default(TVoid), fieldValue); //update the field value to accomodate array-type field           
                //if (indices.Length == 0) //not an array-type field               
                if (indices.Length == 1) //is an array-type field
                {
                    arrayIndex = " at index of " + indices[0].UniqueIndex.ToString();
                }
                
                if(initialValue != null)
                    initialValue = pexmeRewriter.VisitTerm(default(TVoid), initialValue);
            }   

            //Updating the method store
            MethodStore ms;
            if(!methodDic.TryGetValue(method, out ms))
            {
                ms = new MethodStore();
                ms.methodName = method;
                methodDic[method] = ms;
            }

            ms.WriteFields.Add(f);
            //TODO: Gather information of read fields

            //Updating the field store
            FieldStore fs;
            if (!fieldDic.TryGetValue(f, out fs))
            {
                fs = new FieldStore();
                fs.FieldName = f;
                fieldDic[f] = fs;
            }

            TypeEx declaringType;
            if (!method.TryGetDeclaringType(out declaringType))
            {
                this.Log.LogError(WikiTopics.MissingWikiTopic, "monitorfield",
                    "Failed to get the declaring type for the method " + method.FullName);
                return;
            }

            SafeSet<Method> writeMethods;
            if (!fs.WriteMethods.TryGetValue(declaringType, out writeMethods))
            {
                writeMethods = new SafeSet<Method>();
                fs.WriteMethods[declaringType] = writeMethods;
            }
            writeMethods.Add(method);

            var sb = new SafeStringBuilder();
            var swriter = new TermSExpWriter(tm, new SafeStringWriter(sb), true, false);
            swriter.Write(fieldValue);
            sb.Append(arrayIndex);

            int value;
            if (tm.TryGetI4Constant(fieldValue, out value))
            {
                int initialval;
                if (initialValue != null)
                    tm.TryGetI4Constant(initialValue, out initialval);
                else
                    initialval = 0;
                
                sb.Append("  constant value: " + value);

                if (f.Type.ToString() != "System.Boolean")
                {
                    if (value < initialval)
                        fs.ModificationTypeDictionary[method] = FieldModificationType.DECREMENT;
                    else if (value > initialval)
                    {
                        fs.ModificationTypeDictionary[method] = FieldModificationType.INCREMENT;

                        if (value == initialval + 1)
                            fs.PreciseModificationTypeDictionary[method] = FieldModificationType.INCREMENT_ONE;
                    }
                    else
                        fs.ModificationTypeDictionary[method] = FieldModificationType.UNKNOWN;
                }
                else
                {
                    if (value == 0)
                        fs.ModificationTypeDictionary[method] = FieldModificationType.FALSE_SET;
                    else
                        fs.ModificationTypeDictionary[method] = FieldModificationType.TRUE_SET;
                }
            }
            else if (tm.IsDefaultValue(fieldValue))
            {
                if (initialValue != null && !tm.IsDefaultValue(initialValue))
                    fs.ModificationTypeDictionary[method] = FieldModificationType.NULL_SET;
                else
                    fs.ModificationTypeDictionary[method] = FieldModificationType.UNKNOWN;

                sb.Append("  null reference ");
            }
            else
            {
                if (initialValue == null)
                    fs.ModificationTypeDictionary[method] = FieldModificationType.NON_NULL_SET;
                else
                    fs.ModificationTypeDictionary[method] = FieldModificationType.UNKNOWN;

                sb.Append("  not-null reference ");
            }

            fs.FieldValues.Add(sb.ToString());
        }

        /// <summary>
        /// Makes a mapping relationship between a calling method and a called method
        /// </summary>
        /// <param name="callingMethod"></param>
        /// <param name="calledMethod"></param>
        public void AddMethodMapping(Method callingMethod, Method calledMethod)
        {
            //Updating the method store for calling methods
            MethodStore callingMethodStore = null;
            if (!methodDic.TryGetValue(callingMethod, out callingMethodStore))
            {
                callingMethodStore = new MethodStore();
                callingMethodStore.methodName = calledMethod;
                methodDic[callingMethod] = callingMethodStore;
            }
            callingMethodStore.CalledMethods.Add(calledMethod);

            //Updating the method store for called methods
            MethodStore calledMethodStore = null;
            if(!methodDic.TryGetValue(calledMethod, out calledMethodStore))
            {
                calledMethodStore = new MethodStore();
                calledMethodStore.methodName = calledMethod;
                methodDic[calledMethod] = calledMethodStore;
            }
            
            TypeEx callingMethodType;
            if (!callingMethod.TryGetDeclaringType(out callingMethodType))
            {
                this.Log.LogError(WikiTopics.MissingWikiTopic, "methodmapping",
                    "Failed to get the declared type for method " + calledMethod);
                return;
            }

            SafeSet<Method> localCallingMethods;
            if (!calledMethodStore.CallingMethods.TryGetValue(callingMethodType, out localCallingMethods))
            {
                localCallingMethods = new SafeSet<Method>();
                calledMethodStore.CallingMethods[callingMethodType] = localCallingMethods;
            }
            localCallingMethods.Add(callingMethod);            
        }

        /// <summary>
        /// Adds a field to an unsuccessful code location.         
        /// </summary>
        /// <param name="location"></param>
        /// <param name="fields"></param>
        public void AddFieldsOfUncoveredCodeLocations(CodeLocation location, SafeList<Field> fields, FieldModificationType fmt, 
            Term condition, string terms, int fitnessval, TypeEx explorableType, SafeList<TypeEx> allFieldTypes)
        {
            //No need to process this location. 
            if (fields.Count == 0)
            {                
                return;
            }

            Field targetField;  
            TypeEx declaringType;   //This declaring type is considered as explorable type in the rest of the analysis
            if (!PexMeFactoryGuesser.GetTargetExplorableField(this, fields, out targetField, out declaringType))
            {
                this.Log.LogError(WikiTopics.MissingWikiTopic, "factoryguesser",
                   "Failed to retrieve the target field for uncovered location " + location.ToString());
                return;
            }

            //Compare the declaring type and actual explorable type.
            //If there is a inheritance relation, use the actual one
            if (explorableType.IsAssignableTo(declaringType))
            {
                declaringType = explorableType;
            }

            var uclskey = UncoveredCodeLocationStore.GetKey(location.ToString(), declaringType.ToString(), condition.UniqueIndex);            
            UncoveredCodeLocationStoreList uclslist;
            if (!this.unCoveredLocationDic.TryGetValue(uclskey, out uclslist))
            {
                uclslist = new UncoveredCodeLocationStoreList();
                uclslist.Location = location;
                uclslist.ExplorableType = declaringType.ToString();
                uclslist.TermIndex = condition.UniqueIndex;
                this.unCoveredLocationDic[uclskey] = uclslist;
            }
                
            var ucls = new UncoveredCodeLocationStore();
            ucls.Location = location;
            ucls.ExplorableType = declaringType;
            ucls.TargetField = targetField;
            ucls.AllFields.AddRange(fields);
            ucls.AllFieldTypes.AddRange(allFieldTypes);
            ucls.TermIndex = condition.UniqueIndex;
            //add the sequence of method calls
            ucls.MethodCallSequence = new MethodSignatureSequence();
            foreach (var m in this.LastExecutedFactoryMethodCallSequence)
            {             
                ucls.MethodCallSequence.Sequence.Add(MethodOrFieldAnalyzer.GetMethodSignature(m));
            }
            ucls.IsADefectDetectingSequence = this.DefectDetectingSequence;
            ucls.CUTMethodCallSequence = this.LastExecutedCUTMethodCallSequence;

            if (!uclslist.StoreList.Contains(ucls))
            {                
                ucls.TextualTerms.Add(terms);                
                ucls.DesiredFieldModificationType = fmt;
                ucls.Fitnessvalue = fitnessval;
                uclslist.StoreList.Add(ucls);
            }
        }

        /// <summary>
        /// Accumulates the maximum coverage
        /// </summary>
        /// <param name="taggedCovered"></param>
        public void AccumulateMaxCoverage(TaggedBranchCoverageBuilder<PexGeneratedTestName> newCoverage)
        {
            this.coverageBuilderMaxAggregator.Max(newCoverage);
        }

        /// <summary>
        /// Check whether a previously uncovered location is covered now in this run
        /// </summary>
        public bool IsPrevUncoveredLocationCovered(PersistentUncoveredLocationStore pucls)
        {
            //retrieve the method
            MethodDefinition associatedMethod;
            if (!MethodOrFieldAnalyzer.TryGetMethodDefinition(this, pucls.AssemblyShortName, pucls.declaringTypeStr,
                pucls.MethodSignature, out associatedMethod))
            {
                this.Log.LogWarning(WikiTopics.MissingWikiTopic, "serialization", 
                    "Failed to retrieve the method with signature " + pucls.MethodSignature);
                return false;
            }

            //Check whether the offset is covered in this method
            if (this.IsBranchOffsetCoveredInMethod(associatedMethod, pucls.Offset))
                return true;

            return false;
        }
        #endregion

        /// <summary>
        /// Checks whether any previously uncovered locations are covered in this new run
        /// </summary>
        internal void CheckForNewlyCoveredLocations()
        {
            foreach (var fss in this.FactorySuggestionsDictionary.Values)
            {
                foreach (var pucls in fss.locationStoreSpecificSequences.Values)
                {
                    if (pucls.IsDormat())
                        continue;

                    if (!pucls.AlreadyCovered && this.IsPrevUncoveredLocationCovered(pucls))
                    {
                        //this.Log.LogMessage("debug", "Covered the location: " + pucls.CodeLocation.ToString());
                        //StringBuilder sb = new StringBuilder();
                        //foreach (Method m in this.LastExecutedMethodCallSequence)
                        //{
                        //   sb.Append(m.ToString() + "\n");
                        //}
                        //this.Log.LogMessage("debug", "Sequence recorded: " + sb.ToString());

                        pucls.HitSequence = new MethodSignatureSequence();
                        foreach (Method m in this.LastExecutedFactoryMethodCallSequence)
                        {
                            pucls.HitSequence.Sequence.Add(MethodOrFieldAnalyzer.GetMethodSignature(m));
                        }
                        pucls.AlreadyCovered = true;
                    }
                }
            }
        }

        SafeDictionary<string, PersistentUncoveredLocationStore> tempAllLocationStore = null;

        /// <summary>
        /// Compares current uncovered locations with the previous uncovered locations
        /// </summary>
        internal void AnalyzePreviousAndCurrentUncoveredLoc(string currPUTSignature,
            out HashSet<string> allGivenUpLocations, out HashSet<string> allCoveredLocations,
            out HashSet<string> newUncoveredLocations, out bool hasSomeCoveredLocation, 
            out bool bAllAreNewLocations, out bool bNoneAreNewLocations)
        {
            hasSomeCoveredLocation = false;
            bAllAreNewLocations = true;
            bNoneAreNewLocations = true;

            var resolvedPrevLocations = new SafeSet<string>();
            var bestFitnessValues = new SafeDictionary<string, int>();

            //This dictionary is constructed since the FactorySuggestionStore is based
            //on declarting type not on the explorable type, which can be different
            allGivenUpLocations = new HashSet<string>();
            allCoveredLocations = new HashSet<string>();
            tempAllLocationStore = new SafeDictionary<string, PersistentUncoveredLocationStore>();

            //All final suggested sequences of the current PUT
            var putspecificsequences = new List<MethodSignatureSequence>();

            foreach (var fss in this.FactorySuggestionsDictionary.Values)
            {
                foreach (var codelockey in fss.locationStoreSpecificSequences.Keys)
                {
                    var pucls = fss.locationStoreSpecificSequences[codelockey];
                    if (!pucls.IsDormat())  //Donot touch dormant persistent stores
                        tempAllLocationStore[codelockey] = pucls;
                }

                foreach (var givenUpLocation in fss.PermanentFailedUncoveredLocations)
                    allGivenUpLocations.Add(givenUpLocation);

                foreach (var coveredLocation in fss.SuccessfulCoveredLocations)
                    allCoveredLocations.Add(coveredLocation);

                //MethodSignatureSequenceList mssl;
                //if (fss.FinalPUTSequences.TryGetValue(currPUTSignature, out mssl))
                //{
                //    foreach (var seq in mssl.SequenceList)
                //    {
                //        if (!putspecificsequences.Contains(seq))
                //            putspecificsequences.Add(seq);
                //    }
                //}
            }

            var failedLocations = new SafeSet<PersistentUncoveredLocationStore>();
            //Traverse all uncovered locations
            foreach (var ucovLocList in this.UncoveredLocationDictionary.Values)
            {              
                var locationStr = ucovLocList.Location.ToString();
                var key = UncoveredCodeLocationStore.GetKey(ucovLocList.Location.ToString(),
                    ucovLocList.ExplorableType.ToString(), ucovLocList.TermIndex);

                //Check whether there are any defect detecting sequences. If yes promote them
                //in the associated factory store
                foreach (var ucls in ucovLocList.StoreList)
                {
                    if (ucls.IsADefectDetectingSequence)
                    {
                        var fss = this.FactorySuggestionsDictionary[ucls.ExplorableType.ToString()];
                        SafeDebug.AssumeNotNull(fss, "fss cannot be null");
                        fss.AddToDefectDetectingSequences(ucls.MethodCallSequence);
                    }
                }

                if (allGivenUpLocations.Contains(key))
                {
                    //This location has been earlier given up. No need to deal with this
                    resolvedPrevLocations.Add(key);
                    this.Log.LogMessage(WikiTopics.MissingWikiTopic, "Location " + locationStr + " is ignored since it was already given up earlier!!!");
                    continue;
                }

                if (allCoveredLocations.Contains(key))
                {
                    //This location has been covered earlier. Ideally this case should not happen
                    //resolvedPrevLocations.Add(key);
                    this.Log.LogMessage(WikiTopics.MissingWikiTopic, 
                        "Location " + locationStr + " is previously covered, but can be reported since the caller could be different.");
                    bAllAreNewLocations = false;
                    //continue;
                }

                //Get the associated factory suggestion store
                if (tempAllLocationStore.ContainsKey(key))
                {
                    bAllAreNewLocations = false;
                    var pucls = tempAllLocationStore[key];
                    resolvedPrevLocations.Add(key);
                    
                    //For some formats such as TRUE_SET, we do not need the fitness measure
                    //If they are not covered in one attempt, they won't be covered any time
                    if (ucovLocList.StoreList.Count > 0)
                    {
                        var fmt = ucovLocList.StoreList[0].DesiredFieldModificationType;
                        if (!this.IsFitnessRequired(fmt))
                        {
                            pucls.NumberOfUnsuccessfulAttempts = PexMeConstants.MAX_UNSUCCESSFUL_ATTEMPTS + 1;
                        }
                    }

                    //Reached the threshold of number of attempts. So deleting this uncovered location forever
                    if (pucls.NumberOfUnsuccessfulAttempts + 1 <= PexMeConstants.MAX_UNSUCCESSFUL_ATTEMPTS)
                    {                        
                        //Handle according to fitness value and drop the other methods that
                        //acutally did not help increase the fitness value
                        this.RemoveSuggestionsWithLowFitness(ucovLocList, tempAllLocationStore[key], putspecificsequences);
                        this.Log.LogMessage(WikiTopics.MissingWikiTopic,
                        "Location " + locationStr + " is resolved and is still uncovered in the new run " +
                        "(fitness: " + pucls.Fitnessvalue + "), (Attempt: " + pucls.NumberOfUnsuccessfulAttempts + ")");                        
                    }
                    else
                    {
                        this.Log.LogMessage(WikiTopics.MissingWikiTopic, 
                        "Location " + locationStr + " is resolved and but not making any process!!! Will be deleted forever");
                        //This pucls data will be deleted forever since it reached its max attempts without any progress
                        failedLocations.Add(pucls);                        
                    }
                }
            }

            //New locations that added to the factory suggestion store
            newUncoveredLocations = new HashSet<string>();
            foreach (var ucovLocList in this.UncoveredLocationDictionary.Values)
            {
                var key = UncoveredCodeLocationStore.GetKey(ucovLocList.Location.ToString(),
                        ucovLocList.ExplorableType.ToString(), ucovLocList.TermIndex);
                if (!resolvedPrevLocations.Contains(key))
                {
                    newUncoveredLocations.Add(key);
                    this.Log.LogMessage(WikiTopics.MissingWikiTopic, 
                        "Location " + ucovLocList.Location.ToString() + " is newly added in the new run");
                    bNoneAreNewLocations = false;
                }                
            }

            //Unresolved locations from the previous run. This means that these sequences
            //are either already covered or not covered due to some new exception...
            var unresolvedPrevLocations = new SafeSet<PersistentUncoveredLocationStore>();
            var alreadyCoveredLocations = new SafeSet<PersistentUncoveredLocationStore>();
            foreach (var fss in this.FactorySuggestionsDictionary.Values)
            {
                var allRemovedPUCLS = new List<PersistentUncoveredLocationStore>();

                //Delete all failed locations if the suggested methods for this
                //failed location are all actually already explored. If not, place
                //them in pending status. Usually this case happens, if covering
                //the same location within the method is required by another location
                foreach (var pucls in failedLocations)
                {
                    var key = UncoveredCodeLocationStore.GetKey(pucls.CodeLocation.ToString(),
                        pucls.ExplorableType.ToString(), pucls.TermIndex);
                                        
                    fss.RemoveUncoveredLocationStore(pucls, false, this);
                    allRemovedPUCLS.Add(pucls);
                }

                foreach (var codelockey in fss.locationStoreSpecificSequences.Keys)
                {
                    var tpucls = fss.locationStoreSpecificSequences[codelockey];
                    if (tpucls.IsDormat())
                        continue;

                    if (allGivenUpLocations.Contains(codelockey))
                    {
                        bAllAreNewLocations = false;
                        unresolvedPrevLocations.Add(tpucls);
                        this.Log.LogWarning(WikiTopics.MissingWikiTopic, "UncoveredLocation",
                            "Location " + codelockey + " was already given up. Should not be reported again!!! Anyways, Deleting this location forever");
                        continue;
                    }

                    if (!resolvedPrevLocations.Contains(codelockey))
                    {
                        //Check whether this location is covered based on the coverage                       
                        if (tpucls.AlreadyCovered || this.IsPrevUncoveredLocationCovered(tpucls))
                        {
                            alreadyCoveredLocations.Add(tpucls);                            
                            this.Log.LogMessage(WikiTopics.MissingWikiTopic, 
                                "Location " + codelockey + " is successfully covered in the new run");
                            hasSomeCoveredLocation = true;
                        }
                        else
                        {
                            bAllAreNewLocations = false;
                            unresolvedPrevLocations.Add(tpucls);
                            this.Log.LogWarning(WikiTopics.MissingWikiTopic, "UncoveredLocation",
                                "Location " + codelockey + " from the previous run is not found in the new run!!! Deleting this location forever");
                        }
                    }
                }                

                //Delete all unresolved locations as they won't be required anymore!!!
                foreach (var pucls in unresolvedPrevLocations)
                {
                    fss.RemoveUncoveredLocationStore(pucls, false, this);
                    allRemovedPUCLS.Add(pucls);
                }

                //Handle all removed PUCLS
                foreach (var pucls in allRemovedPUCLS)
                {
                    var key = UncoveredCodeLocationStore.GetKey(pucls.CodeLocation, pucls.ExplorableType, pucls.TermIndex);
                    fss.PermanentFailedUncoveredLocations.Add(key);              
                    if (!fss.TemporaryFailedUncoveredLocations.ContainsKey(key))
                    {
                        //Are there any active uncovered locations
                        if (this.AnyActiveUncoveredLocations())
                            fss.TemporaryFailedUncoveredLocations[key] = 1;
                        else
                            fss.TemporaryFailedUncoveredLocations[key] = PexMeConstants.MAX_UNCOVEREDLOC_ATTEMPTS;
                    }
                }

                //Delete all the information regarding covered locations and upgrade their specific factory methods
                foreach (var pucls in alreadyCoveredLocations)
                {
                    fss.RemoveUncoveredLocationStore(pucls, true, this);
                }

                alreadyCoveredLocations.Clear();
            }
        }

        /// <summary>
        /// Targets to remove those sequences with low or the same fitness
        /// </summary>
        /// <param name="ucovLocList"></param>
        /// <param name="persistentUncoveredLocationStore"></param>
        private void RemoveSuggestionsWithLowFitness(UncoveredCodeLocationStoreList ucovLocList, 
            PersistentUncoveredLocationStore pucls, List<MethodSignatureSequence> putspecificsequences)
        {            
            pucls.NumberOfUnsuccessfulAttempts++;
            int bestFitness = pucls.Fitnessvalue;

            foreach (var ucovloc in ucovLocList.StoreList)
            {
                if (ucovloc.Fitnessvalue < bestFitness)
                    bestFitness = ucovloc.Fitnessvalue;
            }

            var explorableType = ucovLocList.ExplorableType;
            var tempSuggestedSequences = new List<MethodSignatureSequence>();
            tempSuggestedSequences.AddRange(pucls.SuggestedMethodSequences);

            //remove those suggestions that have lower fitness values
            foreach (var ucovloc in ucovLocList.StoreList)
            {
                //Get matching sequence from pucls
                MethodSignatureSequence matchingseq;
                if (!FactorySuggestionStore.TryGetMatchingSequence(ucovloc.MethodCallSequence, tempSuggestedSequences, out matchingseq))
                {                    
                    //This sequence is not there in our suggested sequence. This should have happened
                    //due to the case that it came from other uncovered location stores, and is helping
                    //the current store.
                    if (ucovloc.Fitnessvalue <= bestFitness)
                    {
                        var mss = new MethodSignatureSequence();
                        foreach (var methodinucov in ucovloc.MethodCallSequence.Sequence)
                        {
                            if (methodinucov.Contains("..ctor(")) //Don't add constructors
                                continue;
                            if (!methodinucov.Contains(explorableType)) //Ignore the method calls from other types
                                continue;
                            mss.Sequence.Add(methodinucov);
                        }

                        if(mss.Sequence.Count > 0)
                            pucls.SuggestedMethodSequences.Add(mss);
                    }
                    
                    continue;
                }

                tempSuggestedSequences.Remove(matchingseq);
                if (ucovloc.Fitnessvalue > bestFitness)
                {
                    //Previous sequence is of no use as it is leading to higher fitness value
                    pucls.SuggestedMethodSequences.Remove(matchingseq);
                }                
            }

            if (tempSuggestedSequences.Count != 0)
            {
                //Not all sequences are assigned fitness values. Raise warnings for other sequences
                this.Log.LogWarning(WikiTopics.MissingWikiTopic, "sequencematching",
                    "Fitness values are not available for some previous sequences, Needs to handle this case!!!");

                //Remove those suggestions whose fitness value is not evaluated at all. Ideally
                //this case should not happen. Needs additional debugging.
                //foreach (var seq in tempSuggestedSequences)
                //    pucls.SuggestedMethodSequences.Remove(seq);
            }

            //Update the previous fitness value with the current. If there is any improvement in the fitness, reset the number
            //of unsuccessful attempts
            if (pucls.Fitnessvalue != bestFitness)
            {
                pucls.NumberOfUnsuccessfulAttempts = 0;
                pucls.Fitnessvalue = bestFitness;
            }
        }

        /// <summary>
        /// Checks whether fitness strategy matters or not. Since, fitness
        /// is required only for integers, but not for all kinds of types such
        /// as boolean or non-null
        /// </summary>
        /// <returns></returns>
        private bool IsFitnessRequired(FieldModificationType fmt)
        {
            switch (fmt)
            {
                case FieldModificationType.INCREMENT:
                case FieldModificationType.DECREMENT:
                case FieldModificationType.INCREMENT_ONE:
                    return true;
                case FieldModificationType.FALSE_SET:
                case FieldModificationType.TRUE_SET:
                case FieldModificationType.NULL_SET:
                case FieldModificationType.NON_NULL_SET:
                    return false;
                default:
                    return false;                 
            }
        }

        /// <summary>
        /// Method that decides whether Pex needs to be re-executed based on current number of uncovered
        /// locations in the persistent storage. This information is used by external process in re-laucnhing the
        /// entire Pex process
        /// </summary>
        /// <returns></returns>
        public bool NeedReExecute(bool bHasSomeCoveredLocation, bool bAllAreNewLocations, bool bNoneAreNewLocations)
        {       
            bool bAnyTemporaryLocations = false;

            //Re-execution is based on three different factors: Introduced for improving the performance
            //1. All active locations should be new
            //2. All active locations should be old
            //3. If there are some new and some old, atleast one of the old should have been covered.
            if (!(bAllAreNewLocations || bNoneAreNewLocations) && !bHasSomeCoveredLocation)
            {
                this.Log.LogMessage("reexecute", "Issuing STOP since violation to the three factors for re-execution happened");
                return false;
            }

            if (AnyActiveUncoveredLocations())
                return true;

            if (!PexMeConstants.IGNORE_UNCOVEREDLOC_RESURRECTION)
            {
                //Check whether any temporary uncovered locations can be recovered
                foreach (var fss in FactorySuggestionsDictionary.Values)
                {
                    var allLocStr = new HashSet<string>();
                    foreach (var loc in fss.TemporaryFailedUncoveredLocations.Keys)
                    {
                        //Resurrect those locations that are not in system libraries.
                        //this is only a heuristic that resurrecting the locations in system libraries
                        //may not be of help
                        if (fss.TemporaryFailedUncoveredLocations[loc] < PexMeConstants.MAX_UNCOVEREDLOC_ATTEMPTS
                            && !fss.UncoveredSystemLibLocations.Contains(loc))
                        {
                            this.Log.LogWarning(WikiTopics.MissingWikiTopic, "UncoveredLocation",
                                "Resurrecting the uncovered location store " + loc);
                            allLocStr.Add(loc);
                            fss.PermanentFailedUncoveredLocations.Remove(loc);
                            bAnyTemporaryLocations = true;
                        }
                    }

                    foreach (var loc in allLocStr)
                    {
                        fss.TemporaryFailedUncoveredLocations[loc]++;
                    }
                }
            }

            if (bAnyTemporaryLocations)
                return true;

            return false;
        }

        public bool AnyActiveUncoveredLocations()
        {
            //Check whether there are still uncovered locations yet
            foreach (var fss in this.FactorySuggestionsDictionary.Values)
            {
                foreach (var pucls in fss.locationStoreSpecificSequences.Values)
                {
                    if (!pucls.IsDormat())
                        return true;
                }
            }
            return false;
        }
    }
}
