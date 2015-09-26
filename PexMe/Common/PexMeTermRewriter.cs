using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;

namespace PexMe.Common
{
    internal class PexMeTermRewriter : TermInternalizingRewriter<TVoid>
    {
        SafeBag<TypeEx> types = new SafeBag<TypeEx>();
        public PexMeTermRewriter(TermManager termManager)
            : base(termManager, OnCollection.Fail)
        {
        }

        public override Term VisitObject(TVoid parameter, Term term, IObjectId id, ObjectPropertyCollection properties)
        {
            object value;
            if (this.TermManager.TryGetObject(term, out value))
                return base.VisitObject(parameter, term, id, properties);
            else
            {
                TypeEx type;
                if (!this.TermManager.TryGetObjectType(term, out type))
                    SafeDebug.Fail("cannot get object type");
                int index = types.Add(type);
                return this.TermManager.Object(new PexMeId(index), properties);
            }
        }

        public override Term VisitSelect(TVoid parameter, Term term, Term compound, Term index)
        {
            index = this.VisitTerm(parameter, index);
            compound = this.VisitTerm(parameter, compound);

            ISymbolId key;
            if (this.TermManager.TryGetSymbol(index, out key))
            {
                ISymbolIdFromParameter parameterKey = key as ISymbolIdFromParameter;
                if (parameterKey != null &&
                    parameterKey.Parameter.IsThis)
                {
                    Term baseCompound;
                    ITermMap updates;
                    if (this.TermManager.TryGetUpdate(compound, out baseCompound, out updates) &&
                        updates.AreKeysValues)
                        return this.VisitTerm(
                            parameter,
                            this.TermManager.Select(baseCompound, index));
                }
            }

            return this.TermManager.Select(compound, index);
        }
    }
}
