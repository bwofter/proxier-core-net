namespace Proxier.Core.Annotations
{
    using System;
    /// <summary>
    /// <para>
    /// Description:
    /// </para>
    /// <para>
    /// Declares that a class is designed for use by the <see cref="ProxyGenerator{T}"/> class. While <see cref="ProxyGenerator{T}"/>
    /// might accept any reference type in its generic parameter, it will only proxy classes marked with this attribute. Otherwise,
    /// the class will be treated as its own proxy and an instance of the original class will be returned.
    /// </para>
    /// <para>
    /// This <see cref="Attribute"/> has no properties or parameters and is used only to denote a class can be proxied.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProxiedAttribute : Attribute { }
}
