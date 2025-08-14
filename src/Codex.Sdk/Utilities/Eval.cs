namespace Codex.Utilities;

public static class Eval
{
    public static Eval<T> Create<T>(Func<T> value)
    {
        return new Eval<T>(value);
    }
}

public record Eval<T>(Func<T> GetValue)
{
    public T Value => GetValue();

    public override string ToString()
    {
        return "Expanded to evaluate";
    }
}