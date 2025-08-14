using System.Reflection;
using Codex.Utilities.Serialization;
using Meziantou.Framework.CodeDom;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Dom = Meziantou.Framework.CodeDom;

namespace Codex.Framework.Generator;

using static TypeSystemHelpers;

public partial record GeneratorContext
{
    private const BindingFlags FlattenPublicInstanceFlags = BindingFlags.Public 
        | BindingFlags.Instance 
        | BindingFlags.FlattenHierarchy;
    private const BindingFlags DeclaredPublicInstanceFlags = BindingFlags.Public 
        | BindingFlags.Instance 
        | BindingFlags.DeclaredOnly;
    public string LocalsWalkerPath => Path.Combine(ManagedProjectGenPath, "ScopeOperationWalker.g.cs");

    public void GenerateLocalsWalker()
    {
        var operationWalkerType = typeof(OperationWalker<>);

        var members = operationWalkerType.GetMembers(FlattenPublicInstanceFlags)
            .OfType<MethodInfo>()
            .Where(m => m.IsVirtual)
            .Select(m => (Method: m, Parameters: m.GetParameters()))
            .Where(m => 
                m.Parameters.Length == 2
                && m.Parameters[0].ParameterType != typeof(IOperation) 
                && m.Parameters[0].ParameterType.IsAssignableTo(typeof(IOperation)))
            .ToArray();

        var operationTypes = members.Select(m => m.Parameters[0].ParameterType)
            .Where(t => t.IsAssignableTo(typeof(IOperation)))
            .Distinct()
            .ToArray();

        var ns = new Dom.NamespaceDeclaration("Codex.Analysis.Managed");

        var typeParam = new TypeParameter("TArgument");

        var localsWalker = new ClassDeclaration("ScopeOperationWalker")
        { 
            Modifiers = Modifiers.Partial,
            Parameters =
            {
                typeParam
            },
            BaseType = new TypeReference(typeof(OperationWalker<>)).MakeGeneric(typeParam)
        };

        ns.AddType(localsWalker);

        foreach (var item in members)
        {
            var parameterType = item.Parameters[0].ParameterType;

            var properties = parameterType.GetTransitiveInterfaceProperties()
                .Where(IsLocalSymbolProperty)
                .DistinctBy(p => p.Name)
                .ToList();

            if (properties.Count == 0) continue;

            var method = item.Method.CreateOverride(modify: method =>
            {
                method.Statements ??= new();

                method.Statements.Add(new MethodInvokeExpression(
                        new MemberReferenceExpression(null, "BeforeVisitScope"),
                        method.Arguments[0],
                        method.Arguments[1]));

                foreach (var property in properties)
                {
                    method.Statements.Add(new MethodInvokeExpression(
                        new MemberReferenceExpression(null, "VisitLocalSymbol"),
                        method.Arguments[0],
                        new MemberReferenceExpression(method.Arguments[0], property.Name),
                        method.Arguments[1]));
                }
            },
            callBase: true);

            var returnStatement = (ReturnStatement)method.Statements[method.Statements.Count - 1];
            returnStatement.Expression = new MethodInvokeExpression(
                        new MemberReferenceExpression(null, "AfterVisitScope"),
                        method.Arguments[0],
                        method.Arguments[1],
                        returnStatement.Expression);

            method.Arguments[1].Type = typeParam;

            localsWalker.AddMember(method);
        }

        PostProcessAndSave(
            new CompilationUnit()
            {
                Usings =
                {
                    new UsingDirective("Microsoft.CodeAnalysis.FlowAnalysis"),
                    new UsingDirective("Microsoft.CodeAnalysis.Operations"),
                },
                Namespaces = { ns }
            }, 
            LocalsWalkerPath);
    }

    private static bool IsLocalSymbolProperty(PropertyInfo p)
    {
        return p.PropertyType.IsAssignableTo(typeof(ILocalSymbol)) ||
                                p.PropertyType.IsAssignableTo(typeof(IEnumerable<ILocalSymbol>));
    }
}