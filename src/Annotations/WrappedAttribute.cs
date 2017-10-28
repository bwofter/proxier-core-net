namespace Proxier.Core.Annotations
{
    using System;
    /// <summary>
    /// <para>
    /// Description:
    /// </para>
    /// <para>
    /// Marks a class as being wrapped. This is used in conjunction with <see cref="ProxiedAttribute"/>
    /// to declare that the proxy type should have an internal "state" instance of the proxied type
    /// instead of directly overriding the proxied type. This allows for instances to be converted to
    /// and from the proxied type.
    /// </para>
    /// <para>
    /// This <see cref="Attribute"/> has no properties or parameters and is used only to denote a class can be proxied.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class WrappedAttribute : Attribute { }
}
