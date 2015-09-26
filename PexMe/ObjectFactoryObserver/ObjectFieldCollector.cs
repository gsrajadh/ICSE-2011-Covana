using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.Pex.Engine.ComponentModel;
using PexMe.Common;
using PexMe.TermHandler;

namespace PexMe.ObjectFactoryObserver
{

    /**
     * OBSOLETE. The functionality of this class has been moved to TermSolver class in TermHandler package
     * */
    sealed class ObjectFieldCollector : TermInternalizingRewriter<TVoid>
    {
        public SafeList<Field> Fields = new SafeList<Field>();
        public SafeList<TypeEx> Types = new SafeList<TypeEx>();
        public SafeDictionary<Field, FieldValueHolder> FieldValues = new SafeDictionary<Field, FieldValueHolder>();        
        public Field lastAccessedField = null;
        private IPexComponent host;

        public ObjectFieldCollector(IPexComponent host, TermManager termManager)
            : base(termManager, TermInternalizingRewriter<TVoid>.OnCollection.Fail)
        {
            this.host = host;
        }

        ////
        //// Summary:
        ////     Visitor for array-element type.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   arrayType:
        ////     type of an array
        //public override Term VisitArrayElementType(TParameter parameter, Term term, Term arrayType)
        //{

        //}
        ////
        ////
        //// Summary:
        ////     Visitor for box-value type.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   boxType:
        ////     type of an array
        //public override Term VisitBoxValueType(TParameter parameter, Term term, Term boxType);
        ////
        //// Summary:
        ////     Visitor for an empty compound, i.e. the compound whose elements are default
        ////     values.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        //public override Term VisitDefaultStruct(TParameter parameter, Term term);
        ////
        //// Summary:
        ////     Visitor for a map whose element are all mapped to elementValue.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   indexLayout:
        ////
        ////   elementValue:
        //public override Term VisitFill(TParameter parameter, Term term, Layout indexLayout, Term elementValue);
        ////
        //// Summary:
        ////     Visitor for an invocation.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   function:
        ////     function
        ////
        ////   time:
        ////     value that represents the invocation time
        ////
        ////   arguments:
        ////     method arguments
        //public override Term VisitFunctionApplication(TParameter parameter, Term term, IFunction function, Term time, Term[] arguments);
        ////
        //// Summary:
        ////     Visitor for a fused compound.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   leftCompound:
        ////     The left compound.
        ////
        ////   offset:
        ////     The offset.
        ////
        ////   rightCompound:
        ////     The right compound.
        //public override Term VisitFuse(TParameter parameter, Term term, Term leftCompound, Term offset, Term rightCompound);
        ////
        //// Summary:
        ////     Visitor for an integer constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   value:
        ////     constant
        //public override Term VisitI1(TParameter parameter, Term term, byte value);
        ////
        //// Summary:
        ////     Visitor for an integer constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   value:
        ////     constant
        //public override Term VisitI2(TParameter parameter, Term term, short value);
        ////
        //// Summary:
        ////     Visitor for an integer constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   value:
        ////     constant
        //public override Term VisitI4(TParameter parameter, Term term, int value);
        ////
        //// Summary:
        ////     Visitor for an integer constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   value:
        ////     constant
        //public override Term VisitI8(TParameter parameter, Term term, long value);
        ////
        //// Summary:
        ////     Visitor for an if-then-else value, that evaluates to the then value if the
        ////     condition holds, and to else otherwise.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   condition:
        ////     condition of Microsoft.ExtendedReflection.Metadata.Layout.I4
        ////
        ////   then:
        ////     value with same layout as else
        ////
        ////   else:
        ////     value with same layout as then
        //public override Term VisitIfThenElse(TParameter parameter, Term term, Term condition, Term then, Term @else);
        ////
        //// Summary:
        ////     Visitor for a subtype test.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   sourceType:
        ////     source type
        ////
        ////   targetType:
        ////     target type
        //public override Term VisitIsAssignable(TParameter parameter, Term term, Term sourceType, Term targetType);
        ////
        //// Summary:
        ////     Visitor for a multi-dimensional array index.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   indices:
        ////     list of indices
        //public override Term VisitMdIndex(TParameter parameter, Term term, Term[] indices);
        ////
        //// Summary:
        ////     Visitor for a constant representing a method.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   method:
        //public override Term VisitMethod(TParameter parameter, Term term, Method method);
        ////
        //// Summary:
        ////     Visitor for a moved compound.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   compound:
        ////     The compound.
        ////
        ////   offset:
        ////     The offset.
        //public override Term VisitMove(TParameter parameter, Term term, Term compound, Term offset);
        ////
        //// Summary:
        ////     Visitor for an object reference constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   id:
        ////     id of object
        ////
        ////   properties:
        ////     properties of object
        //public override Term VisitObject(TParameter parameter, Term term, IObjectId id, ObjectPropertyCollection properties);
        ////
        //// Summary:
        ////     Visitor for an object property.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   reference:
        ////     object reference
        ////
        ////   property:
        ////     object property
        //public override Term VisitObjectProperty(TParameter parameter, Term term, Term reference, ObjectProperty property);
        ////
        //// Summary:
        ////     Visitor for a pointer to an argument of a method call.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   thread:
        ////     index of thread in which call occurred
        ////
        ////   frame:
        ////     index of stack-frame in thread
        ////
        ////   argumentIndex:
        ////     index of argument in stack-frame
        //public override Term VisitPointerToArgument(TParameter parameter, Term term, int thread, int frame, int argumentIndex);
        ////
        //// Summary:
        ////     Visitor for a pointer to a uniform compound indexed over Microsoft.ExtendedReflection.Metadata.Layout.I,
        ////     where indices range from zero to the given maximum length
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   compound:
        ////     uniform compound
        ////
        ////   length:
        ////     maximum length
        //public override Term VisitPointerToBoundedIUniform(TParameter parameter, Term term, Term compound, Term length);
        ////
        //// Summary:
        ////     Visitor for a pointer to an element in a compound value.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   baseAddress:
        ////     address of compound value
        ////
        ////   index:
        ////     index into compound value
        //public override Term VisitPointerToElement(TParameter parameter, Term term, Term baseAddress, Term index);
        ////
        //// Summary:
        ////     Visitor for a pointer to an instance-field of an object.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   instanceField:
        //public override Term VisitPointerToInstanceFieldMap(TParameter parameter, Term term, Field instanceField);
        ////
        //// Summary:
        ////     Visitor for a pointer to a local variable of a method call.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   thread:
        ////     index of thread in which call occurred
        ////
        ////   frame:
        ////     index of stack-frame in thread
        ////
        ////   localIndex:
        ////     index of local variable in stack-frame
        //public override Term VisitPointerToLocal(TParameter parameter, Term term, int thread, int frame, int localIndex);
        ////
        //// Summary:
        ////     Visitor for a pointer to an illegal address.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        //public override Term VisitPointerToNowhere(TParameter parameter, Term term);
        ////
        //// Summary:
        ////     Visitor for a pointer to the topmost element of the evaluation stack of a
        ////     method call.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   thread:
        ////     index of thread in which call occurred
        ////
        ////   frame:
        ////     index of stack-frame in thread
        //public override Term VisitPointerToStackTop(TParameter parameter, Term term, int thread, int frame);
        ////
        //// Summary:
        ////     Visitor for a pointer to a static field.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   staticField:
        //public override Term VisitPointerToStaticField(TParameter parameter, Term term, Field staticField);
        ////
        //// Summary:
        ////     Visitor for a pointer to an immutable value.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   value:
        //public override Term VisitPointerToValue(TParameter parameter, Term term, Term value);
        ////
        //// Summary:
        ////     Visitor for a floating-point constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   value:
        ////     constant
        //public override Term VisitR4(TParameter parameter, Term term, float value);
        ////
        //// Summary:
        ////     Visitor for a floating-point constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   value:
        ////     constant
        //public override Term VisitR8(TParameter parameter, Term term, double value);
        ////
        //// Summary:
        ////     Visitor for a selection.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   compound:
        ////     compound value
        ////
        ////   index:
        ////     index into compound
        //public override Term VisitSelect(TParameter parameter, Term term, Term compound, Term index);
        ////
        //// Summary:
        ////     Visitor for a struct-field constant.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   instanceField:
        //public override Term VisitStructField(TParameter parameter, Term term, Field instanceField);
        ////
        //// Summary:
        ////     Visitor for a symbol.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   key:
        ////     identifier of symbol
        //public override Term VisitSymbol(TParameter parameter, Term term, ISymbolId key);
        ////
        //// Summary:
        ////     Visitor for a constant representing a type.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   type:
        //public override Term VisitType(TParameter parameter, Term term, TypeEx type);
        ////
        //// Summary:
        ////     Visitor for a unary operation.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   operator:
        ////     unary operator
        ////
        ////   operand:
        ////     operand value
        //public override Term VisitUnary(TParameter parameter, Term term, UnaryOperator @operator, Term operand);
        ////
        //// Summary:
        ////     Visitor for a constant that represents an undefined value.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        //public override Term VisitUndef(TParameter parameter, Term term);
        ////
        //// Summary:
        ////     Visitor for an updated compound.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   baseCompound:
        ////     compound value
        ////
        ////   updates:
        ////     set of updates
        //public override Term VisitUpdate(TParameter parameter, Term term, Term baseCompound, ITermMap updates);
        ////
        //// Summary:
        ////     Visitor for a vtable-lookup.
        ////
        //// Parameters:
        ////   parameter:
        ////     parameter that is threaded through
        ////
        ////   term:
        ////     the value that is visited
        ////
        ////   reference:
        ////     object reference
        ////
        ////   method:
        ////     virtual method
        //public override Term VisitVTableMethod(TParameter parameter, Term term, Term reference, Method method);


        public override Term VisitI2(TVoid parameter, Term term, short value)
        {
            if (this.lastAccessedField != null && this.lastAccessedField.Type == SystemTypes.Int16)
            {
                FieldValueHolder fvh = new FieldValueHolder(FieldValueType.SHORT);
                fvh.shortValue = value;
                this.FieldValues[lastAccessedField] = fvh;
            }
            return base.VisitI2(parameter, term, value);
        }

        public override Term VisitI4(TVoid parameter, Term term, int value)
        {
            if (this.lastAccessedField != null && this.lastAccessedField.Type == SystemTypes.Int32)
            {
                FieldValueHolder fvh = new FieldValueHolder(FieldValueType.INTEGER);
                fvh.intValue = value;

                //TODO: A worst fix for visiting the term. need to understand more on how to get
                //concrete values within a term
                FieldValueHolder existingFvh;
                if (this.FieldValues.TryGetValue(this.lastAccessedField, out existingFvh))
                {
                    if (existingFvh.intValue < fvh.intValue)
                        this.FieldValues[lastAccessedField] = fvh;
                }
                else
                    this.FieldValues[lastAccessedField] = fvh;
            }
            return base.VisitI4(parameter, term, value);
        }

        public override Term VisitI8(TVoid parameter, Term term, long value)
        {
            if (this.lastAccessedField != null && this.lastAccessedField.Type == SystemTypes.Int64)
            {
                FieldValueHolder fvh = new FieldValueHolder(FieldValueType.LONG);
                fvh.longValue = value;
                this.FieldValues[lastAccessedField] = fvh;
            }
            return base.VisitI8(parameter, term, value);
        }       

        public override Term VisitSymbol(TVoid parameter, Term term, ISymbolId key)
        {
            if (this.TermManager.TryGetInstanceField(term, out this.lastAccessedField))
            {
                //Currently we are handling only these types
                if (PexMeFilter.IsTypeSupported(this.lastAccessedField.Type))
                {
                    this.Fields.Add(this.lastAccessedField);                    
                }
            }

            TypeEx objectType;
            if (key is ISymbolIdWithType)
            {
                var type = key as ISymbolIdWithType;
                Types.Add(type.Type);
            }
            return base.VisitSymbol(parameter, term, key);
        }

        // Summary:
        //     Visitor for a binary operation.
        
        // Parameters:
        //   parameter:
        //     parameter that is threaded through
        //
        //   term:
        //     the value that is visited
        //
        //   operator:
        //     binary operator
        //
        //   left:
        //     left operand value
        //
        //   right:
        //     right operand value
        public override Term VisitBinary(TVoid parameter, Term term, BinaryOperator @operator, Term left, Term right)
        {
            return base.VisitBinary(parameter, term, @operator, left, right);
        }

        public override Term VisitIfThenElse(TVoid parameter, Term term, Term condition, Term then, Term @else)
        {
            return base.VisitIfThenElse(parameter, term, condition, then, @else);
        }
    }
}
