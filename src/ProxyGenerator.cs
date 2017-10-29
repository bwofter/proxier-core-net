/*  Copyright 2017 B. Wofter
    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions 
    are met:
    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the
    documentation and/or other materials provided with the distribution.
    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this
    software without specific prior written permission.
    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
    LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
    IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
    CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
    OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
    (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH 
    DAMAGE.*/
namespace Proxier.Core
{
    using Annotations;
    using Annotations.Contracts;
    using Proxier.Core.Internals;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;
    /// <summary>
    /// <para>
    /// Description:
    /// </para>
    /// <para>
    /// A class that generates proxied instances of <typeparamref name="T"/>. <typeparamref name="T"/> must be marked with the 
    /// <see cref="ProxiedAttribute"/> annotation to have a proxy generated for it. Otherwise, this class will always return
    /// instances of <typeparamref name="T"/> itself. This is to allow the generator to be safely used without causing trouble
    /// if <see cref="ProxiedAttribute"/> is ever removed from a type.
    /// </para>
    /// <para>
    /// Additional annotations are provided to define how a class's generator should generate the proxy. These attributes
    /// can be found in the <see cref="Annotations"/> namespace.
    /// </para>
    /// <para>
    /// IL can be injected into the <see cref="ILGenerator"/> used during proxy generation through the use of
    /// the <see cref="ProxierAnnotationAttribute"/> base type. This type defines the
    /// <see cref="ProxierAnnotationAttribute.Inject(ILGenerator)"/> that will be called during the generation
    /// process in the position defined by <see cref="ProxierAnnotationAttribute.IsBeforeCall"/>. More information
    /// on this can be found in the documentation of the <see cref="ProxierAnnotationAttribute"/> type.
    /// </para>
    /// </summary>
    /// <typeparam name="T">A type marked with the <see cref="ProxiedAttribute"/> type.</typeparam>
    public sealed class ProxyGenerator<T> where T : class, new()
    {
        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Returns true if <typeparamref name="T"/> is marked with the <see cref="ObservableAttribute"/> annotation and if
        /// <see cref="IsProxyable"/> returns true.
        /// </para>
        /// </summary>
        public bool IsObservable { get; } = false;
        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Returns true if <typeparamref name="T"/> is marked with the <see cref="ProxiedAttribute"/> annotation.
        /// </para>
        /// </summary>
        public bool IsProxyable { get; } = false;
        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Returns true if <typeparamref name="T"/> is marked with the <see cref="WrappedAttribute"/> annotation and if
        /// <see cref="IsProxyable"/> returns true.
        /// </para>
        /// </summary>
        public bool IsWrapped { get; } = false;

        private IReadOnlyList<Attribute> Attributes { get; }
        private TypeInfo BaseType { get; } = typeof(T).GetTypeInfo();
        private Lazy<Func<T, T>> Extractor { get; }
        private Lazy<Func<T>> Generator { get; }
        private IReadOnlyDictionary<MethodInfo, IReadOnlyList<Attribute>> MethodAttributes { get; }
        private MethodInfo OnChangeCallable { get; set; }
        private MethodInfo OnObserveCallable { get; set; }
        private IReadOnlyDictionary<PropertyInfo, IReadOnlyList<Attribute>> PropertyAttributes { get; }
        private TypeInfo ProxyType { get; set; }
        private string ProxyTypeName =>
            GenerateOrGetProxyType().FullName;

        private static Lazy<ProxyGenerator<T>> Singleton { get; } =
            new Lazy<ProxyGenerator<T>>(() => new ProxyGenerator<T>(), true);

        static ProxyGenerator() { }
        private ProxyGenerator()
        {
            //Listify this class's attributes and determine whether or not a proxied annotation has been attached to the class.
            Attributes = BaseType.GetCustomAttributes().ToList();
            IsProxyable = Attributes.Any(a => a is ProxiedAttribute) && !BaseType.IsSealed;
            //Initialize the lazy loaders for the generator and extractor and mark them for thread safety.
            Generator = new Lazy<Func<T>>(() =>
            {
                //Get the constructor from the proxy type, then generate the new expression and compile it to a lambda.
                ConstructorInfo c = GenerateOrGetProxyType().GetConstructor(Type.EmptyTypes);
                return Expression.Lambda<Func<T>>(Expression.New(c)).Compile();
            }, true);
            Extractor = new Lazy<Func<T, T>>(() =>
            {
                //Initializes a parameter expression of the type T, then convert it to the generic type. We do this to so that
                //the lambda is strictly typed and we don't need to call the dynamic invoke method.
                ParameterExpression p = Expression.Parameter(typeof(T));
                Expression<Func<T, T>> fu = null;
                if (IsWrapped)
                {
                    FieldInfo m = GenerateOrGetProxyType().GetField(ProxyWrappedName(GenerateOrGetProxyType()), BindingFlags.NonPublic | BindingFlags.Instance);
                    BinaryExpression t = Expression.Assign(p, Expression.MakeMemberAccess(Expression.Convert(p, GenerateOrGetProxyType()), m));
                    BinaryExpression f = Expression.Assign(p, p);
                    ConditionalExpression co = Expression.IfThenElse(Expression.TypeIs(p, GenerateOrGetProxyType()), t, f);
                    fu = Expression.Lambda<Func<T, T>>(Expression.Block(co, p), p);
                }
                else
                {
                    fu = Expression.Lambda<Func<T, T>>(p, p);
                }
                return fu.Compile();
            });
            //If the object has the proxied annotation the class is a valid type for proxying and needs its information generated.
            if (IsProxyable)
            {
                Dictionary<MethodInfo, IReadOnlyList<Attribute>> methodAttributes = new Dictionary<MethodInfo, IReadOnlyList<Attribute>>();
                Dictionary<PropertyInfo, IReadOnlyList<Attribute>> propertyAttributes = new Dictionary<PropertyInfo, IReadOnlyList<Attribute>>();
                MethodInfo[] methods = BaseType.GetMethods();
                PropertyInfo[] properties = BaseType.GetProperties();
                //Sets that the class is an observable if the observable attribute is present.
                IsObservable = Attributes.Any(a => a is ObservableAttribute);
                //Sets that the class is wrapped if the wrapped attribute is present.
                IsWrapped = Attributes.Any(a => a is WrappedAttribute);
                //Walks the method definitions of the proxied type. If the method is virtual it is added to the dictionary.
                foreach (var method in methods)
                {
                    IReadOnlyList<Attribute> attributes = method.GetCustomAttributes().ToArray();
                    if (method.IsVirtual && !method.IsSpecialName && method.DeclaringType != typeof(object))
                    {
                        methodAttributes.Add(method, attributes);
                    }
                    //If the method isn't virtual and there are no observe methods already declared, attempt to find them and
                    //add them. Currently the prox generator doesn't support abstract or overloaded methods for observe methods.
                    else if (!method.IsSpecialName && !method.IsVirtual && (OnChangeCallable == null || OnObserveCallable == null))
                    {
                        if (OnChangeCallable == null && attributes.Any(a => a is OnChangeAttribute))
                        {
                            OnChangeCallable = method;
                        }
                        else if (OnObserveCallable == null && attributes.Any(a => a is OnObserveAttribute))
                        {
                            OnObserveCallable = method;
                        }
                    }
                }
                //Set the property to reference this dictionary.
                MethodAttributes = methodAttributes;
                //Walks the property definitions of the proxied type. If the property has at least 1 virtual accessor
                //it is added to the dictionary.
                foreach (var property in properties)
                {
                    if (property.CanRead && property.GetGetMethod().IsVirtual ||
                        property.CanWrite && property.GetSetMethod().IsVirtual)
                    {
                        propertyAttributes.Add(property, property.GetCustomAttributes().ToArray());
                    }
                }
                //Set the property to reference this dictionary.
                PropertyAttributes = propertyAttributes;
            }
        }

        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Gets the base instance of a wrapped type. This will return the value of <paramref name="extractable"/> if
        /// <see cref="IsWrapped"/> is false or <see cref="IsProxied(T)"/> returns false.
        /// </para>
        /// </summary>
        /// <param name="extractable">A wrapped <see cref="T"/> instance.</param>
        /// <returns>Either the wrapped <see cref="T"/> instance if <see cref="IsWrapped"/> is true and <see cref="IsProxied(T)"/>
        /// returns true, the value of <paramref name="extractable"/> otherwise.</returns>
        public T Extract(T extractable) =>
            IsWrapped && IsProxied(extractable) ? Extractor.Value(extractable) : extractable;
        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Checks if the instance is an instance of the proxy type for <typeparamref name="T"/>. This will return false if
        /// the instance is not of the proxy type or if no proxy type was generated.
        /// </para>
        /// </summary>
        /// <param name="proxyable">An instance of <typeparamref name="T"/> or null.</param>
        /// <returns>True if <paramref name="proxyable"/> is not null, <typeparamref name="T"/> is not equal to the proxy
        /// type, and <see cref="Type.IsInstanceOfType(object)"/> returns true, false otherwise.</returns>
        public bool IsProxied(T proxyable) =>
            IsProxyable && GenerateOrGetProxyType().IsInstanceOfType(proxyable);
        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Gets a new instance of the proxy type for <typeparamref name="T"/>. If <typeparamref name="T"/> is not marked with
        /// <see cref="ProxiedAttribute"/> then an instance of <typeparamref name="T"/> is returned instead.
        /// </para>
        /// </summary>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        public T New() =>
            Generator.Value();

        private void GenerateInjection(int i, ILGenerator g, ProxierAnnotationAttribute a)
        {
            //Determines if the attribute is a parameter assertion and, if so, sets the parameter index.
            if (a is IParameterAttribute ia)
            {
                ia.Index = i + 1;
            }
            //Calls the injector of the annotation to inject in the code needed.
            a.Inject(g);
        }
        private void GenerateInjections(ILGenerator g, IReadOnlyList<Attribute> attributes, bool isBeforeCall)
        {
            //Walks the attributes provided and, if they are a proxier annotation attribute, calls their injection method.
            foreach (Attribute a in attributes)
            {
                if (a is ProxierAnnotationAttribute p)
                {
                    //Determines if the proxier annotation attribute's is before call bool that is equal to the is before call
                    //parameter and, if so, injects the IL.
                    if (p.IsBeforeCall == isBeforeCall)
                    {
                        p.Inject(g);
                    }
                }
            }
        }
        private MethodBuilder GenerateMethod(TypeBuilder t, MethodInfo bm, FieldBuilder f, IReadOnlyList<Attribute> attributes = null)
        {
            //Defines the dynamic method based directly off of the base method, then gets its parameters and return info.
            MethodBuilder m = t.DefineMethod(bm.Name, bm.Attributes & ~System.Reflection.MethodAttributes.NewSlot);
            IReadOnlyList<ParameterInfo> parameters = bm.GetParameters().ToArray();
            ParameterInfo rParameter = bm.ReturnParameter;
            //Sets the parameters and return type to the types expected by the parent and defines the dynamic method as a direct
            //override of the base method.
            m.SetParameters(parameters.Select(r => r.ParameterType).ToArray());
            m.SetReturnType(bm.ReturnType);
            t.DefineMethodOverride(m, bm);
            //Gets the IL generator for the method, generates the parameter IL injections and loads this onto the stack.
            ILGenerator g = m.GetILGenerator();
            //Determines if no attributes have been provided. IF they haven't, specifically generate method injections. Otherwise,
            //this is a property, so generate injections based off of the attributes list.
            if (attributes == null)
            {
                GenerateMethodInjections(g, bm, true);
            }
            else
            {
                GenerateInjections(g, attributes, true);
            }
            //Determines if the method is an accessor and the class is an observable. If both are true, determines if the method
            //is the setter and that on change callable is set. If both are true, inject the on change. Otherwise, determine if
            //the return type isn't null and on observe callable is set. If both are true, inject the on observe.
            if (bm.IsSpecialName && attributes != null && IsObservable)
            {
                if (bm.ReturnType == typeof(void) && OnChangeCallable != null)
                {
                    GenerateObserveInjection(g, bm, OnChangeCallable);
                }
                else if (bm.ReturnType != typeof(void) && OnObserveCallable != null)
                {
                    GenerateObserveInjection(g, bm, OnObserveCallable);
                }
            }
            GenerateParameterInjections(g, parameters);
            LocalBuilder l = null;
            g.Emit(OpCodes.Ldarg_0);
            //Determines if the object is wrapped and, if so, loads the field onto the stack, then iterates the parameters
            //to load them onto the stack.
            if (IsWrapped)
            {
                g.Emit(OpCodes.Ldfld, f);
            }
            for (int i = 0; i < parameters.Count; i++)
            {
                g.Emit(OpCodes.Ldarg_S, i + 1);
            }
            //Determines if the object is wrapped and, if so, calls the late-bound version of this method on the wrapper field. Otherwise,
            //this calls the base method on this.
            if (m.ReturnType != typeof(void))
            {
                l = g.DeclareLocal(m.ReturnType);
            }
            if (IsWrapped)
            {
                g.Emit(OpCodes.Callvirt, bm);
            }
            else
            {
                g.Emit(OpCodes.Call, bm);
            }
            //Determines if there is a return type. If so, use the declared local from earlier to hold the value, then generate post
            //call injections.
            if (m.ReturnType != typeof(void))
            {
                g.Emit(OpCodes.Stloc, l);
            }
            //Determines if no attributes have been provided. IF they haven't, specifically generate method injections. Otherwise,
            //this is a property, so generate injections based off of the attributes list.
            if (attributes == null)
            {
                GenerateMethodInjections(g, bm, false);
            }
            else
            {
                GenerateInjections(g, attributes, false);
            }
            //Determines if the return type is void and, if so, ensures that no return is attempted by injecting a no op instruction, then
            //calls the IL return instruction.
            if (m.ReturnType == typeof(void))
            {
                g.Emit(OpCodes.Nop);
            }
            else
            {
                g.Emit(OpCodes.Ldloc, l);
            }
            g.Emit(OpCodes.Ret);
            //Returns the method builder. This is not currently used.
            return m;
        }
        private void GenerateMethodInjections(ILGenerator g, MethodInfo method, bool isBeforeCall)
        {
            //Attempts to get the attributes of the method out of the method attributes dictionary. If successful,
            //call the generate injections method.
            if (MethodAttributes.TryGetValue(method, out IReadOnlyList<Attribute> attributes))
            {
                GenerateInjections(g, attributes, isBeforeCall);
            }
        }
        private void GenerateObserveInjection(ILGenerator g, MethodInfo bm, MethodInfo om)
        {
            //Loads this onto the stack followed by a string, then calls the method defined in cm.
            g.Emit(OpCodes.Ldarg_0);
            //Determines if the observation is happening on an accessor and, if so, removes the access components from the name. Otherwise,
            //use the base method's name for the observable call.
            if (bm.IsSpecialName)
            {
                g.Emit(OpCodes.Ldstr, RemoveAccessorComponents(bm.Name));
            }
            else
            {
                g.Emit(OpCodes.Ldstr, bm.Name);
            }
            //Injects IL to call the observe method. Currently the observe method must have a single string parameter.
            g.Emit(OpCodes.Call, om);
        }
        private TypeInfo GenerateOrGetProxyType()
        {
            //Determines if the proxied type has been generated. If not, determine if the type is proxyable.
            //If it is, create the proxy type. Otherwise, set the proxy type to base type for errorless operation.
            if (ProxyType == null)
            {
                if (IsProxyable)
                {
                    ProxyType = CreateProxyType();
                }
                else
                {
                    ProxyType = BaseType;
                }
            }
            //Return the proxy type.s
            return ProxyType;
        }
        private void GenerateParameterInjections(ILGenerator g, IReadOnlyList<ParameterInfo> parameters)
        {
            //Iterates over the parameters and retrieves their custom attributes.
            for (int i = 0; i < parameters.Count; i++)
            {
                ParameterInfo p = parameters[i];
                IReadOnlyList<ProxierAnnotationAttribute> attributes = p.GetCustomAttributes()
                    .Where(a => a is ProxierAnnotationAttribute)
                    .Cast<ProxierAnnotationAttribute>()
                    .ToArray();
                //Walks over any proxier annotation attributes found and begins the injection process.
                foreach (ProxierAnnotationAttribute a in attributes)
                {
                    GenerateInjection(i, g, a);
                }
            }
        }
        private PropertyBuilder GenerateProperty(TypeBuilder t, PropertyInfo bp, FieldBuilder f)
        {
            //Defines the dynamic property based directly off of the base property.
            PropertyBuilder p = t.DefineProperty(bp.Name,
                bp.Attributes,
                bp.PropertyType,
                bp.GetIndexParameters().Select(r => r.ParameterType).ToArray());
            if (bp.CanRead)
            {
                p.SetGetMethod(GenerateMethod(t, bp.GetMethod, f, PropertyAttributes[bp]));
            }
            if (bp.CanWrite)
            {
                p.SetSetMethod(GenerateMethod(t, bp.SetMethod, f, PropertyAttributes[bp]));
            }
            //Returns the property builder. This is not currently used.
            return p;
        }
        private TypeInfo CreateProxyType()
        {
            TypeBuilder t = ProxyGeneratorCache.Module.DefineType(NewProxyTypeName());
            //Sets the parent of this type to the type provided in the type parameter.
            t.SetParent(BaseType);
            FieldBuilder f = null;
            if (IsWrapped)
            {
                f = GenerateWrappedConstructor(t);
                GenerateWrappedOverride(nameof(GetHashCode), t, f);
                GenerateWrappedOverride(nameof(Equals), t, f);
            }
            //Walks all of the methods that have been defined and generates their overrides.
            foreach (var methodDetails in MethodAttributes.Keys)
            {
                GenerateMethod(t, methodDetails, f);
            }
            //Walks all of the properties that have been defined and generates their overrides.
            foreach (var propertyDetails in PropertyAttributes.Keys)
            {
                GenerateProperty(t, propertyDetails, f);
            }
            return t.CreateTypeInfo();
        }
        private FieldBuilder GenerateWrappedConstructor(TypeBuilder t)
        {
            //Defines a string to contain the name of the wrapped instance field, then assign this value to a randomly generated proxy field that
            //is read only.
            FieldBuilder f = t.DefineField(ProxyWrappedName(t), BaseType, FieldAttributes.FamORAssem | FieldAttributes.InitOnly);
            //Defines the parameterless constructor and gets its generator.
            ConstructorBuilder c = t.DefineConstructor(System.Reflection.MethodAttributes.Public, CallingConventions.HasThis, Type.EmptyTypes);
            ILGenerator g = c.GetILGenerator();
            //Defines a local of the base type, emits IL that calls the constructor of the base type, and finally loads the instance into the loc.
            LocalBuilder l = g.DeclareLocal(BaseType);
            g.Emit(OpCodes.Newobj, BaseType.GetConstructor(Type.EmptyTypes));
            g.Emit(OpCodes.Stloc, l);
            //Loads this and the local, then emits the set field IL pointing to the wrapped instance field.
            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Ldloc, l);
            g.Emit(OpCodes.Stfld, f);
            //Emits IL to safely exit the constructor call.
            g.Emit(OpCodes.Nop);
            g.Emit(OpCodes.Ret);
            //Return the wrapped instance field name. This is used to get the field later on during generation.
            return f;
        }
        private void GenerateWrappedOverride(string name, TypeBuilder t, FieldBuilder f) =>
            //Gets the method by the name provided. This method is generally unsafe if a method has overloads, but for basic built in methods
            //such as get hash code or equals, this should be safe enough.
            GenerateWrappedOverride(typeof(T).GetMethod(name), t, f);
        private void GenerateWrappedOverride(string name, Type[] types, TypeBuilder t, FieldBuilder f) =>
            //Gets the method by the name and parameter types provided. This method should be preferred over name only resolution due to it
            //not producing errors when a method has overloads.
            GenerateWrappedOverride(typeof(T).GetMethod(name, types), t, f);
        private void GenerateWrappedOverride(MethodInfo bm, TypeBuilder t, FieldBuilder f)
        {
            //If the method is found and is virtual, generate the override. Otherwise, we can't generate an override and we need to ignore
            //the call.
            if (bm != null && bm.IsVirtual)
            {
                //Defines a new method using the values of bm. Remove new slot to prevent issues arising later on that might cause the base
                //method to be called in some contexts instead of the override. Then, set the parameters and return types.
                MethodBuilder m = t.DefineMethod(bm.Name, bm.Attributes & ~System.Reflection.MethodAttributes.NewSlot);
                m.SetParameters(bm.GetParameters().Select(p => p.ParameterType).ToArray());
                m.SetReturnType(bm.ReturnType);
                //Add the override to the type and get the method IL generator.
                t.DefineMethodOverride(m, bm);
                ILGenerator g = m.GetILGenerator();
                //Load this onto the stack, then load the field from this onto the stack.
                g.Emit(OpCodes.Ldarg_0);
                g.Emit(OpCodes.Ldfld, f);
                //Load all parameters onto the stack by index.
                for (int i = 0; i < bm.GetParameters().Count(); i++)
                {
                    g.Emit(OpCodes.Ldarg_S, i + 1);
                }
                //Inject IL to call the base method on the field, then return the result if one is returned. If no return type is cleared,
                //force a nop onto the stack to prevent errors being generated when return is hit.
                g.Emit(OpCodes.Callvirt, bm);
                if (m.ReturnType == typeof(void))
                    g.Emit(OpCodes.Nop);
                g.Emit(OpCodes.Ret);
            }
        }
        private string ProxyWrappedName(Type t) =>
            $"_wrappedInstance_{t.FullName.Replace('.', '_')}";

        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Returns the <typeparamref name="T"/> proxy generator singleton. This will always return a valid instance
        /// of <see cref="ProxyGenerator{T}"/>.
        /// </para>
        /// </summary>
        /// <returns>A <see cref="ProxyGenerator{T}"/> for <typeparamref name="T"/>.</returns>
        public static ProxyGenerator<T> GetInstance() =>
            Singleton.Value;

        private static string NewProxyTypeName() =>
            $"Proxier.Core.ProxyType_{ProxyGeneratorCache.ProxyGuid()}";
        private static string RemoveAccessorComponents(string accessorName) =>
            accessorName.Split(new[] { '_' }, 2).Last();
    }
}