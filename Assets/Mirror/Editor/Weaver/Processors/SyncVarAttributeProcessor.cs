using System.Collections.Generic;
using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;

namespace Mirror.Weaver
{
    // Processes [SyncVar] attribute fields in NetworkBehaviour
    // not static, because ILPostProcessor is multithreaded
    public class SyncVarAttributeProcessor
    {
        AssemblyDefinition assembly;
        WeaverTypes weaverTypes;
        SyncVarAccessLists syncVarAccessLists;
        Logger Log;

        // SyncVar<T> added

        static string HookParameterMessage(string hookName, TypeReference ValueType) =>
            $"void {hookName}({ValueType} oldValue, {ValueType} newValue)";

        public SyncVarAttributeProcessor(AssemblyDefinition assembly, WeaverTypes weaverTypes, SyncVarAccessLists syncVarAccessLists, Logger Log)
        {
            this.assembly = assembly;
            this.weaverTypes = weaverTypes;
            this.syncVarAccessLists = syncVarAccessLists;
            this.Log = Log;
        }

        // Get hook method if any
        public static MethodDefinition GetHookMethod(TypeDefinition td, FieldDefinition syncVar, Logger Log, ref bool WeavingFailed)
        {
            CustomAttribute syncVarAttr = syncVar.GetCustomAttribute<SyncVarAttribute>();

            if (syncVarAttr == null)
                return null;

            string hookFunctionName = syncVarAttr.GetField<string>("hook", null);

            if (hookFunctionName == null)
                return null;

            return FindHookMethod(td, syncVar, hookFunctionName, Log, ref WeavingFailed);
        }

        static MethodDefinition FindHookMethod(TypeDefinition td, FieldDefinition syncVar, string hookFunctionName, Logger Log, ref bool WeavingFailed)
        {
            List<MethodDefinition> methods = td.GetMethods(hookFunctionName);

            List<MethodDefinition> methodsWith2Param = new List<MethodDefinition>(methods.Where(m => m.Parameters.Count == 2));

            if (methodsWith2Param.Count == 0)
            {
                Log.Error($"Could not find hook for '{syncVar.Name}', hook name '{hookFunctionName}'. " +
                    $"Method signature should be {HookParameterMessage(hookFunctionName, syncVar.FieldType)}",
                    syncVar);
                WeavingFailed = true;

                return null;
            }

            foreach (MethodDefinition method in methodsWith2Param)
            {
                if (MatchesParameters(syncVar, method))
                {
                    return method;
                }
            }

            Log.Error($"Wrong type for Parameter in hook for '{syncVar.Name}', hook name '{hookFunctionName}'. " +
                     $"Method signature should be {HookParameterMessage(hookFunctionName, syncVar.FieldType)}",
                   syncVar);
            WeavingFailed = true;

            return null;
        }

        static bool MatchesParameters(FieldDefinition syncVar, MethodDefinition method)
        {
            // matches void onValueChange(T oldValue, T newValue)
            return method.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName &&
                   method.Parameters[1].ParameterType.FullName == syncVar.FieldType.FullName;
        }

        public MethodDefinition GenerateSyncVarGetter(FieldDefinition syncVarT, TypeReference syncVarT_ForValue, FieldDefinition originalSyncVar, string originalName)
        {
            //Create the get method
            MethodDefinition get = new MethodDefinition(
                $"get_Network{originalName}", MethodAttributes.Public |
                                              MethodAttributes.SpecialName |
                                              MethodAttributes.HideBySig,
                    originalSyncVar.FieldType);

            ILProcessor worker = get.Body.GetILProcessor();

            // [SyncVar] GameObject?

            /*if (originalSyncVar.FieldType.Is<UnityEngine.GameObject>())
            {
                // return this.GetSyncVarGameObject(ref field, uint netId);
                // this.
                Log.Warning("TODO SyncVarGameObject getter");
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldfld, netFieldId);
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldflda, originalSyncVar);
                //worker.Emit(OpCodes.Call, weaverTypes.getSyncVarGameObjectReference);
                //worker.Emit(OpCodes.Ret);
            }
            // [SyncVar] NetworkIdentity?
            else if (originalSyncVar.FieldType.Is<NetworkIdentity>())
            {
                // return this.GetSyncVarNetworkIdentity(ref field, uint netId);
                // this.
                Log.Warning("TODO SyncVarNetworkIdentity getter");
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldfld, netFieldId);
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldflda, originalSyncVar);
                //worker.Emit(OpCodes.Call, weaverTypes.getSyncVarNetworkIdentityReference);
                //worker.Emit(OpCodes.Ret);
            }
            else if (originalSyncVar.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // return this.GetSyncVarNetworkBehaviour<T>(ref field, uint netId);
                // this.
                Log.Warning("TODO SyncVarNetworkBehaviour getter");
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldfld, netFieldId);
                //worker.Emit(OpCodes.Ldarg_0);
                //worker.Emit(OpCodes.Ldflda, originalSyncVar);
                //MethodReference getFunc = weaverTypes.getSyncVarNetworkBehaviourReference.MakeGeneric(assembly.MainModule, originalSyncVar.FieldType);
                //worker.Emit(OpCodes.Call, getFunc);
                //worker.Emit(OpCodes.Ret);
            }
            // [SyncVar] int, string, etc.
            else*/
            {
                // make generic instance for SyncVar<T>.Value getter
                // so we have SyncVar<int>.Value etc.
                GenericInstanceType syncVarT_Value_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
                MethodReference syncVarT_Value_Get_ForValue = weaverTypes.SyncVarT_Value_Get_Reference.MakeHostInstanceGeneric(assembly.MainModule, syncVarT_Value_GenericInstanceType);

                // push this.SyncVar<T>.Value on stack
                // when doing it manually, this is the generated IL:
                //   IL_0001: ldfld class [Mirror]Mirror.SyncVar`1<int32> Mirror.Examples.Tanks.Test::exampleT
                //   IL_0006: callvirt instance !0 class [Mirror]Mirror.SyncVar`1<int32>::get_Value()
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, syncVarT);
                worker.Emit(OpCodes.Callvirt, syncVarT_Value_Get_ForValue);
                worker.Emit(OpCodes.Ret);
            }

            get.Body.Variables.Add(new VariableDefinition(originalSyncVar.FieldType));
            get.Body.InitLocals = true;
            get.SemanticsAttributes = MethodSemanticsAttributes.Getter;

            return get;
        }

        public MethodDefinition GenerateSyncVarSetter(FieldDefinition syncVarT, TypeReference syncVarT_ForValue, TypeDefinition originalSyncVar, FieldDefinition fd, string originalName, ref bool WeavingFailed)
        {
            //Create the set method
            MethodDefinition set = new MethodDefinition($"set_Network{originalName}", MethodAttributes.Public |
                                                                                      MethodAttributes.SpecialName |
                                                                                      MethodAttributes.HideBySig,
                    weaverTypes.Import(typeof(void)));

            ILProcessor worker = set.Body.GetILProcessor();

            /*if (fd.FieldType.Is<UnityEngine.GameObject>())
            {
                // reference to netId Field to set
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netFieldId);

                worker.Emit(OpCodes.Call, weaverTypes.setSyncVarGameObjectReference);
            }
            else if (fd.FieldType.Is<NetworkIdentity>())
            {
                // reference to netId Field to set
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netFieldId);

                worker.Emit(OpCodes.Call, weaverTypes.setSyncVarNetworkIdentityReference);
            }
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // reference to netId Field to set
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netFieldId);

                MethodReference getFunc = weaverTypes.setSyncVarNetworkBehaviourReference.MakeGeneric(assembly.MainModule, fd.FieldType);
                worker.Emit(OpCodes.Call, getFunc);
            }
            else*/
            {
                // make generic instance for SyncVar<T>.Value setter
                // so we have SyncVar<int>.Value etc.
                GenericInstanceType syncVarT_Value_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
                MethodReference syncVarT_Value_Set_ForValue = weaverTypes.SyncVarT_Value_Set_Reference.MakeHostInstanceGeneric(assembly.MainModule, syncVarT_Value_GenericInstanceType);

                // when doing this.SyncVar<T>.Value = ... manually, this is the
                // generated IL:
                //   IL_0000: ldarg.0
                //   IL_0001: ldfld class [Mirror]Mirror.SyncVar`1<int32> Mirror.Examples.Tanks.Test::exampleT
                //   IL_0006: ldarg.1
                //   IL_0007: callvirt instance void class [Mirror]Mirror.SyncVar`1<int32>::set_Value(!0)
                worker.Emit(OpCodes.Ldarg_0); // 'this.'
                worker.Emit(OpCodes.Ldfld, syncVarT);
                worker.Emit(OpCodes.Ldarg_1); // 'value' from setter
                worker.Emit(OpCodes.Callvirt, syncVarT_Value_Set_ForValue);
            }

            worker.Emit(OpCodes.Ret);

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.In, fd.FieldType));
            set.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            return set;
        }

        // ProcessSyncVar is called while iterating td.Fields.
        // can't add to it while iterating.
        // new SyncVar<T> fields are added to 'addedSyncVarTs' with
        //   <SyncVar<T>, [SyncVar] original>
        public void ProcessSyncVar(TypeDefinition td, FieldDefinition fd, Dictionary<FieldDefinition, FieldDefinition> addedSyncVarTs, ref bool WeavingFailed)
        {
            string originalName = fd.Name;

            // make generic instance of SyncVar<T> type for the type of 'value'
            // initial value is set in constructor.
            TypeReference syncVarT_ForValue = weaverTypes.SyncVarT_Type.MakeGenericInstanceType(fd.FieldType);
            FieldDefinition syncVarTField = new FieldDefinition($"___{fd.Name}SyncVarT", FieldAttributes.Public, syncVarT_ForValue);
            addedSyncVarTs[syncVarTField] = fd;

            MethodDefinition get = GenerateSyncVarGetter(syncVarTField, syncVarT_ForValue, fd, originalName);
            MethodDefinition set = GenerateSyncVarSetter(syncVarTField, syncVarT_ForValue, td, fd, originalName, ref WeavingFailed);

            //NOTE: is property even needed? Could just use a setter function?
            //create the property
            PropertyDefinition propertyDefinition = new PropertyDefinition($"Network{originalName}", PropertyAttributes.None, fd.FieldType)
            {
                GetMethod = get,
                SetMethod = set
            };

            //add the methods and property to the type.
            td.Methods.Add(get);
            td.Methods.Add(set);
            td.Properties.Add(propertyDefinition);

            // add getter/setter to replacement lists
            syncVarAccessLists.replacementSetterProperties[fd] = set;
            syncVarAccessLists.replacementGetterProperties[fd] = get;
        }

        public List<FieldDefinition> ProcessSyncVars(TypeDefinition td, Dictionary<FieldDefinition, FieldDefinition> addedSyncVarTs, ref bool WeavingFailed)
        {
            List<FieldDefinition> syncVars = new List<FieldDefinition>();

            // find syncvars
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.HasCustomAttribute<SyncVarAttribute>())
                {
                    if ((fd.Attributes & FieldAttributes.Static) != 0)
                    {
                        Log.Error($"{fd.Name} cannot be static", fd);
                        WeavingFailed = true;
                        continue;
                    }

                    if (fd.FieldType.IsArray)
                    {
                        Log.Error($"{fd.Name} has invalid type. Use SyncLists instead of arrays", fd);
                        WeavingFailed = true;
                        continue;
                    }

                    if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                    {
                        Log.Warning($"{fd.Name} has [SyncVar] attribute. SyncLists should not be marked with SyncVar", fd);
                    }
                    else
                    {
                        syncVars.Add(fd);

                        ProcessSyncVar(td, fd, addedSyncVarTs, ref WeavingFailed);
                    }
                }
            }

            // add all added SyncVar<T>s
            foreach (FieldDefinition fd in addedSyncVarTs.Keys)
            {
                td.Fields.Add(fd);
            }

            syncVarAccessLists.SetNumSyncVars(td.FullName, syncVars.Count);

            return syncVars;
        }

        // inject initialization code for SyncVar<T> from [SyncVar] into ctor
        // called from NetworkBehaviourProcessor.InjectIntoInstanceConstructor()
        // see also: https://groups.google.com/g/mono-cecil/c/JCLRPxOym4A?pli=1
        public static void InjectSyncVarT_Initialization(AssemblyDefinition assembly, ILProcessor ctorWorker, TypeDefinition td, FieldDefinition syncVarT, FieldDefinition originalSyncVar, WeaverTypes weaverTypes, Logger Log, ref bool WeavingFailed)
        {
            // find hook method in original [SyncVar(hook="func")] attribute (if any)
            MethodDefinition hookMethod = GetHookMethod(td, originalSyncVar, Log, ref WeavingFailed);

            // make generic instance of SyncVar<T> type for the type of 'value'
            TypeReference syncVarT_ForValue = weaverTypes.SyncVarT_Type.MakeGenericInstanceType(originalSyncVar.FieldType);

            // final 'StFld syncVarT' needs 'this.' in front
            ctorWorker.Emit(OpCodes.Ldarg_0);

            // push 'new SyncVar<T>(value, hook)' on stack
            ctorWorker.Emit(OpCodes.Ldarg_0);                // 'this' for this.originalSyncVar
            ctorWorker.Emit(OpCodes.Ldfld, originalSyncVar); // value = originalSyncVar
            // pass hook parameter (a method converted to an Action)
            if (hookMethod != null)
            {
                // hook can be static and instance.
                // for instance method, we need to pass 'this.' first.
                if (!hookMethod.IsStatic)
                {
                    ctorWorker.Emit(OpCodes.Ldarg_0); // 'this' for hook method
                }

                // when doing SyncVar<T> test = new SyncVar<T>(value, hook),
                // this is the IL code to convert hook to Action:
                //   ldftn instance void Mirror.Examples.Tanks.Test::OnChanged(int32, int32)
                //   newobj instance void class [netstandard]System.Action`2<int32, int32>::.ctor(object, native int)
                ctorWorker.Emit(OpCodes.Ldftn, hookMethod);

                // make generic instance of Action<T,T> type for the type of 'value'
                TypeReference actionT_T_ForValue = weaverTypes.ActionT_T_Type.MakeGenericInstanceType(originalSyncVar.FieldType, originalSyncVar.FieldType);

                // make generic ctor for Action<T,T> for the target type Action<T,T> with type of 'value'
                GenericInstanceType actionT_T_GenericInstanceType = (GenericInstanceType)actionT_T_ForValue;
                MethodReference actionT_T_Ctor_ForValue = weaverTypes.ActionT_T_GenericConstructor.MakeHostInstanceGeneric(assembly.MainModule, actionT_T_GenericInstanceType);
                ctorWorker.Emit(OpCodes.Newobj, actionT_T_Ctor_ForValue);
            }
            else
            {
                // push 'hook = null' onto stack
                ctorWorker.Emit(OpCodes.Ldnull);
            }
            // make generic ctor for SyncVar<T> for the target type SyncVar<T> with type of 'value'
            GenericInstanceType syncVarT_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
            MethodReference syncVarT_Ctor_ForValue = weaverTypes.SyncVarT_GenericConstructor.MakeHostInstanceGeneric(assembly.MainModule, syncVarT_GenericInstanceType);
            ctorWorker.Emit(OpCodes.Newobj, syncVarT_Ctor_ForValue);

            // store result in SyncVar<T> member
            ctorWorker.Emit(OpCodes.Stfld, syncVarT);
        }
    }
}
