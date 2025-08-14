namespace CodexTestProject;

public class Other1
{
    public int ClassAndMemberSameName { get; set; }
}

public class Other2
{
    public int ClassAndMemberSameName;
}

public class ClassAndMemberSameName
{
}

public class Other3
{
    public int ClassAndMemberSameName()
    {
        return 0;
    }
}