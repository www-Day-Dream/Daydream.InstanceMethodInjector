## Instance Method Injector Preloader
A Preload Patcher that adds blank-public-instance methods as requested by any mods utilizing an assembly attribute into 'Assembly-CSharp.dll', primarily for relaying default Unity Messages such as Awake, Start, OnEnable, OnCollision, etc.. This is only needed when the target object you're patching doesn't contain a definition for a typical Unity Method as listed [here](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html).

### How to Create Methods
First include a reference to the dll located under your profile: `BepInEx/patchers/Daydream.InstanceMethodInjector`. At the top of your `Plugin.cs`, or whatever file you use as your main BepInPlugin, **ensuring you're outside of the scope of any namespaces**, you'll add your method requirements.
```csharp
using Daydream.InstanceMethodInjector;
[assembly: RequiresMethod(typeof(SomeTypeInAssemblyCSharp), "OnMessageName")]
```
If the message requires parameters then you can continue inserting `typeof(xyz)` statements in the params as follows:
```csharp                                                                    
...                                       
[assembly: RequiresMethod(typeof(SomeTypeInAssemblyCSharp), "OnMessageName", typeof(string), typeof(Action<string, bool>))]
```                                                                          
These methods turn into the following code snippets, respectively:
```csharp
public override void OnMessageName()
{
}
```
```csharp
public override void OnMessageName(string pString, Action<string, bool> pAction)
{       
}       
```     

### Naming of Parameters
You can see the methods you've created in dnSpy by opening `/BepInEx/cache/Assembly-CSharp.dll`, but generally your parameter names will be `p[TypeNamePascalCase]` and in the event of multiple parameters of the same type they will be formatted as `p[TypeNamePascaleCase]_0`, with the last number incrementing per parameter of that type.