namespace Proxier.Core.Annotations
{
    using System;
    /// <summary>
    /// <para>
    /// Description:
    /// </para>
    /// <para>
    /// Declares that a class is designed for use by the <see cref="ProxyGenerator{T}"/> class and that the class should have
    /// observable methods attached to it. This attribute requires the <see cref="ProxiedAttribute"/> to be present, otherwise
    /// it is ignored by <see cref="ProxyGenerator{T}"/>.
    /// </para>
    /// <para>
    /// This <see cref="Attribute"/> has no properties or parameters and is used only to denote a class can be proxied.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ObservableAttribute : Attribute { }
}
