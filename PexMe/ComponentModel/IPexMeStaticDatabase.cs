using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;

namespace PexMe.Core
{
    internal interface IPexMeStaticDatabase
        : IComponent, IService
    {
        /// <summary>
        /// Gets a field store observed through static analysis
        /// </summary>
        /// <param name="field"></param>
        /// <param name="fs"></param>
        /// <returns></returns>
        bool TryGetWriteMethods(Field field, TypeEx declaringType, out SafeSet<Method> writeMethods);
    }
}
