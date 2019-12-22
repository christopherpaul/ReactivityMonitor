using ReactivityProfiler.Support.Store;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support
{
    public static class Instrument
    {
        // Class constructor gives us an opportunity to fire up our server
        static Instrument()
        {
            try
            {
                var server = new Server.Server(Services.Store);
                server.Start();
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0}: failed to launch server: {1}", typeof(Instrument).FullName, ex);
            }
        }

        /// <summary>
        /// Per-thread tracking of instrumentation calls.
        /// </summary>
        private sealed class CallTracker
        {
            private readonly List<IObservableInput> mInfoList = new List<IObservableInput>();
            private readonly Stack<(int InstrPoint, IReadOnlyList<IObservableInput> Inputs)> mCallStack = new Stack<(int InstrPoint, IReadOnlyList<IObservableInput> Inputs)>();
            private int mCurrentInstrPoint;

            public void AddInput(int instrumentationPoint, IObservableInput obsInfo)
            {
                if (instrumentationPoint != mCurrentInstrPoint)
                {
                    mInfoList.Clear();
                    mCurrentInstrPoint = instrumentationPoint;
                }
                mInfoList.Add(obsInfo);
            }

            public void Calling(int instrumentationPoint)
            {
                if (instrumentationPoint != mCurrentInstrPoint)
                {
                    mInfoList.Clear();
                }
                mCallStack.Push((instrumentationPoint, mInfoList.ToArray()));
                mInfoList.Clear();
            }

            public IReadOnlyList<IObservableInput> Returned(int instrumentationPoint)
            {
                (int InstrPoint, IReadOnlyList<IObservableInput> Inputs) entry;
                do
                {
                    if (mCallStack.Count == 0)
                    {
                        return new ObservableInfo[0];
                    }

                    entry = mCallStack.Pop();
                }
                while (entry.InstrPoint != instrumentationPoint);

                return entry.Inputs;
            }
        }

        private static ThreadLocal<CallTracker> sTracker = new ThreadLocal<CallTracker>(() => new CallTracker());

        private static class ArgumentTypeSpecialisation<T>
        {
            public static readonly Func<T, int, T> ArgHandler;

            static ArgumentTypeSpecialisation()
            {
                var argType = typeof(T);

                // Check for T = IObservable<U>
                if (IsObservable(argType, out _))
                {
                    var handlerType = typeof(ObservableArgHandler<>).MakeGenericType(argType);
                    ArgHandler = (Func<T, int, T>)((IObservableArgHandler)Activator.CreateInstance(handlerType)).GetHandler();
                }

                // Check for T = delegate returning IObservable<U>
                if (typeof(Delegate).IsAssignableFrom(argType))
                {
                    var invokeMethod = argType.GetMethod("Invoke");
                    var returnType = invokeMethod.ReturnType;
                    if (IsObservable(returnType, out Type observableItemType))
                    {
                        var attacherType = typeof(DynamicObservableAttacher<>).MakeGenericType(observableItemType);
                        var delegateArgParams = invokeMethod.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
                        var handlerArgParam = Expression.Parameter(argType);
                        var handlerIpParam = Expression.Parameter(typeof(int));
                        var attacherVar = Expression.Parameter(attacherType);
                        var observableVar = Expression.Parameter(returnType);

                        Expression callAttachOnReturnedObservable = Expression.Call(
                                                attacherVar,
                                                attacherType.GetMethod("Attach"),
                                                observableVar);

                        if (returnType != typeof(IObservable<>).MakeGenericType(observableItemType))
                        {
                            // subinterface, so need to wrap the result from the call to Attach
                            var wrapperFunction = GetDerivedInterfaceWrapper(returnType);
                            callAttachOnReturnedObservable =
                                Expression.Convert(
                                    Expression.Invoke(
                                        Expression.Constant(wrapperFunction),
                                        observableVar,
                                        callAttachOnReturnedObservable),
                                    returnType);
                        }

                        var handlerExpression =
                            Expression.Lambda<Func<T, int, T>>(
                                Expression.Block(new[] {attacherVar},
                                    Expression.Assign(attacherVar, Expression.New(attacherType)),
                                    Expression.Call(
                                        Expression.Property(Expression.Constant(sTracker), "Value"),
                                        typeof(CallTracker).GetMethod("AddInput"),
                                        handlerIpParam, attacherVar),
                                    Expression.Lambda(argType,
                                        Expression.Block(new[] {observableVar},
                                            Expression.Assign(observableVar,
                                                Expression.Invoke(handlerArgParam, delegateArgParams)),
                                            callAttachOnReturnedObservable),
                                        delegateArgParams)),
                                handlerArgParam, handlerIpParam);
                        ArgHandler = handlerExpression.Compile();
                    }
                }
            }

            private static bool IsObservable(Type argType, out Type observableItemType)
            {
                if (!argType.IsInterface)
                {
                    observableItemType = null;
                    return false;
                }

                if (argType.IsGenericType && argType.GetGenericTypeDefinition() == typeof(IObservable<>))
                {
                    observableItemType = argType.GenericTypeArguments[0];
                    return true;
                }

                var ifaces = argType.FindInterfaces((i, _) => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IObservable<>), null);
                if (ifaces.Length == 1)
                {
                    observableItemType = ifaces[0].GenericTypeArguments[0];
                    return true;
                }

                observableItemType = null;
                return false;
            }
        }

        private interface IObservableArgHandler
        {
            object GetHandler();
        }

        private sealed class ObservableArgHandler<T> : IObservableArgHandler
        {
            public object GetHandler() => new Func<T, int, T>(HandleArg);

            public T HandleArg(T observable, int instrumentationPoint)
            {
                if (observable is IInstrumentedObservable instrumented)
                {
                    sTracker.Value.AddInput(instrumentationPoint, (ObservableInfo)instrumented.Info);
                }
                return observable;
            }
        }


        /// <summary>
        /// Called for each IObservable argument of an instrumented method call.
        /// </summary>
        public static T Argument<T>(T argValue, int instrumentationPoint)
        {
            var handler = ArgumentTypeSpecialisation<T>.ArgHandler;
            if (handler == null)
            {
                return argValue;
            }

            return handler(argValue, instrumentationPoint);
        }

        /// <summary>
        /// Called just before an instrumented method call (after any <see cref="Argument"/> calls).
        /// </summary>
        public static void Calling(int instrumentationPoint)
        {
            sTracker.Value.Calling(instrumentationPoint);
        }

        /// <summary>
        /// Called after an instrumented method call returns.
        /// </summary>
        public static IObservable<T> Returned<T>(IObservable<T> observable, int instrumentationPoint)
        {
            var inputs = sTracker.Value.Returned(instrumentationPoint);

            if (observable == null)
            {
                return observable;
            }

            if (observable is IInstrumentedObservable)
            {
                // Already instrumented. Not sure if we'd want to associate it with any
                // inputs to this call as well - for now assume not.
                return observable;
            }

            var obsInfo = Services.Store.CreateObservable(instrumentationPoint);
            foreach (var input in inputs)
            {
                input.AssociateWith(obsInfo);
            }

            return new InstrumentedObservable<T>(observable, obsInfo);
        }

        private static readonly ConcurrentDictionary<RuntimeTypeHandle, Func<object, object, object>> cDerivedInterfaceWrappers =
            new ConcurrentDictionary<RuntimeTypeHandle, Func<object, object, object>>();

        private static readonly ConcurrentDictionary<Type, Type> cGenericDerivedInterfaceWrapperTypes =
            new ConcurrentDictionary<Type, Type>();

        private static readonly Func<RuntimeTypeHandle, Func<object, object, object>> cCreateDerivedInterfaceWrapper = typeHandle =>
        {
            // We only have to handle a limited number of known derived interfaces. Constraints:
            // - interface must derive from IObservable exactly once
            // - interface must have a generic type argument that is used as the type arg for its IObservable base

            var type = Type.GetTypeFromHandle(typeHandle);
            Debug.Assert(type.IsInterface, "interface");
            Debug.Assert(type.IsGenericType, "generic");

            var genericTypeDef = type.GetGenericTypeDefinition();

            var genericWrapperType = cGenericDerivedInterfaceWrapperTypes.GetOrAdd(genericTypeDef, CreateGenericWrapperType);

            var wrapperType = genericWrapperType.MakeGenericType(type.GenericTypeArguments);

            return (original, instrumented) => Activator.CreateInstance(wrapperType, original, instrumented);
        };

        private static Type CreateGenericWrapperType(Type genericTypeDef)
        {
            Debug.WriteLine($"CreateGenericWrapperType: {genericTypeDef}");

            var observableBaseType = genericTypeDef.GetInterface("System.IObservable`1");
            Debug.Assert(observableBaseType != null, "derives from IObservable");
            Debug.Assert(observableBaseType.IsGenericType, "IObservable is generic");
            var observableTypeArg = observableBaseType.GenericTypeArguments[0];
            Debug.Assert(observableTypeArg.IsGenericParameter, "IObservable type arg is a type param of derived interface");

            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(Guid.NewGuid().ToString()),
                AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(Guid.NewGuid().ToString());
            Debug.WriteLine($"CreateGenericWrapperType: creating type builder");
            var typeBuilder = moduleBuilder.DefineType(
                Guid.NewGuid().ToString(),
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(InstrumentedObservableProxy));

            Debug.WriteLine($"CreateGenericWrapperType: defining generic params");
            var genericParams = typeBuilder.DefineGenericParameters(genericTypeDef.GetGenericArguments().Select(a => a.Name).ToArray());

            Debug.WriteLine($"CreateGenericWrapperType: creating interface instantiation to be implemented");
            var implementedInterface = genericTypeDef.MakeGenericType(genericParams);
            Debug.WriteLine($"CreateGenericWrapperType: adding said interface");
            typeBuilder.AddInterfaceImplementation(implementedInterface);

            Debug.WriteLine($"CreateGenericWrapperType: creating instantiation of IObservable to be implemented");
            var observableInterface = typeof(IObservable<>).MakeGenericType(genericParams[observableTypeArg.GenericParameterPosition]);

            Debug.WriteLine($"CreateGenericWrapperType: creating fields");
            var originalField = typeBuilder.DefineField("original", implementedInterface, FieldAttributes.Private | FieldAttributes.InitOnly);
            var instrumentedField = typeBuilder.DefineField("instrumented", observableInterface, FieldAttributes.Private | FieldAttributes.InitOnly);

            Debug.WriteLine($"CreateGenericWrapperType: defining constructor");
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.HasThis,
                new[] { implementedInterface, observableInterface });
            var constructorIl = constructor.GetILGenerator();
            constructorIl.Emit(OpCodes.Ldarg_0); // load this
            constructorIl.Emit(OpCodes.Ldarg_2); // load 2nd arg (instrumented observable)
            constructorIl.Emit(OpCodes.Call, typeof(InstrumentedObservableProxy).GetConstructors().First()); // call base constructor
            constructorIl.Emit(OpCodes.Ldarg_0); // load this
            constructorIl.Emit(OpCodes.Ldarg_1); // load 1st arg (original observable)
            constructorIl.Emit(OpCodes.Stfld, originalField); // store 1st arg in field
            constructorIl.Emit(OpCodes.Ldarg_0); // load this
            constructorIl.Emit(OpCodes.Ldarg_2); // load 2st arg (instrumented observable)
            constructorIl.Emit(OpCodes.Stfld, instrumentedField); // store 2st arg in field
            constructorIl.Emit(OpCodes.Ret);

            Debug.WriteLine($"CreateGenericWrapperType: generating implementations for interface");
            foreach (var iface in genericTypeDef.GetInterfaces().Concat(new[] { genericTypeDef }))
            {
                foreach (var member in iface.GetMembers())
                {
                    Debug.WriteLine($"CreateGenericWrapperType: member {member}");
                    if (member.MemberType == MemberTypes.Method)
                    {
                        var method = (MethodInfo)member;
                        var methodBuilder = typeBuilder.DefineMethod(
                            method.Name,
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            method.ReturnType,
                            method.GetParameters().Select(p => p.ParameterType).ToArray());

                        var methodIl = methodBuilder.GetILGenerator();
                        methodIl.Emit(OpCodes.Ldarg_0); // load this
                        if (iface == observableBaseType)
                        {
                            methodIl.Emit(OpCodes.Ldfld, instrumentedField);
                        }
                        else
                        {
                            methodIl.Emit(OpCodes.Ldfld, originalField);
                        }
                        int argCount = method.GetParameters().Length;
                        for (int arg = 0; arg < argCount; arg++)
                        {
                            methodIl.Emit(OpCodes.Ldarg, arg + 1); // 0 is this
                        }

                        methodIl.Emit(OpCodes.Callvirt, method);
                        methodIl.Emit(OpCodes.Ret);

                        typeBuilder.DefineMethodOverride(methodBuilder, method);
                    }
                }
            }

            Debug.WriteLine($"CreateGenericWrapperType: creating type");
            return typeBuilder.CreateTypeInfo();
        }

        private static Func<object, object, object> GetDerivedInterfaceWrapper(RuntimeTypeHandle typeHandle)
        {
            return cDerivedInterfaceWrappers.GetOrAdd(typeHandle, cCreateDerivedInterfaceWrapper);
        }

        private static Func<object, object, object> GetDerivedInterfaceWrapper(Type type) => GetDerivedInterfaceWrapper(type.TypeHandle);

        public static IObservable<T> ReturnedSubinterface<T>(IObservable<T> observable, int instrumentationPoint, RuntimeTypeHandle constructedTypeHandle)
        {
            var instrumented = Returned(observable, instrumentationPoint);
            if (ReferenceEquals(instrumented, observable))
            {
                return observable;
            }

            var wrapperFunction = GetDerivedInterfaceWrapper(constructedTypeHandle);
            return (IObservable<T>)wrapperFunction(observable, instrumented);
        }
    }
}
