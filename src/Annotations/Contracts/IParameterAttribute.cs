namespace Proxier.Core.Annotations.Contracts
{
    /// <summary>
    /// <para>
    /// Description:
    /// </para>
    /// <para>
    /// Declares that the attribute is attached to a parameter and that it has the <see cref="Index"/> property available to it.
    /// <see cref="Index"/> is assigned the value -1 if the return value is being targeted.
    /// </para>
    /// </summary>
    public interface IParameterAttribute
    {
        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// The index of the parameter. This is assigned the value -1 if the return value is being targeted.
        /// </para>
        /// </summary>
        int Index { get; set; }
    }
}
