using System;

namespace Daydream.InstanceMethodInjector;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class RequiresMethodAttribute : Attribute
{
    private readonly Type type;
    private readonly string methodName;
    private readonly Type[] parameterTypes;
    public RequiresMethodAttribute(Type type, string methodName, params Type[] parameterTypes)
    {
        this.type = type;
        this.methodName = methodName;
        this.parameterTypes = parameterTypes;
    }
}