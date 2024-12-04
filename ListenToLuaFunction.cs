using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZUA
{
    /// <summary>
    /// Allows Lua scripts to subscribe to events dynamically and listen for these events in the Lua script.
    /// enables Lua code to 
    /// </summary>
    public class ListenToLuaFunction : ILuaFunction
    {
        public string Namespace => "Zua";
        public string Name => "ListenTo";
        public Delegate CreateFunction()
        {
            return new Action<string>(Implementation);
        }

        /// <summary>
        /// The implementation of the Lua function `ListenTo`.
        /// This function subscribes to the event specified by its name, 
        /// allowing Lua scripts to handle the event when it is triggered.
        /// </summary>
        /// <param name="eventNameArg">The name of the event to listen to. 
        /// This event name must be registered through an ILuaEvent.
        private void Implementation(string eventNameArg)
        {
            Zua.ListenTo(eventNameArg);
        }
    }
}
