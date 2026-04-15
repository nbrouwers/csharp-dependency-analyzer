using System;
using System.Reflection;

namespace SampleApp.Infrastructure
{
    /// <summary>
    /// Loads order-related types by name using the .NET reflection API.
    /// Exercises the static reflection dependency detection feature:
    /// Type.GetType() and Assembly.GetType() with string literal FQNs
    /// are detected as dependencies on the named types.
    /// </summary>
    public class OrderTypeRegistry
    {
        /// <summary>
        /// Returns the <see cref="Type"/> of <c>SampleApp.Core.OrderService</c>
        /// resolved via reflection. Detected as a direct dependency on OrderService.
        /// </summary>
        public Type? GetOrderServiceType()
        {
            return Type.GetType("SampleApp.Core.OrderService");
        }

        /// <summary>
        /// Returns the <see cref="Type"/> of <c>SampleApp.Core.OrderValidator</c>
        /// resolved from an explicit assembly. Detected as a direct dependency
        /// on OrderValidator via Assembly.GetType string literal.
        /// </summary>
        public Type? GetOrderValidatorType(Assembly assembly)
        {
            return assembly.GetType("SampleApp.Core.OrderValidator");
        }
    }
}
