using System.Linq;
using Mono.Cecil;

namespace Daydream.InstanceMethodInjector;

internal class ComparableMethod
{
    internal static string ArgumentsCompiled(TypeReference[] args) =>
        string.Join(", ", args?.Select((argument) => argument.Name)
            .ToArray() ?? new string[0]);

    internal string ArgumentsComp =>
        ArgumentsCompiled(Arguments.Select(attr => (TypeReference)attr.Value).ToArray());
    internal readonly string TypeName;
    internal readonly string MethodName;
    internal readonly CustomAttributeArgument[] Arguments;

    public override string ToString()
    {
        return TypeName + "." + MethodName + "(" + ArgumentsComp + ")";
    }


    internal bool IsEqual(MethodDefinition methodDef)
    {
        return MethodName == methodDef.Name &&
               TypeName == methodDef.DeclaringType.FullName &&
               ArgumentsComp ==
               ArgumentsCompiled(methodDef.Parameters
                   .Select(par => par.ParameterType).ToArray());
    }
    internal bool IsEqual(ComparableMethod other)
    {
        return TypeName == other.TypeName && MethodName == other.MethodName && ArgumentsComp == other.ArgumentsComp;
    }
    private ComparableMethod(string typeName, string methodName, CustomAttributeArgument[] parameters)
    {
        TypeName = typeName;
        MethodName = methodName;
        Arguments = parameters;
    }
    internal static ComparableMethod FromCecilConstructor(CustomAttribute attribute)
    {
        if (attribute.AttributeType.FullName != typeof(RequiresMethodAttribute).FullName)
            return null;
        return new ComparableMethod(
            ((TypeReference)attribute.ConstructorArguments[0].Value).FullName,
            (string)attribute.ConstructorArguments[1].Value,
            (CustomAttributeArgument[])attribute.ConstructorArguments[2].Value);
    }
}