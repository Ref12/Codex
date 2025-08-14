using System.Collections.Immutable;
using System.Reflection;
using Codex.Utilities;
using Meziantou.Framework.CodeDom;
using Dom = Meziantou.Framework.CodeDom;

namespace Codex.Framework.Generator
{
    public class TypeDefinition
    {
        public TypeDefinition(Type type, GeneratorContext context)
        {
            Type = type;
            Context = context;

            BaseName = type.Name.StartsWith('I') ? type.Name.Substring(1) : type.Name;

            ModelDeclaration = new ClassDeclaration(BaseName)
            {
                Modifiers = Modifiers.Public | Modifiers.Partial
            };

            ModelDeclaration.Members.Add(new MethodDeclaration(nameof(ICreate<Ent>.Create))
            {
                Modifiers = Modifiers.Static,
                ReturnType = ModelDeclaration,
                PrivateImplementationType = new TypeReference(nameof(ICreate<Ent>)).MakeGeneric(ModelDeclaration),
                Statements = new()
                {
                    new ReturnStatement(new NewObjectExpression(ModelDeclaration))
                }
            });

            //ModelDeclaration.Members.Add(new MethodDeclaration(nameof(IEntity<Ent, IEnt>.Create))
            //{
            //    Modifiers = Modifiers.Static,
            //    ReturnType = ModelDeclaration,
            //    PrivateImplementationType = new TypeReference(nameof(IEntity<Ent, IEnt>)).MakeGeneric(ModelDeclaration, type),
            //    Arguments =
            //    {
            //        new MethodArgumentDeclaration(type, "value"),
            //        new MethodArgumentDeclaration(typeof(bool), "shallow")
            //    },
            //    Statements = new()
            //    {
            //        new ReturnStatement(new NewObjectExpression(ModelDeclaration, new ArgumentReferenceExpression("value"), new ArgumentReferenceExpression("shallow")))
            //    }
            //});

            var st = typeof(SingletonDescriptorBase<,,>);
            DescriptorDeclaration = new ClassDeclaration(BaseName + "Descriptor")
            {
                Modifiers = Modifiers.Public | Modifiers.Partial,
                BaseType = new TypeReference(st.Name.AsSpan().SubstringBeforeFirstIndexOfAny("`").ToString()).MakeGeneric(ModelDeclaration, type, new TypeReference(BaseName + "Descriptor")),
            };

            IEntityType = new TypeReference(nameof(IEntity<Ent, IEnt>)).MakeGeneric(ModelDeclaration, type, DescriptorDeclaration);

            var icreate = new TypeReference(typeof(ICreate<>)).MakeGeneric(DescriptorDeclaration);
            DescriptorDeclaration.Implements.Add(icreate);
            DescriptorConstructor = new ConstructorDeclaration();
            //DescriptorDeclaration.Members.Add(new FieldDeclaration("Instance", DescriptorDeclaration)
            //{
            //    InitExpression = new NewObjectExpression(DescriptorDeclaration),
            //    Modifiers = Modifiers.Public | Modifiers.ReadOnly | Modifiers.Static
            //});

            DescriptorDeclaration.Members.Add<CodeMemberMethod>(new CodeMemberMethod(nameof(ICreate<int>.Create))
            {
                Modifiers = Modifiers.Static,
                ReturnType = DescriptorDeclaration,
                Statements = new GeneratorContext.ExpressionBodyStatementCollection()
                {
                    BodyExpression = new Dom.NewObjectExpression(DescriptorDeclaration)
                },
                PrivateImplementationType = icreate
            });
            DescriptorDeclaration.Members.Add(DescriptorConstructor);

            IsAdapter = type.GetCustomAttribute<AdapterTypeAttribute>() != null;
            Exclude = type.GetCustomAttribute<GeneratorExcludeAttribute>();

            if (ShouldGenerate)
            {
                context.ModelNamespace.Types.Add(ModelDeclaration);
                context.DescriptorsClass.AddType(DescriptorDeclaration);
            }
        }

        public IEnumerable<Type> GetBaseInterfaces(bool generatedOnly = false)
        {
            return Type.GetInterfaces().Where(t => Context.Types.TryGetValue(t.IsGenericType ? t.GetGenericTypeDefinition() : t, out var baseDef) && (!generatedOnly || baseDef.ShouldGenerate));
        }

        public Type Type { get; }
        public GeneratorContext Context { get; }
        public string BaseName { get; }
        public TypeReference IEntityType { get; }
        public ImmutableDictionary<string, PropertyData> Properties { get; set; }
        public ImmutableHashSet<Type> Interfaces { get; set; }

        public MemberDeclaration CopyMethodDeclaration { get; set; }
        public ClassDeclaration ModelDeclaration { get; }
        public ClassDeclaration DescriptorDeclaration { get; }
        public ConstructorDeclaration DescriptorConstructor { get; }
        public bool IsAdapter { get; }
        public bool IsExcluded => Exclude?.IncludeProperties == false;

        private GeneratorExcludeAttribute Exclude { get; }

        public bool ShouldGenerate => !IsExcluded && !IsAdapter && !Type.IsGenericType && Exclude == null;
        public TypeDefinition BaseDefinition { get; set; }
    }

    public record PropertyData(PropertyInfo Info)
    {
        public int FieldNumber { get; set; }
        public string Name => Info.Name;
        public Func<Expression> GetAddExpression { get; set; }
    }
}