using MoonSharp.Interpreter;

namespace Content.Server.Vanilla.Moonsharp;

public sealed class MoonsharpVM
{
    private readonly Script _script;
    private DynValue? _coroutine;

    public bool Running { get; private set; }

    public MoonsharpVM()
    {
        _script = new Script();
    }

    public void Run(string code)
    {
        if (Running)
            return;

        // чтобы скрипт можно было остановить
        _script.DoString($"""
            function __drone_main()
                {code}
            end
        """);

        _coroutine = _script.CreateCoroutine(
            _script.Globals.Get("__drone_main"));

        Running = true;
    }

    public void Tick()
    {
        if (!Running || _coroutine == null)
            return;

        var result = _coroutine.Coroutine.Resume();

        if (_coroutine.Coroutine.State == CoroutineState.Dead)
            Running = false;
    }

    public void Stop()
    {
        if (!Running)
            return;

        Running = false;
        _coroutine = null;
    }
}
