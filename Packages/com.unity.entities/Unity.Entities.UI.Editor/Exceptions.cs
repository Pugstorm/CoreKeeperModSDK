using System;

namespace Unity.Entities.UI
{
    /// <summary>
    /// The exception that is thrown when trying to resolve an invalid path.
    /// </summary>
    [Serializable]
    internal class InvalidBindingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidBindingException"/> class with a specified path.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public InvalidBindingException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidBindingException"/> class with a specified type and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="inner">The inner exception reference.</param>
        public InvalidBindingException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
