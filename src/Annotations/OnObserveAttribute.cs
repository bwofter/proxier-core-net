namespace Proxier.Core.Annotations
{
    using System;
    /// <summary>
    /// <para>
    /// Description:
    /// </para>
    /// <para>
    /// Declares that a method should be used any time a property is observed if the <see cref="ObservableAttribute"/>
    /// is attached to the class.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class OnObserveAttribute : Attribute { }
}
