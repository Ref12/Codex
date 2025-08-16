using System.Reflection;
using Codex.Application.Verbs;
using Codex.Storage;
using Codex.Utilities;

namespace Codex.Application;

public sealed record CodexProgram : CodexProgramBase
{
    public override async ValueTask<int?> TryRunSpecialAsync(string[] args)
    {
        if (CodexLegacyProgram.LegacyVerbNames.Contains(args.FirstOrDefault()))
        {
            var program = new CodexLegacyProgram();
            return await program.RunAsync(args);
        }

        return null;
    }
}

public record CodexProgramBase : OperationBase
{
    public static Task<int> Main(params string[] args)
    {
        return new CodexProgram().RunAsync(args);
    }

    public virtual ValueTask<int?> TryRunSpecialAsync(string[] args)
    {
        return ValueTask.FromResult<int?>(null);
    }

    public async Task<int> RunAsync(params string[] args)
    {
        Arguments = args;
        if (!MiscUtilities.TryGetEnvironmentVariable("CODEX_DISABLE_PRINT_ARGS", out _))
        {
            Console.WriteLine(Environment.CommandLine);
            Console.WriteLine("Args");
            Console.WriteLine(String.Join("\n", args));
        }

        var result = await TryRunSpecialAsync(args);
        if (result.HasValue) return result.Value;

        ParserResult<object> parsedArgs = ParseArgs();

        if (parsedArgs.Tag == ParserResultType.NotParsed)
        {
            handleErrors(parsedArgs.Errors);
            return -1;
        }

        return await runAsync((OperationBase)parsedArgs.Value);

        ValueTask<int> runAsync(OperationBase operation)
        {
            operation.Cmdlet = Cmdlet;
            return operation.RunAsync(throwErrors: false);
        }

        void handleErrors(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }
        }
    }

    public ParserResult<object> ParseArgs()
    {
        var verbTypes = GetCandidateVerbTypes().Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();

        var parser = new Parser(s =>
        {
            s.CaseSensitive = false;
            s.CaseInsensitiveEnumValues = true;
            s.AllowMultiInstance = true;
            s.AutoHelp = true;
            s.HelpWriter = Console.Error;
        });

        var parsedArgs = parser.ParseArguments(Arguments, verbTypes);
        return parsedArgs;
    }

    public static ParserResult<object> ParseArgs(params string[] args)
    {
        return new CodexProgram()
        {
            Arguments = args
        }.ParseArgs();
    }

    protected virtual IEnumerable<Type> GetCandidateVerbTypes()
    {
        var type = this.GetType();
        return this.GetType().Assembly.GetTypes();
    }

    public string[] Arguments { get; set; }

    protected override ValueTask<int> ExecuteAsync()
    {
        throw new NotImplementedException();
    }
}