using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZUA
{
    /// <summary>
    /// Represents a Lua function definition that includes a namespace, name, and a delegate to invoke the function.
    /// </summary>
    public interface ILuaFunction
    {
        /// <summary>
        /// Gets the namespace associated with the Lua function.
        /// </summary>
        string Namespace { get; }

        /// <summary>
        /// Gets the name of the Lua function.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates a delegate representing the Lua function's implementation.
        /// </summary>
        /// <returns>A delegate to invoke the Lua function.</returns>
        Delegate CreateFunction();
    }
}
