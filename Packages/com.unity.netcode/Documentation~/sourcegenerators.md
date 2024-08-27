# Netcode for Entities Source Generators

The Netcode for Entities package uses Roslyn SourceGenerator to automatically generate at compile time:
- All the serialization code for replicated Components and Buffers, ICommand, Rpc and IInputCommandData.
- All the necessary boiler template systems that handle Rpc and Commands handling
- Systems that copy to/from from IInputCommandData to the underlying ICommand buffer 
- Other internal system (mostly used for registration of the replicated types).
- Extract all the information from the replicate types to avoid using reflection at runtime.

The project is organized as follow:

```
Unity.NetCode
- Editor
- Runtime
  -- SourceGenerators      Labels
  --- NetCodeGenerator.dll  *SourceGenerator*
  ---- Source~  (hidden, not handled by Unity)
  ------ NetCodeSourceGenerator
  ------- CodeGenerator
  ------- Generators
  ------- Helpers
  ------ Tests
  ------ SourceGenerators.sln  
```

The NetCodeSourceGenerator.dll is generated from the Source~ folder and used by the Editor compilation pipeline to inject the generate code in each assemply definitions (and also Assembly-CSharp and similar).
> IMPORTANT
> 
> The generator dll is quite special and as some specific requirements:
> 1) It MUST be not imported by Unity Editor or any platform (incompatibility)
> 2) In order to be detected by the compilation pipeline and used as generator it MUST be labelled with the 'SourceGenerator' label.

The dll present in the package is alredy configured appropiately. However, in case after recompiling the dll, some of the settings are lost 
you can either use the Editor, edit the meta file, or restore the previous meta file, to reset the settings.

## Generator output
By default, the Netcode generators emits all the generated files in the `Temp/NetcodeGenerated` folder (accessible also from the MultiplayerMenu shortcut).
A sub-folder is created for each assembly for which serialization code as been generated.

The generator also write all the info/debug logs inside the `Temp/NetcodeGenerated/sourcegenerator.log`. Errors and Warning are emitted also in the Editor console.

## Configuring the files and logging generator behaviour
It is possible to configure the generator using the Roslyn Analyzer Config file. Unity 2022+ detect the presence of GlobalAnalyzerConfig assets, either global (root of the Assets folder) 
or on per assembly definition level, similarly to the .buildrule files.  

In order to configure the options to pass to our generator, it is necessary to create a `Default.globalconfig` text file should be added to the `Assets` folder in your project.</br>
The file must contains a list of key/value pairs and must have the following format:

```
# you can write comment like this
is_global=true

your_key=your value
your_key=your value
...
```
More information about the format and the analyzer configuration be found in the [Global Analyzer Config](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files#global-analyzerconfig) microsoft documentation.

The Netcode generators supports the following flags/keys:

| Key                                               | Value                            | Description                                                                                                                                                                        |
|---------------------------------------------------|----------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| unity.netcode.sourcegenerator.outputfolder        | a valid relative string          | Override the output folder where the generator flush logs and generated files. Should be relative to the project path. Default is Temp/NetCodeGenerated.                           |
| unity.netcode.sourcegenerator.write_files_to_disk | empty or 1 (enable). 0 (disable) | Enable/Disable the output generated files to output folder                                                                                                                         |
| unity.netcode.sourcegenerator.write_logs_to_disk  | empty or 1 (enable). 0 (disable) | Enable/Disable writing the logs to the output folder. All logs are redirected to the Editor logs if disabled                                                                       |
| unity.netcode.sourcegenerator.emit_timing         | empty or 1 (enable). 0 (disable) | Logs timings information for each compiled assembly.                                                                                                                               |
| unity.netcode.sourcegenerator.logging_level       | info, warning, error             | Set the logging level to use. **Default is error**.                                                                                                                                |
| unity.netcode.sourcegenerator.attach_debugger     | an optional assembly name        | Stop the generator execution and wait for a debugger to be attached. If assembly name is non empty, the generator wait for the debugger only when the assembly is being processed. |

## How to build the source generators
There are cases when you may need to recompile the generator that come with package. For example, to fix an issue or to extend them.

The generator DLLs need to be compiled manually outside of the Unity using the .NET SDK 6.0 or higher: https://dotnet.microsoft.com/en-us/download/dotnet/6.0. 
That can be done with dotnet from within the Packages\com.unity.netcode\Runtime\SourceGenerators\Source~ directory via command prompt.

`dotnet publish -c Release` to compile a release build </br>
`dotnet publish -c Debug` to compile a debug build. (recommended for debugging)

Additionally, they can be built/debugged using the provide **Packages/com.unity.netcode/Runtime/SourceGenerators/Source~/SourceGenerators.sln** solution.

## How to debug generator problems

Debugging source generators can a little hard at first. The generator execution is invoked by an external process and you need to attach the debugger to it in order to be able to step throuhg the code.

The first step is to open the SourceGenerators.sln in either Rider or VisualStudio and recompile the generator using the [**Debug configuration**](How to build the source generators).

To simplify the process of attaching the debugger when generator is invoked, we provide some utilities that let you attach the debugger to the running process in a controllable manner.

### Using the global config

By adding the "unity.netcode.sourcegenerator.attach_debugger" option, you can let generator wait for a debugger to be attached, to either all the invocation or for a specific assembly.

### Modify the generator code
You can use the `Debug.LaunchDebugger` utility method
```csharp
// Launch the debugger inconditionally
Debug.LaunchDebugger()
// Launch the debugger if the current processed assembly match the name 
Debug.LaunchDebugger(GeneratorExecutionContext context, string assembly)
```
These helper methods can be invoked/called from any place. We suggest to start from the NetcodeSourceGenetator.cs, inside the `Execute` method. 

```csharp
public void Execute(GeneratorExecutionContext executionContext)
{
    ....
    Debug.LaunchDebugger();
    try
    {
        Generate(executionContext, diagnostic);
    }
    catch (Exception e)
    {
       ...
    }
```

>Note:
> Because the execute is invoked multiple time (one per assembly) if your are not using the assembly filter, multiple popup will show-up on your screen.

In all cases, a dialog will open at the right time, stating which is the process id you should attach to.
