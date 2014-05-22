namespace LavishScript2
{

    public sealed class InputDeclaration
    {
        public System.String CSharpDeclaration { get; }
        public System.String Name { get; }
        public LavishScript2.Type Type { get; }
    }

    /// <summary>
    /// A Module is an extension of the LavishScript 2 environment, via the C++ API
    /// </summary>
    public sealed class Module
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
    public sealed class Script
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
    public sealed class Thread
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

    public sealed class Method
    {
        public InputDeclaration[] InputDeclarations { get; }
        public Type ParentType { get; }

    }

    public sealed class StaticMethod
    {
        public InputDeclaration[] InputDeclarations { get; }
        public Type ParentType { get; }
    }

    public sealed class Property
    {
        public Type ParentType { get; }
        public Type PropertyType { get; }
        public bool IsGetSupported { get; }
        public bool IsSetSupported { get; }
        public string CSharpDeclaration { get; }

        public object Get(object subject);
        public void Set(object subject, object value);
    }

    public sealed class StaticProperty
    {
        public Type ParentType { get; }
        public Type PropertyType { get; }
        public bool IsGetSupported { get; }
        public bool IsSetSupported { get; }
        public string CSharpDeclaration { get; }

        public object Get();
        public void Set(object value);
    }

    public sealed class Field
    {
        public System.Object Get(System.Object subject);
        public void Set(System.Object subject, System.Object value);
        public System.String CSharpDeclaration { get; }
        public LavishScript2.Type FieldType { get; }
        public System.Object InitialValue { get; }
        public LavishScript2.Type ParentType { get; }
    }

    public sealed class StaticField
    {
        public System.String CSharpDeclaration { get; }
        public System.String Name { get; }
        public LavishScript2.Type ParentType { get; }
        public LavishScript2.Type StaticFieldType { get; }
        public System.Object Value { get; set; }
    }
    /// <summary>
    /// A Type is a type of object as defined within the environment
    /// </summary>
    public sealed class Type
    {
        public string Name { get; }
        public Type ParentType { get; }

        public LavishScript2.Table AllMethods { get; }
        public LavishScript2.Table AllStaticMethods { get; }
        public LavishScript2.Table AllProperties { get; }
        public LavishScript2.Table AllStaticProperties { get; }
        public LavishScript2.Field[] AllFields { get; }
        public LavishScript2.Table AllStaticFields { get; }
        public Type[] AllInterfaces { get; }

        public static Type[] AllTypes { get; }
    }

    public sealed class Table : System.Collections.IEnumerable
    {
        public System.Collections.IEnumerator GetEnumerator();

        public void Add(string key, object value);
        public void Clear();
        public bool ContainsKey(string key);
        public bool ContainsValue(object value);
        public bool Remove(string key);
        public bool TryGetValue(string key, out object value);
        public object this[string key] { get; set; }
    }
}
