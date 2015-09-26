// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Monitoring;
using DUCover.Core;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;

namespace DUCover.Core
{
    /// <summary>
    /// An instance of this class is created by the ExtendedReflection library
    /// at the beginning of the execution of a .NET application
    /// after setting environment variables with the registertrace.cmd script.
    /// </summary>
    [__DoNotInstrument]
    public sealed class Tracer : ExecutionMonitorBase
    {
        public Tracer()
        {
            lock (this)
            {
                Console.WriteLine("... tracer loaded");
            }
        }

        /// <summary>
        /// This method is invoked for each thread of the monitored .NET application.
        /// </summary>
        /// <param name="threadId">an identifier of the thread</param>
        /// <returns>a monitor of events for the given thread id</returns>
        protected override IThreadExecutionMonitor CreateThreadExecutionMonitor(int threadId)
        {
            //SafeDebugger.Break();

            var mode = System.Environment.GetEnvironmentVariable("DUCOVER_MODE");
            if (mode == "0")
            {
                return new SETracer(threadId);
            }
            else
            {
                return new ThreadTracer(threadId);
            }
        }
    }
}