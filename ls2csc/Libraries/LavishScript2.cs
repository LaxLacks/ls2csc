namespace LavishScript2
{
    /// <summary>
    /// A Module is an extension of the LavishScript 2 environment, via the C++ API
    /// </summary>
    class Module
    {
        public string Name { get; }
        public string Filename { get; }
        public bool IsStarted { get; }

        public void Start();
        public void Start(params object[] parameters_to_module_init);
        public void Stop();

        public static Module Register(string module_name, string file_name);
        public static Module Get(string name);
        public static void Unregister(string module_name);

        public static Module[] AllModules { get; }
    }

    /// <summary>
    /// A Script is a LavishScript 2 program registered with the environment
    /// </summary>
    class Script
    {
        public string Name { get; }
        public string Filename { get; }
        public bool IsStarted { get; }

        public void Start();
        public void Start(params object[] parameters_to_script_entry);
        public void Stop();

        public static String Register(string module_name, string file_name);
        public static String Get(string name);
        public static void Unregister(string module_name);

        public static Script[] AllScripts { get; }

    }

    /// <summary>
    /// A Thread is a microthread hosted by the environment, and operating on LavishScript 2 bytecode
    /// </summary>
    class Thread
    {
        public uint ID { get; }
        public bool IsStarted { get; }
        public bool IsPaused { get; set; }
        public bool IsFirstClass { get; set; }
        public uint InstructionsPerCycle { get; set; }

        public Script Script { get; set; }

        public void Start();
        public void Start(params object[] parameters_to_thread_entry);
        public void Stop();
    }

    class Method
    {
    }

    class StaticMethod
    {
    }

    class Property
    {
    }

    class StaticProperty
    {
    }

    /// <summary>
    /// A Type is a type of object as defined within the environment
    /// </summary>
    class Type
    {
        public string Name { get; }
        public Type ParentType { get; }

        public Method[] AllMethods { get; }
        public StaticMethod[] AllStaticMethods { get; }
        public Property[] AllProperties { get; }
        public StaticProperty[] AllStaticProperties { get; }


        public static Type[] AllTypes { get; }
    }
}
