using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Framework.Explorable;
using Microsoft.Pex.Engine.Explorable;
using Microsoft.Pex.Engine.ComponentModel;
using PexMe.FactoryRecommender;
using PexMe.Common;
using Microsoft.Pex.Engine.Packages;
using Microsoft.Pex.Framework.Packages;
using Microsoft.Pex.Engine.Logging;
using PexMe.Core;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Reasoning;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using PexMe.ObjectFactoryObserver;
using Microsoft.ExtendedReflection.Collections;
using PexMe.TermHandler;

namespace PexMe.Attribute
{
    /// <summary>
    /// Class that recommends factory methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class PexMeRecommenderAttribute
        : PexExecutionPackageAttributeBase, IPexExecutionPackage
    {
        IPexComponent host;
        PexMeDynamicDatabase pmd;
        TargetBranchAnalyzer tba;

        /// <summary>
        /// Invoked when a factory method is required
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        protected IEnumerable<IPexExplorableGuesser> CreateExplorableGuessers(IPexComponent host)
        {
            //host.Log.LogMessage(PexMeLogCategories.MethodBegin, "Begin of CreateExplorableGuessers method");            
            yield return new PexMeFactoryGuesser(host);
        }

        /// <summary>
        /// Gets invoked before execution
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        protected override object BeforeExecution(IPexComponent host)
        {
            this.host = host;
            //register all explorables
            foreach (IPexExplorableGuesser guesser in this.CreateExplorableGuessers(host))
            {
                host.Services.ExplorableGuesserManager.AddExplorableGuesser(guesser);
            }

            this.host.Log.ExplorableHandler += Log_ExplorableHandler;
            this.host.Log.ProblemHandler += Log_ProblemHandler;
            this.pmd = host.GetService<IPexMeDynamicDatabase>() as PexMeDynamicDatabase;
            
            //TargetBranch Handler cannot be instantiated with ExplorationServices from here if TERM_SOLVER
            //functionality is required
            if(!PexMeConstants.USE_TERM_SOLVER)
                this.tba = new TargetBranchAnalyzer(this.pmd, this.host.Services, null);
                        
            return null;
        }

        /// <summary>
        /// TODO:
        /// </summary>
        /// <param name="e"></param>
        void Log_ExplorableHandler(PexExplorableEventArgs e)
        {
            //this.host.Log.LogMessage(PexMeLogCategories.MethodBegin, "Entered method  Log_ExplorableHandler");
            //this.host.Log.LogMessage(PexMeLogCategories.Debug, "Requested for type " + e.ExplorableType);

            this.pmd.AddControllableType(e.ExplorableType.FullName);

            //var objectIssueDictionary = Host.GetService<IssueTrackDatabase>().ObjectCreationIssueDictionary;

            //if (!objectIssueDictionary.ContainsKey(e.Kind))
            //{
            //    objectIssueDictionary.Add(e.Kind, new SafeSet<TypeName>());
            //}

            //var set = objectIssueDictionary[e.Kind];
            //set.Add(e.ExplorableType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        void Log_ProblemHandler(Microsoft.ExtendedReflection.Logging.ProblemEventArgs e)
        {            
            //TODO: Xusheng's code for additional OCI issues
            if (e.Result == TryGetModelResult.Success)
                return;
                        
            CodeLocation location = e.FlippedLocation;
            var term = e.Suffix;

            SafeDictionary<Field, FieldValueHolder> fieldValues;
            SafeList<TypeEx> allFieldTypes;
            SafeList<Field> fields = TargetBranchAnalyzer.GetInvolvedFields(this.host, e.TermManager, term, out fieldValues, out allFieldTypes);
            
            //Not an object creation issue
            if (fields == null || fields.Count == 0)
                return;
            this.host.Log.LogMessage("ProblemHandler", "Recorded an issue at code location " + location.ToString());
            if (!PexMeConstants.USE_TERM_SOLVER)
            {
                //A heuristic to choose the explorable type             
                this.tba.HandleTargetBranch(location, term, e.TermManager, allFieldTypes[0]);
            }
        }
    }
}
