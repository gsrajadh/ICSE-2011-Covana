using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using System.Collections;
using PexMe.Core;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Logging;
using PexMe.Common;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;

namespace PexMe.ObjectFactoryObserver
{   
    /// <summary>
    /// includes suggestions from the previous run for each type for a particular code location
    /// only class that is serialized among all other stores and used by the next runs
    /// </summary>
    [Serializable]
    public class FactorySuggestionStore
    {
        /// <summary>
        /// associated type
        /// </summary>
        public string DeclaringType = null;

        /// <summary>
        /// Stores all sequences that serve as pre-requisites for a particular method.  
        /// These are stored with respect to a method.
        /// </summary>        
        public Dictionary<string, MethodSignatureSequenceList> FinalSuggestedMethodSequences 
            = new Dictionary<string, MethodSignatureSequenceList>();

        /// <summary>
        /// Stores all PUT specific sequences. Stores all sequences
        /// that ever helped in exploring a PUT. Gets cleaned when a PUT is ended. These are
        /// independent of any uncovered location.
        /// </summary>        
        public Dictionary<string, MethodSignatureSequenceList> FinalPUTSequences
            = new Dictionary<string, MethodSignatureSequenceList>();

        /// <summary>
        /// Sequences that helped detecting defects at some point of time
        /// </summary>        
        public List<MethodSignatureSequence> DefectDetectingSequences = new List<MethodSignatureSequence>();

        /// <summary>
        /// Main store that stores information about sequences for each uncovered location
        /// </summary>
        public Dictionary<string, PersistentUncoveredLocationStore> locationStoreSpecificSequences =
            new Dictionary<string, PersistentUncoveredLocationStore>();

        /// <summary>
        /// Main store that stores information about sequences those  uncovered locations
        /// that are being failed by our approach after the number of unsuccessful attempts
        /// These locations need not be tried again while dealing with a PUT.
        /// </summary>
        public HashSet<string> PermanentFailedUncoveredLocations = new HashSet<string>();

        /// <summary>
        /// Only a temporary version of previous permanent uncovered locations. Will be used
        /// mainly to retry a failed uncovered location. This can be ignored if a perfect
        /// implementation of chaining approach is available.
        /// </summary>
        public Dictionary<string, int> TemporaryFailedUncoveredLocations
            = new Dictionary<string, int>();

        /// <summary>
        /// Stores the uncovered locations in system libraries. Used to prevent
        /// the resurrection of system libraries.
        /// </summary>
        public HashSet<string> UncoveredSystemLibLocations = new HashSet<string>();

        /// <summary>
        /// Main store that stores information all uncovered locations that are successfully covered
        /// by our approach.
        /// </summary>
        public HashSet<string> SuccessfulCoveredLocations = new HashSet<string>();

        /// <summary>
        /// Represents when factory suggestion store is created.
        /// </summary>
        public bool BCreatedNow = true;
                                
        /// <summary>
        /// Gets the entire list of suggested methods
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MethodSignatureSequence> GetSuggestedMethodSequences(PexMeDynamicDatabase pmd)
        { 
            var uniqueSequenceList = new List<MethodSignatureSequence>();           

            //Gather all sequences among all location stores. Detect the unique
            //sequences among them and suggest the complete unique sequences            
            foreach (var pucls in this.locationStoreSpecificSequences.Values)
            {
                if (pucls.IsDormat())
                    continue;
                
                foreach (var ms in pucls.SuggestedMethodSequences)
                {
                    if (ms.Sequence.Count == 0)
                        continue;

                    if (!uniqueSequenceList.Contains(ms))
                        uniqueSequenceList.Add(ms);
                }
            }

            //Return all final sequence ever collected to help cover more
            //at the first time itself. Along with the final suggested method,
            //we also need to add the method itself
            foreach (var methodid in this.FinalSuggestedMethodSequences.Keys)
            {
                var seqlist = this.FinalSuggestedMethodSequences[methodid];
                foreach (var seq in seqlist.SequenceList)
                {                   
                    MethodSignatureSequence tempseq = new MethodSignatureSequence();
                    tempseq.Sequence.AddRange(seq.Sequence);
                    tempseq.Sequence.Add(methodid);

                    if (!uniqueSequenceList.Contains(tempseq))
                        uniqueSequenceList.Add(tempseq);
                }
            }

            foreach (var seqlist in this.FinalSuggestedMethodSequences.Values)
            {
                foreach(var seq in seqlist.SequenceList)
                {
                    if (seq.Sequence.Count == 0)
                        continue;

                    if (!uniqueSequenceList.Contains(seq))
                        uniqueSequenceList.Add(seq);
                }
            }
            
            //Return all previously collected sequences for this PUT, if there exist
            //no sequences specific to any uncovered location yet.
            var putsignature = MethodOrFieldAnalyzer.GetMethodSignature(pmd.CurrentPUTMethod);
            MethodSignatureSequenceList mssl;
            if (this.FinalPUTSequences.TryGetValue(putsignature, out mssl))
            {
                foreach (var seq in mssl.SequenceList)
                {
                    if (seq.Sequence.Count == 0)
                        continue;

                    if (!uniqueSequenceList.Contains(seq))
                        uniqueSequenceList.Add(seq);
                }
            }
                     
            foreach (var ms in uniqueSequenceList)
                yield return ms;
        }

        /// <summary>
        /// Gets a persistent location store version of an uncovered location store
        /// </summary>
        /// <param name="ucls"></param>
        /// <returns></returns>
        public PersistentUncoveredLocationStore GetPersistentLocationstore(UncoveredCodeLocationStore ucls,
            out bool bNewlyCreated)
        {
            PersistentUncoveredLocationStore pucls;
            bNewlyCreated = false;
            var key = UncoveredCodeLocationStore.GetKey(ucls.Location.ToString(), ucls.ExplorableType.ToString(), ucls.TermIndex);
            if (!locationStoreSpecificSequences.TryGetValue(key, out pucls))
            {
                pucls = new PersistentUncoveredLocationStore(ucls.Location, ucls.ExplorableType,
                    ucls.TermIndex, ucls.Fitnessvalue, this);
                pucls.CodeLocation = ucls.Location.ToString();
                locationStoreSpecificSequences[key] = pucls;
                bNewlyCreated = true;
            }
            return pucls;
        }

        /// <summary>
        /// Adds a method signature sequence to defect detecting sequences.
        /// </summary>
        /// <param name="mss"></param>
        public void AddToDefectDetectingSequences(MethodSignatureSequence mss)
        {
            if (!this.DefectDetectingSequences.Contains(mss))
                this.DefectDetectingSequences.Add(mss);
        }

        /// <summary>
        /// Adds a default method-call sequence that represents the first execution
        /// of the PUT
        /// </summary>
        internal void AddDefaultSequence(PexMeDynamicDatabase pmd, MethodSignatureSequence defaultSeq)
        {
            //Get the method associated with the current exploring PUT
            string methodcallname = PexMeConstants.DEFAULT_FINAL_SUGGESTION_STORE;
            Method assocMethod;
            if (PUTGenerator.PUTGenerator.TryRetrieveMethodCall(pmd.CurrentPUTMethod, out assocMethod))
                methodcallname = MethodOrFieldAnalyzer.GetMethodSignature(assocMethod);

            //Get PUT independent sequence list
            MethodSignatureSequenceList putIndependentMssl;
            if (!this.FinalSuggestedMethodSequences.TryGetValue(methodcallname, out putIndependentMssl))
            {
                putIndependentMssl = new MethodSignatureSequenceList();
                this.FinalSuggestedMethodSequences.Add(methodcallname, putIndependentMssl);
                putIndependentMssl.Add(defaultSeq);
            }

            //Also update the PUT specific sequences. These gets cleared once a 
            //PUT is completely explored.
            var putsignature = MethodOrFieldAnalyzer.GetMethodSignature(pmd.CurrentPUTMethod);
            MethodSignatureSequenceList putSpecificMssl;
            if (!this.FinalPUTSequences.TryGetValue(putsignature, out putSpecificMssl))
            {
                putSpecificMssl = new MethodSignatureSequenceList();
                this.FinalPUTSequences.Add(putsignature, putSpecificMssl);
                putSpecificMssl.Add(defaultSeq);
            }
        }

        /// <summary>
        /// Removes the uncovered location store. Mainly keeps the sequence
        /// that helped to cover the target location and drops all others       
        /// </summary>
        /// <param name="pucls"></param>
        /// <param name="successful">Helps to distinguish between a removal during success and failure</param>
        internal void RemoveUncoveredLocationStore(PersistentUncoveredLocationStore pucls, 
            bool successful, PexMeDynamicDatabase pmd)
        {
            var key = UncoveredCodeLocationStore.GetKey(pucls.CodeLocation, pucls.ExplorableType, pucls.TermIndex);                
            this.locationStoreSpecificSequences.Remove(key);
            if (!successful)
            {                
                return;
            }

            this.SuccessfulCoveredLocations.Add(key);
            this.PermanentFailedUncoveredLocations.Remove(key);
            this.TemporaryFailedUncoveredLocations.Remove(key);
            this.UncoveredSystemLibLocations.Remove(key);

            //Get the method associated with the current exploring PUT
            string methodcallname = PexMeConstants.DEFAULT_FINAL_SUGGESTION_STORE;
            Method assocMethod;
            if (PUTGenerator.PUTGenerator.TryRetrieveMethodCall(pmd.CurrentPUTMethod, out assocMethod))
                methodcallname = MethodOrFieldAnalyzer.GetMethodSignature(assocMethod);

            //Get PUT independent sequence list
            MethodSignatureSequenceList putIndependentMssl;
            if (!this.FinalSuggestedMethodSequences.TryGetValue(methodcallname, out putIndependentMssl))
            {
                putIndependentMssl = new MethodSignatureSequenceList();
                this.FinalSuggestedMethodSequences.Add(methodcallname, putIndependentMssl);
            }

            //Also update the PUT specific sequences. These gets cleared once a 
            //PUT is completely explored.
            var putsignature = MethodOrFieldAnalyzer.GetMethodSignature(pmd.CurrentPUTMethod);
            MethodSignatureSequenceList putSpecificMssl;
            if (!this.FinalPUTSequences.TryGetValue(putsignature, out putSpecificMssl))
            {
                putSpecificMssl = new MethodSignatureSequenceList();
                this.FinalPUTSequences.Add(putsignature, putSpecificMssl);
            }

            //Any Persistent Uncovered location store that is successfully
            //covered gets a hit sequence
            SafeDebug.AssumeNotNull(pucls.HitSequence, "pucls.HitSequence");
            MethodSignatureSequence matchingseq;
            if (!FactorySuggestionStore.TryGetMatchingSequence(pucls.HitSequence,
                pucls.SuggestedMethodSequences, out matchingseq))
            {
                //Failed to retrieve the hit sequence. However, a heuristic
                //can be used where there is only one suggested sequence
                if (pucls.SuggestedMethodSequences.Count == 1)
                {
                    matchingseq = pucls.SuggestedMethodSequences[0];
                    putIndependentMssl.Add(matchingseq);
                    putSpecificMssl.Add(matchingseq);
                }
                else
                {
                    pmd.Log.LogWarning(WikiTopics.MissingWikiTopic, "SequenceMatch",
                        "Failed to retrieve a matching sequence for a hit sequence, adding complete hit sequence " + pucls.HitSequence);

                    var hitSubSequence = new MethodSignatureSequence();
                    foreach (var mhit in pucls.HitSequence.Sequence)
                    {
                        if (mhit.Contains("..ctor(")) //Don't add constructors
                            continue;
                        if (!mhit.Contains(this.DeclaringType)) //Ignore the method calls from other types
                            continue;
                        hitSubSequence.Sequence.Add(mhit);
                    }

                    //Add all sequences to final set of sequences for further usage.            
                    putIndependentMssl.Add(hitSubSequence);
                    putSpecificMssl.Add(hitSubSequence);
                    //this.UpgradeActiveULStores(putsignature, pucls.SuggestedMethodSequences);
                }
            }
            else
            {
                //Add all sequences to final set of sequences for further usage.            
                putIndependentMssl.Add(matchingseq);
                putSpecificMssl.Add(matchingseq);
                //this.UpgradeActiveULStores(putsignature, matchingseq);
            }
        }

        /// <summary>
        /// Upgrades other PersistentUncoveredLocationStores that are currently 
        /// active with the newly detected sequences.
        /// </summary>
        /// <param name="putsignature"></param>
        /// <param name="matchingseq"></param>
        private void UpgradeActiveULStores(string putsignature, MethodSignatureSequence matchingseq)
        {
            foreach (var pucls in this.locationStoreSpecificSequences.Values)
            {
                if (pucls.IsDormat())
                    continue;

                if(!pucls.SuggestedMethodSequences.Contains(matchingseq))
                    pucls.SuggestedMethodSequences.Add(matchingseq);
            }
        }

        /// <summary>
        /// Upgrades other PersistentUncoveredLocationStores that are currently 
        /// active with the newly detected sequences.
        /// </summary>
        /// <param name="putsignature"></param>
        /// <param name="list"></param>
        private void UpgradeActiveULStores(string putsignature, List<MethodSignatureSequence> list)
        {
            foreach (var seq in list)
                this.UpgradeActiveULStores(putsignature, seq);
        }


        /// <summary>
        /// Gets a MethodSignatureSequenceList for each putmethod. If one does not exist,
        /// creates a fresh one
        /// </summary>
        /// <param name="putmethod"></param>
        /// <returns></returns>
        public bool TryGetListFromFinalSuggestedSequences(Method putmethod, out MethodSignatureSequenceList mssl)
        {
            mssl = null;

            Method assocMethod;
            if (!PUTGenerator.PUTGenerator.TryRetrieveMethodCall(putmethod, out assocMethod))
                return false;

            var methodcallname = MethodOrFieldAnalyzer.GetMethodSignature(assocMethod);                       
            if (!this.FinalSuggestedMethodSequences.TryGetValue(methodcallname, out mssl))
            {
                mssl = new MethodSignatureSequenceList();
                this.FinalSuggestedMethodSequences.Add(methodcallname, mssl);
            }
            return true;
        }

        /// <summary>
        /// Cleans a factory suggestion store at the end of the PUT
        /// </summary>
        public void EndOfPUTCleanUp(string putsignature)
        {
            this.FinalPUTSequences.Remove(putsignature);
            //TODO: Make these things specific for the PUT later. Currently we delete all entires so that
            //the new PUT had no troubles
            //this.PermanentFailedUncoveredLocations.Clear();
            //this.SuccessfulCoveredLocations.Clear();
            //this.TemporaryFailedUncoveredLocations.Clear();
            //this.UncoveredSystemLibLocations.Clear();
        }

        /// <summary>
        /// Detects a matching sequence of "seq" in the sequence list. There should be an exact match
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="seqlist"></param>
        /// <returns></returns>
        internal static bool TryGetMatchingSequence(MethodSignatureSequence seq, List<MethodSignatureSequence> seqlist,
            out MethodSignatureSequence matchingseq)
        {       
            var matchingList = new Microsoft.ExtendedReflection.Collections.SafeList<MethodSignatureSequence>(); 
            matchingseq = null;
            foreach (var tseq in seqlist)
            {
                if (IsASubsequence(seq, tseq))
                    matchingList.Add(tseq);                    
            }

            if (matchingList.Count == 0)
                return false;
            else
            {
                matchingList.Sort();
                matchingseq = matchingList[0];
                return true;
            }
        }

        /// <summary>
        /// Checks whether seq2 is a subsequence of seq1. This function do not considers null sequences
        /// </summary>
        /// <param name="seq1"></param>
        /// <param name="seq2"></param>
        /// <returns></returns>
        internal static bool IsASubsequence(MethodSignatureSequence seq1, MethodSignatureSequence seq2)
        {
            if (seq1.Sequence.Count == 0 || seq2.Sequence.Count == 0)
                return false;
            
            IEnumerator<string> seq2iter = seq2.Sequence.GetEnumerator();
            seq2iter.MoveNext();
            var seq2elem = seq2iter.Current;
            foreach (var seq1elem in seq1.Sequence)
            {
                if (seq1elem == seq2elem)
                {
                    if (seq2iter.MoveNext())
                        seq2elem = seq2iter.Current;
                    else
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether seq2 is equal to seq1. This function do not considers null sequences
        /// </summary>
        /// <param name="seq1"></param>
        /// <param name="seq2"></param>
        /// <returns></returns>
        internal static bool AreEqualSequences(MethodSignatureSequence seq1, MethodSignatureSequence seq2)
        {
            if (seq1.Sequence.Count == 0 || seq2.Sequence.Count == 0 || seq1.Sequence.Count != seq2.Sequence.Count)
                return false;

            IEnumerator<string> seq2iter = seq2.Sequence.GetEnumerator();
            seq2iter.MoveNext();
            var seq2elem = seq2iter.Current;
            foreach (var seq1elem in seq1.Sequence)
            {
                if (seq1elem != seq2elem)
                    return false;
                
                seq2elem = seq2iter.Current;                
            }

            return true;
        }
    }
}
