using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Interpretation;
using PexMe.Core;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using PexMe.ComponentModel;
using PexMe.TermHandler;

namespace PexMe.ObjectFactoryObserver
{
    /// <summary>
    /// Stores details of an uncovered code location
    /// </summary>
    public class UncoveredCodeLocationStore
    {
        public static string GetKey(string location, string explorableType, int termindex)
        {
            //return location + explorableType + termindex;
            return location + explorableType;   //TODO: Need to figure out whether to include termindex or not
        }

        /// <summary>
        /// Location of the code
        /// </summary>
        public CodeLocation Location;

        /// <summary>
        /// Associated explorable type
        /// </summary>
        public TypeEx ExplorableType;
          
        /// <summary>
        /// Set of fields associated with the location
        /// </summary>
        public SafeList<Field> AllFields = new SafeList<Field>(1);

        /// <summary>
        /// Used to handle inheritance issues. For example a class "X" is extending Dictionary.
        /// Now the explorable type changes to Dictionary, however the actual type is "X".
        /// </summary>
        public SafeList<TypeEx> AllFieldTypes = new SafeList<TypeEx>();

        /// <summary>
        /// values of the field used for identifying the modification type
        /// </summary>
        public SafeDictionary<Field, FieldValueHolder> FieldValues = new SafeDictionary<Field, FieldValueHolder>(1);

        /// <summary>
        /// Set of terms associated with the uncovered location.
        /// </summary>
        public int TermIndex;

        /// <summary>
        /// expected type of the field
        /// </summary>
        public FieldModificationType DesiredFieldModificationType;

        /// <summary>
        /// This is currently valid only when the FieldModificationType is FieldModificationType.INCREMENT or FieldModificationType.DECREMENT
        /// </summary>
        public int Fitnessvalue;

        /// <summary>
        /// Set of terms associated with the uncovered location. Stores in textual format
        /// </summary>
        public SafeSet<string> TextualTerms = new SafeSet<string>(1);

        /// <summary>
        /// Suggested factory methods
        /// </summary>
        public SafeSet<Method> SuggestedMethodSetforFactory;

        /// <summary>
        /// string form for debugging
        /// </summary>
        public string SuggestedMethodsforFactory;

        /// <summary>
        /// The field that is responsible for this uncovered location store to exist
        /// </summary>
        public Field TargetField;

        /// <summary>
        /// stores the sequence of method calls executed when this uncovered location is hit
        /// </summary>
        public MethodSignatureSequence MethodCallSequence;
        public bool IsADefectDetectingSequence = false;

        /// <summary>
        /// Stores the stack trace of the code under test when this uncovered
        /// location is reported.
        /// </summary>
        public SafeList<Method> CUTMethodCallSequence;

        /// <summary>
        /// definining equals method
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            var otherobj = obj as UncoveredCodeLocationStore;
            if (otherobj == null)
                return false;

            if (this.Location != otherobj.Location)
                return false;

            if (this.ExplorableType != otherobj.ExplorableType)
                return false;

            if (this.AllFields.Count != otherobj.AllFields.Count)
                return false;

            using (IEnumerator<Field> otherfieldEnum = otherobj.AllFields.GetEnumerator())
            {
                foreach (var thisField in this.AllFields)
                {
                    if (!otherfieldEnum.MoveNext())
                        return false;

                    if (thisField != otherfieldEnum.Current)
                        return false;
                }
            }

            //Also check for method sequences that lead to these uncovered locations
            if (this.MethodCallSequence != null && otherobj.MethodCallSequence != null)
            {
                var seq1 = this.MethodCallSequence.Sequence;
                var seq2 = otherobj.MethodCallSequence.Sequence;
                if (seq1.Count != seq2.Count)
                    return false;

                if (seq1.Count != 0)
                {
                    IEnumerator<string> seq2iter = seq2.GetEnumerator();
                    seq2iter.MoveNext();
                    foreach (var seq1elem in seq1)
                    {
                        if (seq1elem != seq2iter.Current)
                            return false;
                        seq2iter.MoveNext();
                    }
                }               
            }

            //Also check for the term value            
            if (this.TermIndex != otherobj.TermIndex)
                return false;

            return true;
        }

        /// <summary>
        /// string form of uncovered location
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.Location.ToString();
        }
    }
}
