using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using System.ComponentModel;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using PexMe.Core;
using PexMe.PersistentStore;
using PexMe.Common;
using Microsoft.ExtendedReflection.Coverage;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Utilities;
using PexMe.ObjectFactoryObserver;
using System.IO;

namespace PexMe.FactoryRecommender
{
    /// <summary>
    /// A post processor that guesses factory methods for each non-covered location involving fields
    /// </summary>
    internal class PexMePostProcessor
        : PexComponentElementBase
    {
        IPexComponent host;
        PexMeDynamicDatabase pmd;
        PexMeStaticDatabase psd;
        PexMeDumpWriter pdw;
        AssemblyEx currAssembly;
        
        /// <summary>
        /// Stores the current nesting depth. Set to -1 if the environment 
        /// variable is not set. Usually happens when Pex is not launched with our
        /// external Perl script.
        /// </summary>
        int ndepth = -1;

        public PexMePostProcessor(IPexComponent host)
            : base(host)
        {
            this.host = host;
            this.pmd = host.GetService<IPexMeDynamicDatabase>() as PexMeDynamicDatabase;
            this.psd = host.GetService<IPexMeStaticDatabase>() as PexMeStaticDatabase;
            this.pdw = new PexMeDumpWriter(host);
            this.currAssembly = this.pmd.Services.CurrentAssembly.Assembly.Assembly;
            var nestingdepth = System.Environment.GetEnvironmentVariable("PEXME_NESTED_DEPTH");
            if (nestingdepth != null)
                ndepth = Convert.ToInt32(nestingdepth, 10);
        }

        /// <summary>
        /// Gets invoked after the execution of the entire pex process
        /// </summary>
        public void AfterExecution()
        {
            try
            {
                //this.host.Log.LogMessage(PexMeLogCategories.MethodBegin, "Begin of PexMePostProcessor.AfterExecution() method");

                DirectoryHelper.CheckExistsOrCreate(PexMeConstants.PexMeStorageDirectory);

                //Filter covered locations from uncovered locations
                this.pmd.FilterOutCoveredCodeLocations();
                var currPUTSignature = MethodOrFieldAnalyzer.GetMethodSignature(this.pmd.CurrentPUTMethod);

                //Suggest factory methods
                SafeSet<Method> allSuggestedNewPUTs;
                bool bHasSomeCoveredLocation, bAllAreNewLocations, bNoneAreNewLocations;
                this.GuessFactorymethods(out allSuggestedNewPUTs, currPUTSignature, out bHasSomeCoveredLocation, out bAllAreNewLocations, out bNoneAreNewLocations);
                                               
                var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.ReExecutionStatusFile);
                using (StreamWriter sw = new StreamWriter(filename))
                {
                    //nestingdepth == null represents that this is not run from the Perl 
                    //script. So, ignoring the generation of alternative PUTs.
                    if (allSuggestedNewPUTs.Count == 0 || this.ndepth == -1
                        || this.ndepth >= PexMeConstants.MAX_ALLOWED_NESTED_DEPTH)
                    {
                        this.pmd.PendingExplorationMethods.Remove(currPUTSignature);

                        //Decide to re-execute or stop. So update all sequences
                        //within uncovered locations with new suggestions and prepare for re-execute
                        this.UpdateUncoveredLocationsWithNewSuggestions(currPUTSignature);

                        //Check whether there is a need to further continue based on requirement
                        //in uncovered locations in factory suggestion store. Used to communicate to outside
                        //process for re-execution 
                        if (this.pmd.NeedReExecute(bHasSomeCoveredLocation, bAllAreNewLocations, bNoneAreNewLocations))
                        {
                            sw.WriteLine("REEXECUTE");                            
                        }
                        else
                        {
                            sw.WriteLine("STOP");

                            //Marks a successful exploration of this PUT                            
                            this.pmd.AllExploredMethods.Add(currPUTSignature);

                            //Remove all PUT specific sequences from the permanent store
                            this.EndOfPUTCleanUp(currPUTSignature);                            
                        }
                    }
                    else
                    {
                        //Set up new targets for additional explorations which are required for achieving
                        //the current target.
                        var newCommandFile = "PexMe." + System.Environment.TickCount + ".txt";
                        this.GenerateNestedCommandFile(allSuggestedNewPUTs, newCommandFile);
                        sw.WriteLine("NESTEDEXECUTE");
                        sw.WriteLine(newCommandFile);

                        //Putting all current information on uncovered locations to dormant
                        //stage. Will be re-activated when required and when this PUT is again
                        //brought to action.
                        var currPUT = MethodOrFieldAnalyzer.GetMethodSignature(this.pmd.CurrentPUTMethod);
                        this.SendToDormant(currPUT);
                    }
                }

                this.pdw.DumpDynamicDatabase(this.pmd as PexMeDynamicDatabase);
                this.pdw.DumpStaticDatabase(this.psd as PexMeStaticDatabase);
            }
            catch (Exception ex)
            {
                this.host.Log.LogCriticalFromException(ex, WikiTopics.MissingWikiTopic,
                    "postprocessor", ex.StackTrace);
            }
        }

        /// <summary>
        /// Removes all this PUT specific sequences from the permanent store.,
        /// </summary>
        private void EndOfPUTCleanUp(string putsignature)
        {         
            foreach (var fss in this.pmd.FactorySuggestionsDictionary.Values)
            {
                fss.EndOfPUTCleanUp(putsignature);
            }
        }

        /// <summary>
        /// Updates all sequences in uncovered location stores with new suggestions that
        /// are also stored within the PUT itself
        /// </summary>
        private void UpdateUncoveredLocationsWithNewSuggestions(string currPUTSignature)
        {
            //Iterate through each uncovered location
            foreach (var ucovLocList in this.pmd.UncoveredLocationDictionary.Values)
            {
                //TODO: Classify the uncovered locations into different groups
                //Because, it can happen that a single code location can have multiple terms which 
                //cannot be merged together.               
                if (ucovLocList.StoreList.Count == 0)
                    continue;

                //Check whether this location is earlier attempted and is failed. no
                //need to try this location again
                FactorySuggestionStore fss;
                if (!this.pmd.FactorySuggestionsDictionary.TryGetValue(ucovLocList.ExplorableType, out fss))
                {
                    this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "UncoveredLocations",
                        "Failed to retrieve factory suggestion store for explorable type " + ucovLocList.ExplorableType);
                    continue;
                }

                MethodSignatureSequenceList putspecificlist;
                if (!fss.FinalPUTSequences.TryGetValue(currPUTSignature, out putspecificlist))
                    putspecificlist = new MethodSignatureSequenceList();
                
                var ucovLoc = ucovLocList.StoreList[0];
                //If a suggested method is not yet explored, add it to all new suggested method
                if (ucovLoc.SuggestedMethodSetforFactory != null)
                {
                    //Prepare new sequences with the suggested method
                    var newmssl = new MethodSignatureSequenceList();
                    foreach (var suggestedm in ucovLoc.SuggestedMethodSetforFactory)
                    {
                        //Get all the pre-requisite sequences for this suggested method
                        var suggestedmsig = MethodOrFieldAnalyzer.GetMethodSignature(suggestedm);
                        MethodSignatureSequenceList mssl;
                        fss.FinalSuggestedMethodSequences.TryGetValue(suggestedmsig, out mssl);                                                
                        if (mssl == null || mssl.SequenceList.Count == 0)
                        {
                            var tempmss = new MethodSignatureSequence();                            
                            tempmss.Sequence.Add(suggestedmsig);
                            newmssl.SequenceList.Add(tempmss);
                        }
                        else
                        {
                            //Make a new sequence from the suggested method and its pre-requisite
                            foreach (var seq in mssl.SequenceList)
                            {
                                var tempmss = new MethodSignatureSequence();
                                tempmss.Sequence.AddRange(seq.Sequence);
                                tempmss.Sequence.Add(suggestedmsig);
                                newmssl.SequenceList.Add(tempmss);
                            }
                        }               
                    }

                    //Add all these suggestions to the uncovered location store itself
                    bool bNewlyCreated = false;
                    var pucls = fss.GetPersistentLocationstore(ucovLoc, out bNewlyCreated);
                    pucls.UpdateSuggestedMethodSequences(newmssl, putspecificlist);

                    //Check whether any looping is required. If a method
                    //is repeated more than 3 times, in a sequence, we consider
                    //it as a looping requirement. This is only a heuristic based.
                    if (!PexMeConstants.IGNORE_LOOP_FEATURE && !pucls.LoopingFeatureApplied)
                    {
                        this.CheckNEnhanceForLooping(pucls, pucls.Fitnessvalue, 
                            ucovLoc.DesiredFieldModificationType, ucovLoc.AllFields);
                    }

                    //Check for the number of sequences in this pucls. If they
                    //exceed the limit, delete the sequences
                    if ((pucls.SuggestedMethodSequences.Count - fss.FinalSuggestedMethodSequences.Values.Count)
                        > PexMeConstants.MAX_ALLOWED_SEQUENCES)
                    {
                        fss.RemoveUncoveredLocationStore(pucls, false, this.pmd);
                        var key = UncoveredCodeLocationStore.GetKey(pucls.CodeLocation, pucls.ExplorableType, pucls.TermIndex);
                        fss.PermanentFailedUncoveredLocations.Add(key);
                        fss.TemporaryFailedUncoveredLocations.Add(key, PexMeConstants.MAX_UNCOVEREDLOC_ATTEMPTS);
                        this.pmd.Log.LogWarning(WikiTopics.MissingWikiTopic, "uncoveredlocation",
                            @"Sequences for uncovered location " + pucls.CodeLocation +
                            "crossed the threshold " + PexMeConstants.MAX_ALLOWED_SEQUENCES + ", Deleted forever");
                    }
                }
            }

            //Add default sequences to the newly created factory suggestion stores
            foreach (var ucovLocList in this.pmd.UncoveredLocationDictionary.Values)
            {
                if (ucovLocList.StoreList.Count == 0)
                    continue;

                FactorySuggestionStore fss;
                if (!this.pmd.FactorySuggestionsDictionary.TryGetValue(ucovLocList.ExplorableType, out fss))
                    continue;
                
                var ucovLoc = ucovLocList.StoreList[0];
                if (fss.BCreatedNow)
                {
                    fss.BCreatedNow = false;
                    var mss = new MethodSignatureSequence();
                    var explorableType = ucovLocList.ExplorableType;
                    foreach (var methodinucov in ucovLoc.MethodCallSequence.Sequence)
                    {
                        if (methodinucov.Contains("..ctor(")) //Don't add constructors
                            continue;
                        if (!methodinucov.Contains(explorableType)) //Ignore the method calls from other types
                            continue;
                        mss.Sequence.Add(methodinucov);
                    }
                    fss.AddDefaultSequence(this.pmd, mss);
                }
            }
        }

        /// <summary>
        /// Checks whether there are any looping requireements based on looping threshold and enhances 
        /// the sequence accordingly. Applies only for INCREMENT and DECREMENT field modification types
        /// </summary>
        /// <param name="pucls"></param>
        /// <param name="p"></param>
        private void CheckNEnhanceForLooping(PersistentUncoveredLocationStore pucls, 
            int fitnessval, FieldModificationType fmt, SafeList<Field> culpritFields)
        {
            if (fmt != FieldModificationType.INCREMENT && fmt != FieldModificationType.DECREMENT)
                return;

            //Not even a single execution happened on this location.
            if (fitnessval == Int32.MaxValue)
                return;

            var field = culpritFields[0];
            FieldStore fs;
            if (!this.pmd.FieldDictionary.TryGetValue(field, out fs))
                return;

            Dictionary<string, Method> writeMethods = new Dictionary<string, Method>();
            foreach (var mset in fs.WriteMethods.Values)
            {
                foreach (var m in mset)
                {
                    var sig = MethodOrFieldAnalyzer.GetMethodSignature(m);
                    writeMethods.Add(sig, m);
                }
            }

            foreach (var seq in pucls.SuggestedMethodSequences)
            {
                string loop_method;
                if (!this.IsEligibleForLooping(seq, field, writeMethods, fs, out loop_method))
                    continue;

                pucls.LoopingFeatureApplied = true;
                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "LoopingFeature",
                    "Applying looping feature on method: " + loop_method + " (" + (fitnessval - 1) + ")");
                for (int count = 1; count < fitnessval; count++)
                    seq.Sequence.Add(loop_method);
            }
        }

        private bool IsEligibleForLooping(MethodSignatureSequence seq, Field field, 
            Dictionary<string, Method> writeMethods, FieldStore fs, out string loop_method)
        {          
            string prevMethod = "";
            int numTimes = 1;
            foreach (var method in seq.Sequence)
            {
                if (method == prevMethod)
                    numTimes++;
                else
                    numTimes = 1;
                
                if (numTimes >= PexMeConstants.LOOP_THRESHOLD)
                {     
                    //Check whether this loop_method modifies the culprit field
                    //exactly by ONE (We current handle only INCREMENT_BY_ONE CASE)             
                    if (!writeMethods.ContainsKey(method))
                        continue;

                    var wm = writeMethods[method];
                    FieldModificationType fmt;
                    if (!fs.PreciseModificationTypeDictionary.TryGetValue(wm, out fmt))
                        continue;

                    if (fmt != FieldModificationType.INCREMENT_ONE)
                        continue;
                    loop_method = method;
                    return true;
                }

                prevMethod = method;
            }

            loop_method = "";
            return false;
        }


        /// <summary>
        /// Sends all the current information in factory suggestion store to dormant stage
        /// </summary>
        /// <param name="currPUT"></param>
        private void SendToDormant(string currPUTSignature)
        {
            foreach(var fss in this.pmd.FactorySuggestionsDictionary.Values)
            {
                foreach (var pucls in fss.locationStoreSpecificSequences.Values)
                {
                    if(!pucls.IsDormat())
                        pucls.SendToDormant(currPUTSignature);
                }
            }
        }

        /// <summary>
        /// Generates nested command files. Ignores if there are any PUTs in 
        /// pmd.PendingExplorationMethods since those methods can cause cycles.
        /// </summary>
        /// <param name="allSuggestedNewPUTs"></param>
        /// <param name="cmdfile"></param>
        private void GenerateNestedCommandFile(SafeSet<Method> allSuggestedNewPUTs, string cmdfile)
        {
            using (StreamWriter sw = new StreamWriter(cmdfile))
            {
                foreach (var newput in allSuggestedNewPUTs)
                {
                    var newputsig = MethodOrFieldAnalyzer.GetMethodSignature(newput);                  
                    sw.WriteLine(PUTGenerator.PUTGenerator.GeneratePUTCommand(newput));
                }
            }
        }

        /// <summary>
        /// Helps guess factory methods for each uncovered condition
        /// </summary>
        private void GuessFactorymethods(out SafeSet<Method> allNewSuggestedPUTs, string currPUTSignature, out bool bHasSomeCoveredLocation,
            out bool bAllAreNewLocations, out bool bNoneAreNewLocations)
        {
            PexMeFactoryGuesser pfg = new PexMeFactoryGuesser(this.host);

            HashSet<string> allGivenUpLocations, allCoveredLocations, newUnCoveredLocations;            
            //Analyze the current uncovered and previously uncovered locations
            this.pmd.AnalyzePreviousAndCurrentUncoveredLoc(currPUTSignature, out allGivenUpLocations,
                out allCoveredLocations, out newUnCoveredLocations, out bHasSomeCoveredLocation, out bAllAreNewLocations, out bNoneAreNewLocations);

            allNewSuggestedPUTs = new SafeSet<Method>();
            //Iterate through each uncovered location
            foreach (var ucovLocList in pmd.UncoveredLocationDictionary.Values)
            {
                //TODO: Classify the uncovered locations into different groups
                //Because, it can happen that a single code location can have multiple terms which 
                //cannot be merged together.               
                if (ucovLocList.StoreList.Count == 0)
                    continue;

                //Check whether this location is earlier attempted and is failed. no
                //need to try this location again
                FactorySuggestionStore fss = null;
                string key = null;
                if (this.pmd.FactorySuggestionsDictionary.TryGetValue(ucovLocList.ExplorableType, out fss))
                {
                    key = UncoveredCodeLocationStore.GetKey(ucovLocList.Location.ToString(),
                        ucovLocList.ExplorableType, ucovLocList.TermIndex);

                    //A fix to keep track of uncovered locations in system libraries
                    if (TargetBranchAnalyzer.IsUncoveredLocationInSystemLib(ucovLocList.Location))
                    {
                        fss.UncoveredSystemLibLocations.Add(key);
                    }
                    
                    if (allGivenUpLocations.Contains(key))
                        continue;
                    //Already covered locations can be reported again due to different PUTs or different call sites
                    //if (allCoveredLocations.Contains(key))
                    //    continue;
                    if (fss.PermanentFailedUncoveredLocations.Contains(key))
                        continue;
                    //if (fss.SuccessfulCoveredLocations.Contains(key))
                    //    continue;
                }          

                //A single element of ucovLoc is sufficient here
                var ucovLoc = ucovLocList.StoreList[0];                              
                if (!pfg.TryInferFactoryMethod(ucovLoc, out ucovLoc.SuggestedMethodSetforFactory))
                {
                    this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "postprocessor",
                        "Failed to suggest factory methods for uncovered location " + ucovLoc.ToString() + " -> Adding to permanent failed locations");

                    if (fss != null && key != null)
                        fss.PermanentFailedUncoveredLocations.Add(key);

                    continue;
                }

                //If a suggested method is not yet explored, add it to all new suggested method
                if (ucovLoc.SuggestedMethodSetforFactory != null)
                {
                    foreach (var suggestedm in ucovLoc.SuggestedMethodSetforFactory)
                    {
                        if (suggestedm.IsConstructor)
                            continue;

                        //Check if this is ever explored by this process by getting associated PUT                        
                        Method pexmethod;
                        bool bretval = PUTGenerator.PUTGenerator.TryRetrievePUT(this.pmd, this.currAssembly, suggestedm, out pexmethod);
                        if (!bretval)
                        {
                            //The suggested method is out of scope of current library and
                            //no need to explore it explicitly
                            continue;
                        }

                        //Ignore self suggestions
                        if (pexmethod == this.pmd.CurrentPUTMethod)
                            continue;
                        var signature = MethodOrFieldAnalyzer.GetMethodSignature(pexmethod);
                        if (this.pmd.AllExploredMethods.Contains(signature))
                            continue;

                        if (this.pmd.PendingExplorationMethods.Contains(signature))
                        {
                            this.host.Log.LogWarning(WikiTopics.MissingWikiTopic,
                                "Nested PUTs", "Ignoring the nested PUT due to cycle detection " + signature);
                            continue;
                        }

                        allNewSuggestedPUTs.Add(pexmethod);
                    }
                }
            }         
        }
    }
}
