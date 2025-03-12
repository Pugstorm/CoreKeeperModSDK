using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Jobs
{
    /// <summary>
    /// Used by automatically generated code. Do not use in projects.
    /// </summary>
    public class EarlyInitHelpers
    {
        /// <summary>
        /// Used by automatically generated code. Do not use in projects.
        /// Delegate used for early initialization
        /// </summary>
        public delegate void EarlyInitFunction();

        private static List<EarlyInitFunction> s_PendingDelegates;

        static EarlyInitHelpers()
        {
            FlushEarlyInits();
        }

        /// <summary>
        /// Used by automatically generated code. Do not use in projects.
        /// Calls all EarlyInit delegates and clears the invocation list
        /// </summary>
        public static void FlushEarlyInits()
        {
            while (s_PendingDelegates != null)
            {
                var oldList = s_PendingDelegates;
                s_PendingDelegates = null;

                for (int i = 0; i < oldList.Count; ++i)
                {
                    try
                    {
                        oldList[i]();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Used by automatically generated code. Do not use in projects.
        /// Adds an EarlyInit helper function to invocation list.
        /// </summary>
        /// <param name="func">EarlyInitFunction add to early call list</param>
        public static void AddEarlyInitFunction(EarlyInitFunction func)
        {
            if (s_PendingDelegates == null)
                s_PendingDelegates = new List<EarlyInitFunction>();

            s_PendingDelegates.Add(func);
        }

        /// <summary>
        /// Used by automatically generated code. Do not use in projects.
        /// This methods is called when JobReflectionData cannot be created during EarlyInit.
        /// </summary>
        /// <param name="ex">Exception type to throw</param>
        public static void JobReflectionDataCreationFailed(Exception ex)
        {
            Debug.LogError($"Failed to create job reflection data. Please refer to callstack of exception for information on which job could not produce its reflection data.");
            Debug.LogException(ex);
        }
    }

}
