using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.PostAnalysis;

namespace Covana.ResultTrackingExtrator
{
    public class ResultTrackConditionExtractor : TermInternalizingRewriter<TVoid>
    {
        private Method method;
        private SafeSet<Parameter> parameters;
        private int callerOffset;
        private CodeLocation location;
        private IMethodSignature signature;
        public StringBuilder Log = new StringBuilder("term log: ");
        public bool foundSymbol = false;


        public ResultTrackConditionExtractor(TermManager termManager)
            : base(termManager, OnCollection.Fail)
        {
            this.parameters = new SafeSet<Parameter>();
        }

        public IMethodSignature Signature
        {
            get { return signature; }
        }

        public int CallerOffset
        {
            get { return callerOffset; }
        }

        public CodeLocation Location
        {
            get { return location; }
        }

        public Method Method
        {
            get { return this.method; }
        }

        public IEnumerable<Parameter> Parameters
        {
            get { return this.parameters; }
        }

        public override Term VisitSymbol(TVoid parameter, Term term, ISymbolId key)
        {
            //            ISymbolIdFromParameter fromParameter = key as ISymbolIdFromParameter;
            //            if (fromParameter != null &&
            //                fromParameter.Parameter != null)
            //            {
            //                Parameter p = fromParameter.Parameter;
            //                Method m = p.DeclaringMember as Method;
            //                if (m != null)
            //                {
            //                    this.method = m;
            //                    this.parameters.Add(p);
            //                }
            //            }
            Log.AppendLine("In Result track condition extrator: ");
            Log.AppendLine("ISymbolId is " + key.Description + " type: " + key.GetType());
            if (key.GetType().FullName.IndexOf("SymbolId")!=-1)
            {
                foundSymbol = true;
            }

//            Log.AppendLine("parameter is " + parameter.GetType());
            if (MetadataFromReflection.GetType(key.GetType()) ==
                MetadataFromReflection.GetType(typeof (PexTrackedResultId)))
            {
                var resultId = key as PexTrackedResultId;
                method = resultId.CallerMethod;
                location = resultId.CallerLocation;
                callerOffset = resultId.CallerOffset;
                signature = resultId.MethodSignature;
                foundSymbol = true;
            }

            if (MetadataFromReflection.GetType(key.GetType()) ==
                MetadataFromReflection.GetType(typeof(PexTrackedParameterId)))
            {
                var resultId = key as PexTrackedParameterId;
                Log.AppendLine("Parameter is " + resultId.Parameter + " description: " + resultId.Description);
                foundSymbol = true;
            }
            return base.VisitSymbol(parameter, term, key);
        }
    }
}