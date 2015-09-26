using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Monitoring;
using Microsoft.ExtendedReflection.Metadata;
using System.Diagnostics;
using System.IO;
using DUCover.Core;

namespace DUCoverConsole
{
    static class TracingExitCodes
    {
        public const int InvalidArguments = -1;
        public const int UnexpectedException = -2;
    }

    class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static int Main(string[] args)
        {
            //For debugging purpose only
            //GraphXMLTester.TestInstructionGraphXML(args[0], args[1]);
            //bool btrue = true;
            //if (btrue)
            //    return 0;
            //End of for debugging purpose only

            Console.WriteLine("Tracing: A .NET program tracer demo application");
            Console.WriteLine("Copyright (c) Microsoft Corporation. All rights reserved.");
            Console.WriteLine("(c) Microsoft Corporation. All rights reserved.");
            Console.WriteLine();
            Console.WriteLine("usage: sample.tracing.exe <application to trace> <mode> <optional current asselbly>");
            Console.WriteLine("0: Gathers side effects");
            Console.WriteLine("1: Gathers def-use coverage");
            Console.WriteLine();           

            if (args == null || (args.Length != 2 && args.Length != 3))
            {
                Console.WriteLine("Incorrect arguments!!! Please check usage");
                return -1;
            }

            string[] assembliesToMonitor = null;
            if (args[1] == "0")
            {
                System.Environment.SetEnvironmentVariable("DUCOVER_MODE", "0");
                assembliesToMonitor = new string[] { "*" };
            }
            else if (args[1] == "1")
            {
                System.Environment.SetEnvironmentVariable("DUCOVER_MODE", "1");

                if (args.Length == 3)
                {
                    assembliesToMonitor = new string[] { GetShortNameFromAssembly(args[2]) };
                }
                else
                {
                    assembliesToMonitor = new string[] { GetShortNameFromAssembly(args[0]) };
                }
            }
            else
            {
                Console.WriteLine("Incorrect value for Mode. Only 0 or 1 is allowed");
                return -1;
            }
            
            var envvar = System.Environment.GetEnvironmentVariable("DUCOVER_STORE");
            if (envvar == null)
            {
                Console.WriteLine("Environment variable DUCOVER_STORE is not set. Please set it to a valid directory!!!");
                return -1;
            }

            if (args.Length == 3)
            {                
                System.Environment.SetEnvironmentVariable(DUCoverConstants.DUCoverAssemblyVar, GetShortNameFromAssembly(args[2]));
            }
            else
            {
                System.Environment.SetEnvironmentVariable(DUCoverConstants.DUCoverAssemblyVar, GetShortNameFromAssembly(args[0]));
            }     

            //Create directory if not exists
            if (!Directory.Exists(envvar))
                Directory.CreateDirectory(envvar);          

            // set up the environment variables for the instrumented process
            var userAssembly = Metadata<Tracer>.Assembly;
            var userType = Metadata<Tracer>.Type;

            //Is it runnning in NUnit mode. Check for environment variable.
            ProcessStartInfo startInfo;
            var nunitpath = System.Environment.GetEnvironmentVariable(DUCoverConstants.DUCoverNUnitEnvPath);
            if (nunitpath == null)
            {
                startInfo = new ProcessStartInfo(args[0], null);
            }
            else
            {
                startInfo = new ProcessStartInfo(nunitpath, args[0]);
            }
            startInfo.UseShellExecute = false;

            //While running in Mode 0, all assemblies including mscorlib needs to be monitored.
            List<string> ignoreList = new List<string>();
            ignoreList.Add(Metadata<_ThreadContext>.Assembly.ShortName);
            ignoreList.Add(userAssembly.ShortName);
            ignoreList.Add("System.Security");
            ignoreList.Add("System.Threading");
            ignoreList.Add("NLog");
            ignoreList.Add("Microsoft.VisualStudio.QualityTools.UnitTestFramework");
            if(args[1] == "1")
            {
                ignoreList.Add(Metadata<Object>.Assembly.ShortName);                
                ignoreList.Add("System");                
                ignoreList.Add("System.Core");
                ignoreList.Add("System.Data");
                ignoreList.Add("System.Xml");
            }

            if (args.Length == 3)
            {
                ignoreList.Add(GetShortNameFromAssembly(args[0]));                
            }

            //Do not monitor NUnit
            if (nunitpath != null)
            {
                ignoreList.Add(nunitpath);
                ignoreList.Add("nunit.framework");
                ignoreList.Add("nunit.framework.extensions");
                ignoreList.Add("Microsoft.Pex.Framework");
                ignoreList.Add("Microsoft.ExtendedReflection");
            }

            var assembliesToIgnore = new string[ignoreList.Count];
            Array.Copy(ignoreList.ToArray(), assembliesToIgnore, ignoreList.Count);
            
            ControllerSetUp.SetMonitoringEnvironmentVariables(
                startInfo.EnvironmentVariables,
                MonitorInstrumentationFlags.All,
                false,
                userAssembly.Location,
                userType.FullName,
                null, // substitutions assemblies
                null, // types to monitor
                null, // types to exclude to monitor
                null, // namespaces to monitor
                new string[] {
                    "System.Threading",
                    "System.AppDomain",
                    "System.Security", 
                    "System.IO"
                }, // namespace to exclude to monitor
                assembliesToMonitor, // assemblies to monitor
                assembliesToIgnore, //assemblies to ignore
                null, // types to project
                null, null, null,
                null, null,
                null, // log file name
                false, // crash on failure
                null, // target clr version
                true, // protect all .cctors
                false, // disable mscorlib supressions
                ProfilerInteraction.Fail, // allow loading external profiler
                null
                );

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return TracingExitCodes.UnexpectedException;
            }
        }

        /// <summary>
        /// Retrieves the short name from the assembly
        /// </summary>
        /// <param name="p"></param>
        private static string GetShortNameFromAssembly(string assembly)
        {
            return Path.GetFileNameWithoutExtension(assembly);
        }
    }
}
