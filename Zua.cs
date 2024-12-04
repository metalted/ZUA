using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using System.IO;
using ZeepSDK.ChatCommands;
using BepInEx;

namespace ZUA
{
    /// <summary>
    /// Provides a Lua scripting system for extending functionality through custom scripts.
    /// Supports registering functions, events, and loading/unloading Lua scripts dynamically.
    /// </summary>
    public static class Zua
    {
        private static bool init;
        private static Script lua;
        private static bool loaded;
        private static List<ILuaEvent> RegisteredEvents = new List<ILuaEvent>();
        private static List<ILuaEvent> ListenedToEvents = new List<ILuaEvent>();

        /// <summary>
        /// Invoked when Lua functions and events are registered.
        /// </summary>
        public static Action OnRegister;

        /// <summary>
        /// Invoked when a Lua script is successfully loaded.
        /// </summary>
        public static Action OnLoaded;

        /// <summary>
        /// Invoked when a Lua script is unloaded.
        /// </summary>
        public static Action OnUnloaded;       

        public static void Log(string message)
        {
            //Plugin.Instance.Log(message);
        }

        /// <summary>
        /// Initializes the Lua system. Ensures this is only done once.
        /// Registers chat commands for interacting with Lua scripts.
        /// </summary>
        public static void Initialize()
        {
            if (init)
            {
                return;
            }

            RegisterChatCommands();            
            init = true;
        }

        /// <summary>
        /// Loads a Lua script file from the specified path.
        /// If a script is already loaded, it will be unloaded first.
        /// </summary>
        /// <param name="path">The file path of the Lua script to load.</param>
        public static void LoadScriptFile(string path)
        {
            if (loaded)
            {
                UnloadScript();
            }

            if (LoadScript(path))
            {
                ScriptLoaded();
            }
            else
            {
                Unsubscribe();
            }
        }

        /// <summary>
        /// Unloads the current Lua script, unsubscribes from events, and resets the loaded state.
        /// </summary>
        public static void UnloadScript()
        {
            OnUnloaded?.Invoke();
            CallFunction("OnUnload");
            Unsubscribe();
            loaded = false;
        }

        /// <summary>
        /// Listens to an event by name, subscribing to it if found.
        /// </summary>
        /// <param name="eventName">The name of the event to listen to.</param>
        public static void ListenTo(string eventName)
        {
            ILuaEvent luaEvent = RegisteredEvents.FirstOrDefault(e => e.Name == eventName);
            if (luaEvent == null)
            {
                Log($"Event '{eventName}' not found.");
                return;
            }

            if (ListenedToEvents.Any(e => e.Name == luaEvent.Name))
            {
                Log($"Event '{eventName}' is already being listened to.");
                return;
            }

            try
            {
                luaEvent.Subscribe();
                ListenedToEvents.Add(luaEvent);
                Log($"Started listening to event '{eventName}'.");
            }
            catch (Exception ex)
            {
                Log($"Error subscribing to event '{eventName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Calls a Lua function by name with the specified arguments.
        /// </summary>
        /// <param name="functionName">The name of the Lua function to call.</param>
        /// <param name="args">The arguments to pass to the Lua function.</param>
        public static void CallFunction(string functionName, params object[] args)
        {
            try
            {
                var function = lua.Globals.Get(functionName);
                if (function.Type != DataType.Function)
                {
                    Log($"Lua function '{functionName}' is not implemented. Skipping.");
                    return;
                }

                DynValue[] dynArgs = args.Select(arg => DynValue.FromObject(lua, arg)).ToArray();
                lua.Call(function, dynArgs);
            }
            catch (Exception e)
            {
                Log($"Error calling Lua function '{functionName}': {e.Message}");
            }
        }

        /// <summary>
        /// Registers a Lua function of the specified type.
        /// </summary>
        /// <typeparam name="TFunction">The type of the Lua function to register.</typeparam>
        public static void RegisterFunction<TFunction>()
        where TFunction : ILuaFunction, new()
        {
            RegisterFunction(typeof(TFunction));
        }

        /// <summary>
        /// Registers a C# type for use in Lua scripts.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        public static void RegisterType<T>()
        {
            if (UserData.IsTypeRegistered(typeof(T)))
            {
                Log($"Type '{typeof(T).FullName}' is already registered. Skipping.");
                return;
            }

            try
            {
                UserData.RegisterType<T>();
                Log($"Successfully registered type '{typeof(T).FullName}'.");
            }
            catch (Exception ex)
            {
                Log($"Error registering type '{typeof(T).FullName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a Lua event of the specified type.
        /// </summary>
        /// <typeparam name="TEvent">The type of the Lua event to register.</typeparam>
        public static void RegisterEvent<TEvent>() where TEvent : ILuaEvent, new()
        {
            var luaEvent = new TEvent();
            if (RegisteredEvents.Any(e => e.Name == luaEvent.Name)) return;
            RegisteredEvents.Add(luaEvent);
        }

        /// <summary>
        /// Attempts to load a Lua script from the Plugins folder by name.
        /// </summary>
        /// <param name="name">The name of the Lua script file to load (without the extension).</param>
        public static void TryLoadLuaFromPluginsFolder(string name)
        {
            string searchPattern = $"{name}.lua";

            // Search for the file in the directory
            string[] luaFiles = Directory.GetFiles(Paths.PluginPath, searchPattern, SearchOption.AllDirectories);

            if (luaFiles.Length > 0)
            {
                LoadScriptFile(luaFiles[0]);
            }
        }

        private static void ScriptLoaded()
        {
            OnLoaded?.Invoke();
            CallFunction("OnLoad");
            loaded = true;
        }
        private static bool LoadScript(string path)
        {
            lua = new Script(CoreModules.None);

            RegisterAllFunctionsInCurrentAssembly();
            RegisterAllEventsInCurrentAssembly();

            OnRegister?.Invoke();

            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                try
                {
                    lua.DoString(content);
                    return true;
                }
                catch (InterpreterException ex)
                {
                    Log($"Lua Error: {ex.DecoratedMessage}");
                    return false;
                }
            }
            else
            {
                Log($"Lua file not found: {path}");
                return false;
            }
        }
        
        private static void RegisterAllFunctionsInCurrentAssembly()
        {
            var types = typeof(Zua).Assembly.GetTypes()
                .Where(x => !x.IsAbstract && x.IsClass)
                .Where(x => typeof(ILuaFunction).IsAssignableFrom(x))
                .ToList();

            foreach (var type in types)
            {
                RegisterFunction(type);
            }
        }

        private static void RegisterFunction(Type functionType)
        {
            if (functionType == null)
                throw new ArgumentNullException(nameof(functionType));

            if (!typeof(ILuaFunction).IsAssignableFrom(functionType))
                throw new ArgumentException(nameof(functionType));

            var function = Activator.CreateInstance(functionType) as ILuaFunction;

            var namespaceTable = lua.Globals.Get(function.Namespace).Table;
            if (namespaceTable == null)
            {
                namespaceTable = new Table(lua);
                lua.Globals[function.Namespace] = namespaceTable;
            }

            DynValue existingFunction = namespaceTable.Get(function.Name);
            if (existingFunction == DynValue.Nil)
            {
                namespaceTable[function.Name] = function.CreateFunction();
                Log($"Registered: {function.Namespace}.{function.Name}");
            }
            else
            {
                Log($"Skipped: {function.Namespace}.{function.Name} (already exists)");
            }
        }
        
        private static void RegisterAllEventsInCurrentAssembly()
        {
            var eventTypes = typeof(Zua).Assembly.GetTypes()
                .Where(t => typeof(ILuaEvent).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var type in eventTypes)
            {
                var luaEvent = Activator.CreateInstance(type) as ILuaEvent;
                if (luaEvent != null && RegisteredEvents.All(e => e.Name != luaEvent.Name))
                {
                    RegisteredEvents.Add(luaEvent);
                }
            }
        }

        private static void Unsubscribe()
        {
            foreach (var luaEvent in ListenedToEvents)
            {
                try
                {
                    luaEvent.Unsubscribe();
                }
                catch (Exception ex)
                {
                    Log($"Error unsubscribing from event '{luaEvent.Name}': {ex.Message}");
                }
            }

            ListenedToEvents.Clear();
        }

        private static void RegisterChatCommands()
        {
            ChatCommandApi.RegisterLocalChatCommand(
                "/",
                "zua load",
                "Loads the scripts from the plugins folder by name.",
                arguments => {

                    if (!string.IsNullOrWhiteSpace(arguments))
                    {
                        TryLoadLuaFromPluginsFolder(arguments.Trim());
                    }
                }
            );

            ChatCommandApi.RegisterLocalChatCommand(
                "/",
                "zua unload",
                "Unloads the current script if there is any, and unsubscribes from any events.",
                arguments => {
                    UnloadScript();
                }
            );            
        }
    }
}
