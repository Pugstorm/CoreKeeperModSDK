using System;
using Unity.Collections;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// Stores the result of a network operation. This is normally used when a job needs to return
    /// a result to its caller. For example the <see cref="ReceiveJobArguments"/> structure contains
    /// one which is used to report the result of receive operations on network interfaces, which
    /// is then reported through <see cref="NetworkDriver.ReceiveErrorCode"/>.
    /// </summary>
    public struct OperationResult : IDisposable
    {
        private FixedString64Bytes m_Label;
        private NativeReference<int> m_ErrorCode;

        internal OperationResult(FixedString64Bytes label, Allocator allocator)
        {
            m_Label = label;
            m_ErrorCode = new NativeReference<int>(allocator);
        }

        /// <summary>
        /// Get and set the error code for the operation. Setting a non-zero value will result in
        /// the error also being logged to the console.
        /// </summary>
        /// <value>Numerical error code (0 is success, anything else is an error).</value>
        public int ErrorCode
        {
            get => m_ErrorCode.Value;
            set
            {
                if (value != 0)
                {
                    DebugLog.ErrorOperation(ref m_Label, value);
                }
                m_ErrorCode.Value = value;
            }
        }

        public void Dispose()
        {
            m_ErrorCode.Dispose();
        }
    }
}
