using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Metadata.Names;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Drivers;
using Microsoft.Pex.Engine.Logging;
using Microsoft.Pex.Engine.PostAnalysis;
using Microsoft.Pex.Framework.Packages;
using Enumerable=Microsoft.ExtendedReflection.Collections.Enumerable;

namespace Covana.ResultTrackingExtrator
{
    public sealed class ResultTrackingObserver
        : PexPathPackageAttributeBase
    {
        private bool verbose = false;
        private StringBuilder log;


        private void PexLog(IPexPathComponent host, string title, string text)
        {
            if (verbose)
            {
                host.Log.Dump(title, title, text);
            }
        }

        private void PexLog(IPexPathComponent host, string title, IEnumerable<string> text)
        {
            if (verbose)
            {
                host.Log.LogMessage(title, title + " start");

                foreach (var t in text)
                {
                    host.Log.Dump(title, title, t);
                }

                host.Log.LogMessage(title, title + " start");
            }
        }


        protected override object BeforeRun(IPexPathComponent host)
        {
            return null;
        }

        protected override void AfterRun(IPexPathComponent host, object data)
        {
            Term[] pathConditions = new Term[] {};
            IList<Term> conditions = null;
            IList<CodeLocation> locations = null;
            SafeSet<Method> trackingMethods = null;

            var database = host.GetService<ProblemTrackDatabase>();
            var unInstrumentedMethods = database.ExternalMethods;
            log = database.SimpleLog;
            foreach (var unInstrumentedMethod in unInstrumentedMethods)
            {
                var controller = new ResultTracer(host, unInstrumentedMethod.Method, log);
                log.AppendLine("try tracking " + unInstrumentedMethod.Method.FullName);
                log.AppendLine("*****************************************************");
                using (IEngine trackingEngine = host.PathServices.TrackingEngineFactory.CreateTrackingEngine(controller)
                    )
                {
                    trackingEngine.GetService<IPexTrackingDriver>().Run();
                    pathConditions = Enumerable.ToArray(controller.PathConditions);
                    conditions = controller.Conditions;
                    locations = controller.Locations;
                    trackingMethods = controller.TrackingMethods;
                }
                PexLog(host, "ResultTracing", prettyPrintPathCondition(host, pathConditions));
//                log.AppendLine("condition: " + prettyPrintPathCondition(host, pathConditions));
                PexLog(host, "tracking methods", trackingMethods.Select(x => x.FullName));
                for (int i = 0; i < conditions.Count; i++)
                {
                    var condition = conditions[i];
                    using (var extractor = new ResultTrackConditionExtractor(host.ExplorationServices.TermManager))
                    {
                        extractor.VisitTerm(default(TVoid), condition);
                        if (extractor.Method == null)
                        {
                            host.Log.Dump("method", "not in branch", "null");
                            continue;
                        }
                        PexLog(host, "method", extractor.Method.FullName);

                        PexLog(host, "offset", extractor.CallerOffset.ToString("x"));
                        PexLog(host, "location", extractor.Location.ToString());
                        var method = extractor.Signature as Method;
                        PexLog(host, "signature", method.FullName);
                        log.AppendLine("found method: " + method.FullName + " in branch " + locations[i]);
                        if (!host.GetService<ProblemTrackDatabase>().ExternalMethodInBranch.ContainsKey(method))
                        {
                            host.GetService<ProblemTrackDatabase>().ExternalMethodInBranch.Add(method,
                                                                                                   new HashSet
                                                                                                       <CodeLocation>());
                        }
                        host.GetService<ProblemTrackDatabase>().ExternalMethodInBranch[method].Add(locations[i]);
                    }
                }

                PexLog(host, "location", locations.Select(x => x.ToString()));
                log.AppendLine("*****************************************************");
            }
            log.AppendLine("=========================");
        }


        private static string prettyPrintPathCondition(IPexPathComponent host, Term[] pathConditions)
        {
            var writer = new SafeStringWriter();
            var codeWriter = host.Services.TestManager.Language.CreateBodyWriter(
                writer,
                VisibilityContext.Private,
                100);
            var emitter = new TermEmitter(
                host.ExplorationServices.TermManager,
                new NameCreator());
            for (int i = pathConditions.Length - 1; i >= 0; i--)
            {
                var pathCondition = pathConditions[i];
                if (!emitter.TryEvaluate(
                         new Term[] {pathCondition},
                         10000, // bound on size of expression we are going to pretty-print
                         codeWriter))
                {
                    writer.WriteLine("(expression too big; consider using environment variable {0})",
                                     ExtendedReflectionEnvironmentSettings.NoCodeEmitterSizeLimit.Name);
                }
                else
                {
                    writer.WriteLine("path condition: " + pathCondition.GetType());
                    codeWriter.Return(SystemTypes.Bool);
                }
            }
            return writer.ToString();
        }

        private sealed class ResultTracer
            : PexTrackingControllerBase
        {
            private readonly IPexPathComponent host;
            private readonly MethodName _trackMethod;
            private readonly StringBuilder _log;
            private Stack<Method> methodStack = new Stack<Method>();
            private List<Term> conditions = new List<Term>();
            private List<CodeBranch> branches = new List<CodeBranch>();
            public List<CodeLocation> Locations = new List<CodeLocation>();
            public SafeSet<Method> TrackingMethods = new SafeSet<Method>();
            private bool dump = false;
            private bool track;
            private bool trackArg;
            private Method methodForTrackingArg;

            private void Dump(string text)
            {
                if (dump)
                {
                    _log.AppendLine(text);
                }
            }


            public ResultTracer(IPexPathComponent component, MethodName trackMethod, StringBuilder log)
            {
                host = component;
                _trackMethod = trackMethod;
                _log = log;
            }

            #region Don't do anything special with call arguments

            public override void DerivedArguments(int frameId, Method method, Term[] arguments)
            {
            }

            public override void FrameDisposed(int frameId, PexTrackingFrame frame)
            {
                Dump("in FrameDisposed=====================");
                Dump("frameId: " + frameId + " frame.method: " + frame.Method);
                Dump("end FrameDisposed=====================");
                Dump("");
                methodStack.Pop();
            }

            public override void FrameHasExceptions(int frameId, Method method)
            {
                Dump("in FrameHasExceptions=====================");
                Dump("frameId: " + frameId + " method: " + method);
                Dump("end FrameHasExceptions=====================");
                Dump("");
            }

            public override void FrameHasExplicitFailures(int frameId, Method method,
                                                          IEnumerable<object> exceptionObjects)
            {
                Dump("in FrameHasExplicitFailures=====================");
                Dump("frameId: " + frameId + " method: " + method);
                StringBuilder methodsInExceptionBuilder = new StringBuilder("methods: \n");
                List<string> methodsInException = new List<string>();
                List<string> lines = new List<string>();
                List<string> exceptionStrings = new List<string>();
                try
                {
                    exceptionStrings = extractMethods(exceptionObjects, methodsInExceptionBuilder,
                                                      methodsInException, lines);
                }
                catch (Exception ex)
                {
                    Dump("ex: in extractMethods of FrameHasExplicitFailures: " + ex);
                }

                if (methodsInException.Count > 0)
                {
                    methodsInException.ForEach(x => Dump("extracted methods: " + x));
                }

                for (int i = 0; i < methodsInException.Count; i++)
                {
                    var exceptionMethod = methodsInException[i];
//                    string fullNameWithParameters = _trackMethod.FullNameWithParameters;
//                    Dump("fullNameWithParameters: " + fullNameWithParameters);
//                    Dump("ShortNameWithParameterNames: " + _trackMethod.ShortNameWithParameterNames);
//                    Dump("ShortNameWithParameters: " + _trackMethod.ShortNameWithParameters);
//                    Dump("ShortTypedNameWithParameters: " + _trackMethod.ShortTypedNameWithParameters);
//                    Dump("ShortTypedPrettyNameWithParameters: " + _trackMethod.ShortTypedPrettyNameWithParameters);
//                    fullNameWithParameters = fullNameWithParameters.Substring(fullNameWithParameters.IndexOf(" ") + 1);
//                    Dump("changed signature: " + fullNameWithParameters);
                    Dump("try test exceptionMethod: " + exceptionMethod + " with tracking method: " +
                         _trackMethod.FullName);
                    if (exceptionMethod.Equals(_trackMethod.FullName))
                    {
                        Dump("************found external method throw exception: " + _trackMethod);
                        try
                        {
                            host.GetService<ProblemTrackDatabase>().ExceptionExternalMethods.Add(
                                new ExceptionExternalMethod(_trackMethod.FullName, methodsInException[i + 1],
                                                            lines[i + 1],
                                                            exceptionStrings[i]));

                            Dump("at " + methodsInException[i + 1] + " line: " + lines[i + 1]);
                        }
                        catch (Exception ex)
                        {
                            Dump("ex" + ex + " showing line number of exception or extrenalmethod " + _trackMethod);
                            Dump("i: " + i + " methodsInException.Count: " + methodsInException.Count + " lines.Count: " + lines.Count);
                        }
                    }
                    else
                    {
                        Dump(exceptionMethod + " is not tracking method: " + _trackMethod.FullName);
                    }
                    Dump("method: " + exceptionMethod);
                    Dump("line: " + lines[i]);
                }

                exceptionObjects.ToList().ForEach(x => Dump("type: " + x.GetType() + " " + x.ToString()));
                Dump("end FrameHasExplicitFailures=====================");
                Dump("");
            }

            private List<string> extractMethods(IEnumerable<object> exceptionObjects,
                                                StringBuilder methodsInExceptionBuilder, List<string> methodsInException,
                                                List<string> lines)
            {
                List<string> exceptionStrings = new List<string>();
                foreach (var exceptionObject in exceptionObjects)
                {
                    var exception = exceptionObject as Exception;
                    string stackTrace = exception.StackTrace;
                    Dump("full stacktrace: " + stackTrace);
                    bool lasttime = false;
                    while (stackTrace.IndexOf("(") != -1 || lasttime)
                    {
                        if (lasttime)
                        {
                            lasttime = false;
                        }

                        int leftIndex = stackTrace.IndexOf("(");
                        if (leftIndex != -1)
                        {
                            int startIndex = stackTrace.IndexOf("(") - 1;
                            char current = stackTrace[startIndex];
                            int length = 1;
                            while (current != ' ')
                            {
                                length++;
                                startIndex--;
                                current = stackTrace[startIndex];
                            }

                            string methodName = stackTrace.Substring(startIndex + 1, length - 1);
                            methodsInExceptionBuilder.AppendLine(methodName);
                            methodsInException.Add(methodName);
                            stackTrace = stackTrace.Substring(stackTrace.IndexOf(")") + 1);
                            Dump("extracted methodName: " + methodName);
                            Dump("changed stacktrace: " + stackTrace);
                            Dump("line: " + stackTrace.IndexOf("line"));
                            Dump("(: " + stackTrace.IndexOf("("));
                            if (stackTrace.IndexOf("(") == -1)
                            {
                                lasttime = true;
                            }
                        }


//                        Dump("at: " + stackTrace.IndexOf("at"));
                        if (stackTrace.IndexOf("line") != -1)
                        {
                            string stackTraceAfterLine = stackTrace.Substring(stackTrace.IndexOf("line") + 5);
                            Dump("at: " + stackTraceAfterLine.IndexOf("at"));
                            if (stackTrace.IndexOf("line") < stackTrace.IndexOf("(") &&
                                stackTraceAfterLine.IndexOf("at") != -1)
                            {
                                lines.Add(stackTraceAfterLine.Substring(0, stackTraceAfterLine.IndexOf("at") - 5));
                            }
                            else if (stackTrace.IndexOf("line") != -1 && stackTrace.IndexOf("(") == -1)
                            {
                                lines.Add(stackTrace.Substring(stackTrace.IndexOf("line") + 5));
                            }
                            else
                            {
                                lines.Add("0");
                            }
                        }
                        else
                        {
                            lines.Add("0");
                        }
//                        lines.Add(stackTrace.Substring(stackTrace.IndexOf("line") + 5, stackTrace.IndexOf("at") - (stackTrace.IndexOf("line") + 5)));
                        if (lines.Count > 0)
                        {
                            Dump("lines: " + lines[lines.Count - 1]);
                        }

//                        Dump("methods: " + methodsInExceptionBuilder.ToString());
                        exceptionStrings.Add(exception.ToString());
                    }
                }
                return exceptionStrings;
            }

            public override void PathConditionAdded(Term condition, int codeLabel)
            {
                conditions.Add(condition);
                Method method = methodStack.Peek();
                var branch = new CodeBranch(method.Definition, codeLabel);
                branches.Add(branch);
                Locations.Add(method.Definition.GetBranchLabelSource(codeLabel));
            }

            public override PexArgumentTracking GetArgumentTreatment(PexTrackingThread thread, int frameId,
                                                                     Method method)
            {
                Dump("in GetArgumentTreatment=====================");
                Dump("thread.ThreadId: " + thread.ThreadId + " frameid: " + frameId + " method: " + method);


                if (trackArg && methodForTrackingArg != null && method.Equals(methodForTrackingArg))
                {
                    trackArg = false;
                    methodForTrackingArg = null;
                    Dump("track parameters of " + method);
                    Dump("end GetArgumentTreatment=====================");
                    Dump("");
                    return PexArgumentTracking.Track;
                }

                Dump("end GetArgumentTreatment=====================");
                Dump("");
                return PexArgumentTracking.Derived;
            }

            public override int GetNextFrameId(int threadId, Method method)
            {
                Dump("in GetNextFrameId=====================");
                Dump("threadId: " + threadId + " method: " + method);
                Dump("end GetNextFrameId=====================");
                Dump("");
                methodStack.Push(method);
                return 0;
            }

            #endregion

            public override int GetNextCallId(int threadId, int offset, IMethodSignature methodSignature,
                                              TypeEx[] varArgTypes, Term[] arguments)
            {
                DumpInfo(methodSignature, threadId, offset, varArgTypes);
                var termManager = host.ExplorationServices.TermManager;
                var method = methodSignature as Method;
                if (method != null)
                {
                    Dump("method name: " + method.FullName + " offset: " + offset);
                    if (!method.FullName.Equals(_trackMethod.FullName))
                    {
                        Dump("method: " + method.FullName + " is not tracking method " + _trackMethod);
                        Dump("end GetNextCallId=============================");
                        Dump("");
                        return 0;
                    }
                }


                if (method != null)
                {
                    Dump("method name: " + method.FullName);
                    if (method.FullName.Equals(_trackMethod.FullName))
                    {
                        bool foundSymbol = false;
                        if (arguments == null)
                        {
                            Dump("args is null");
                        }
                        else
                        {
                            string arg = "";
                            foreach (Term term in arguments)
                            {
                                arg += "arg: " + term + " is symbolic: " + termManager.IsSymbol(term);
                                var extractor = new ResultTrackConditionExtractor(termManager);
                                extractor.VisitTerm(default(TVoid), term);
                                arg += " symbol: " + extractor.Log;
                            }
                            Dump("args: " + arg);

                            foreach (Term argument in arguments)
                            {
                                //pex bug
                                if (termManager.IsSymbol(argument))
                                {
                                    foundSymbol = true;
                                    break;
                                }
                                else
                                {
                                    var extractor = new ResultTrackConditionExtractor(termManager);
                                    extractor.VisitTerm(default(TVoid), argument);
                                    if (extractor.foundSymbol)
                                    {
                                        foundSymbol = true;
                                        break;
                                    }
                                }
                            }
                        }

                        track = foundSymbol;
//                        track = true;
                        if (track)
                        {
                            Dump("track " + method.FullName);
                            trackArg = true;
                            methodForTrackingArg = method;
                            Dump("track parameter of " + method.FullName);
                        }
                    }
                }
                else
                {
                    Dump(methodSignature + " signature is null.");
                }

                Dump("end GetNextCallId=============================");
                Dump("");
                return 0;
            }

            private void DumpInfo(IMethodSignature methodSignature, int threadId, int offset, TypeEx[] varArgTypes)
            {
                Dump("in GetNextCallId=============================");
                Dump("method of the stack: " + methodStack.Peek().FullName);
                try
                {
                    Dump("threadId: " + threadId + " offset: " + offset + " method: " + methodSignature);
                    if (varArgTypes == null)
                    {
                        Dump("types is null");
                    }
                    else
                    {
                        string types = "";
                        foreach (TypeEx ex in varArgTypes)
                        {
                            types += "varArgType: " + ex + ", ";
                        }
                        Dump("types: " + types);
                    }
                }
                catch (Exception e)
                {
                    Dump(e.Message + " trace: " + e.StackTrace);
                }
            }

            public override PexResultTracking GetResultTreatment(PexTrackingThread thread, int callId,
                                                                 IMethodSignature methodSignature, TypeEx[] varArgTypes,
                                                                 bool hasDerivedResult)
            {
                Dump("in GetResultTreatment=============================");
                // track uninstrumented method calls
                if (!hasDerivedResult)
                {
                    var method = methodSignature as Method;
                    if (method.FullName.Equals(_trackMethod.FullName))
                    {
                        if (track)
                        {
                            Dump("start to track " + method.FullName);
                            TrackingMethods.Add(method);
                            Dump("end GetResultTreatment=============================");
                            return PexResultTracking.Track;
                        }
                        Dump("method " + method.FullName + " is not tracked!");
                    }


//                    IssueTrackDatabase db;
//                    if (host.TryGetService<IssueTrackDatabase>(out db))
//                    {
////                        host.Log.Dump("size", "size", "uninstrumented methods: " + db.UnInstrumentedMethods.Count);
//                        foreach (var uninstrumentedMethod in db.UnInstrumentedMethods)
//                        {
////                            host.Log.Dump("Method", "Method", "uninstrumented methods: " + uninstrumentedMethod.Method.FullName);
////                            host.Log.Dump("Method", "Method2", "methods: " + method.Definition.FullName);
//                            if (uninstrumentedMethod.Method.FullName.Equals(method.FullName))
//                            {
////                                host.Log.Dump("Method", "track", "track: " + method.Definition.FullName);
//                                if (TrackingMethods.Add(method))
//                                {
//                                    return PexResultTracking.Track;
//                                }
//                            }
//                        }
//                    }
                }
                Dump("end GetResultTreatment=============================");
                return PexResultTracking.ConcreteOrDerived;
            }

            public IEnumerable<Term> PathConditions
            {
                get { return this.PathConditionBuilder.RawConditions; }
            }

            public IList<Term> Conditions
            {
                get { return conditions; }
            }
        }
    }

    [Serializable]
    public class ExceptionExternalMethod
    {
        public string MethodName;
        public string CallerMethod;
        public string Line;
        public string ExceptionString;

        public ExceptionExternalMethod(string methodName, string callerMethod, string line, string exceptionString)
        {
            MethodName = methodName;
            CallerMethod = callerMethod;
            Line = line;
            ExceptionString = exceptionString;
        }

        public bool Equals(ExceptionExternalMethod other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.MethodName, MethodName) && Equals(other.CallerMethod, CallerMethod) &&
                   Equals(other.Line, Line) && Equals(other.ExceptionString, ExceptionString);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ExceptionExternalMethod)) return false;
            return Equals((ExceptionExternalMethod) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (MethodName != null ? MethodName.GetHashCode() : 0);
                result = (result*397) ^ (CallerMethod != null ? CallerMethod.GetHashCode() : 0);
                result = (result*397) ^ (Line != null ? Line.GetHashCode() : 0);
                result = (result*397) ^ (ExceptionString != null ? ExceptionString.GetHashCode() : 0);
                return result;
            }
        }

        public override string ToString()
        {
            return "method: " + MethodName + " throw exception at: \n" + CallerMethod + " line " + Line +
                   "\n exception details: " + ExceptionString;
        }
    }
}