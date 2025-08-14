using System.Collections.Generic;

namespace CodexTestProject;

/// <summary>
/// Test a comment with a <see cref="Value"/>
/// </summary>
public class XedocBase
{
    public virtual int Value => 0;

    public void Casting(object o)
    {
        // Cast from simple type
        var i = (List<int>)o;

        // as cast
        var j = o as int?;

        // is cast
        if (o is int k)
        {

        }
    }
}

public interface IXedoc
{
    int Value { get; }
}

public class XedocInt : XedocBase, IXedoc
{
}

public class XedocImpl : XedocBase, IXedoc
{
    public override int Value => 1;

    public void Test()
    {
    }
}
