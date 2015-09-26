using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PexMe.Core;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Logging;
using PexMe.Common;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Metadata;

namespace PexMe.ObjectFactoryObserver
{
    /// <summary>
    /// A temporary store for determining a sequence for a single uncovered store
    /// </summary>
    [Serializable]
    public class PersistentUncoveredLocationStore
    {
        /// <summary>
        /// Fitness value
        /// </summary>
        public int Fitnessvalue = Int32.MaxValue;

        /// <summary>
        /// Stores the number of unsuccessful attempts without any improvement in the fitness value
        /// </summary>
        public int NumberOfUnsuccessfulAttempts;

        public string CodeLocation;
        public string MethodSignature;  //Method to which the code location belongs to
        public int Offset;              //Offset of the code location
        public string ExplorableType;   //Used for the key

        public bool AlreadyCovered = false;
        public MethodSignatureSequence HitSequence; //Stores the sequences that is the best
        //when this location is actually covered.

        /// <summary>
        /// Looping feature can be applied only once to a pucls.
        /// </summary>
        public bool LoopingFeatureApplied = false;

        FactorySuggestionStore parentfss;
        public string declaringTypeStr;
        public string AssemblyShortName;
        public int TermIndex;

        public PersistentUncoveredLocationStore(CodeLocation cl,
            TypeEx explorableType, int termIndex, int fitnessvalue, FactorySuggestionStore fss)
        {
            this.CodeLocation = cl.ToString();
            this.MethodSignature = MethodOrFieldAnalyzer.GetMethodSignature(cl.Method);
            TypeDefinition declaringType;
            if (!cl.Method.TryGetDeclaringType(out declaringType))
            {
                //TODO:Error
            }

            this.ExplorableType = explorableType.ToString();
            this.Offset = cl.Offset;
            this.AssemblyShortName = declaringType.Module.Assembly.Location;
            this.declaringTypeStr = declaringType.ToString();
            this.Fitnessvalue = fitnessvalue;
            this.TermIndex = termIndex;
            this.parentfss = fss;
        }

        /// <summary>
        /// Suggested method sequences for this store
        /// </summary>
        public List<MethodSignatureSequence> SuggestedMethodSequences = new List<MethodSignatureSequence>();

        /// <summary>
        /// A flag that indicates whether this persistent store is kept to dormant
        /// This happens when a nested PUT is invoked and the parent PUT is temporarily halted.
        /// </summary>
        private bool bDormant = false;
        public bool IsDormat()
        {
            return bDormant;
        }

        private string associatedPUTName = "";
        public string AssociatedPUTName
        {
            get
            {
                return this.associatedPUTName;
            }
        }

        internal MethodSignatureSequenceList putSeqSnapshot;

        /// <summary>
        /// Moves this uncovered location store to dormant state
        /// </summary>
        /// <param name="putname"></param>
        public void SendToDormant(string putname)
        {
            bDormant = true;
            this.associatedPUTName = putname;

            //Save a snapshot of final sequences of PUT here. no need to worry about result
            //if it does not exist.
            this.parentfss.FinalPUTSequences.TryGetValue(putname, out putSeqSnapshot);
        }

        /// <summary>
        /// Re-activates this PUT;
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public bool ActivateFromDormant(string putname)
        {
            if (this.associatedPUTName == putname)
            {
                bDormant = false;
                var newSequenceList = new List<MethodSignatureSequence>();

                //Re-activate based on the snapshot of sequences taken. 
                MethodSignatureSequenceList mssl;
                if (!this.parentfss.FinalPUTSequences.TryGetValue(putname, out mssl))
                    return true;

                if (this.putSeqSnapshot == null)
                    this.putSeqSnapshot = new MethodSignatureSequenceList();

                foreach (var seq in mssl.SequenceList)
                {
                    if (this.putSeqSnapshot.SequenceList.Contains(seq))
                        continue;

                    //A new sequence is detected after this uncovered location store went to dormant stage
                    MethodSignatureSequence newMs = new MethodSignatureSequence();
                    newMs.Sequence.AddRange(seq.Sequence);
                    this.SuggestedMethodSequences.Add(newMs);
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Takes a new sequence list and updates the existing sequence list
        /// with the new list
        /// </summary>
        /// <param name="mssl"></param>
        public void UpdateSuggestedMethodSequences(MethodSignatureSequenceList mssl, MethodSignatureSequenceList putSpecificList)
        {
            var newSuggesedMethodSequences = new List<MethodSignatureSequence>();

            if (this.SuggestedMethodSequences.Count == 0)
            {
                //A fresh location.
                //Make up new sequences with the known list
                if (putSpecificList.SequenceList.Count == 0)
                {
                    foreach (var suggestedm in mssl.SequenceList)
                    {
                        MethodSignatureSequence newMS = new MethodSignatureSequence();
                        newMS.Sequence.AddRange(suggestedm.Sequence);
                        newSuggesedMethodSequences.Add(newMS);
                    }
                }
                else
                {
                    foreach (var pseq in putSpecificList.SequenceList)
                    {
                        foreach (var suggestedm in mssl.SequenceList)
                        {
                            MethodSignatureSequence newMS = new MethodSignatureSequence();
                            newMS.Sequence.AddRange(pseq.Sequence);
                            foreach (var method in suggestedm.Sequence)
                            {
                                if(!method.Contains("..ctor("))
                                    newMS.Sequence.Add(method);
                            }                
                                
                            if(!newSuggesedMethodSequences.Contains(newMS))
                                newSuggesedMethodSequences.Add(newMS);
                        }
                    }
                }
            }
            else
            {
                //Once the looping feature is applied, there is no point in updating the sequences
                if (!this.LoopingFeatureApplied)
                {
                    //Merge and make up the new list
                    foreach (var pseq in this.SuggestedMethodSequences)
                    {
                        foreach (var suggestedm in mssl.SequenceList)
                        {
                            MethodSignatureSequence newMS = new MethodSignatureSequence();
                            newMS.Sequence.AddRange(pseq.Sequence);                            
                            foreach (var method in suggestedm.Sequence)
                            {
                                if (!method.Contains("..ctor("))
                                    newMS.Sequence.Add(method);
                            }  

                            if(!newSuggesedMethodSequences.Contains(newMS))
                                newSuggesedMethodSequences.Add(newMS);
                        }
                    }
                }
                else
                    newSuggesedMethodSequences = this.SuggestedMethodSequences;
            }

            this.SuggestedMethodSequences = newSuggesedMethodSequences;
        }
    }
}
