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
namespace Proxier.Core.Annotations
{
    using System;
    using System.Reflection.Emit;
    /// <summary>
    /// <para>
    /// Description:
    /// </para>
    /// <para>
    /// A base type for implementers to use to define their own proxier operations. This type
    /// provides a property that defines where injection should occur and a method that defines
    /// the IL injector.
    /// </para>
    /// </summary>
    public abstract class ProxierAnnotationAttribute : Attribute
    {
        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Whether <see cref="Inject"/> should be called before the proxied type call is generated. If true,
        /// <see cref="Inject"/> will be applied before the IL is emitted to call the proxied type, otherwise
        /// it will be applied after. This is defaulted to false.
        /// </para>
        /// </summary>
        public bool IsBeforeCall { get; }

        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Creates a new instance of the <see cref="Attribute"/> and sets the value of <see cref="IsBeforeCall"/>
        /// to <paramref name="isBeforeCall"/>.
        /// </para>
        /// </summary>
        /// <param name="isBeforeCall">The value to assign to <see cref="IsBeforeCall"/>. This is defaulted to false.</param>
        public ProxierAnnotationAttribute(bool isBeforeCall = false) =>
            IsBeforeCall = isBeforeCall;

        /// <summary>
        /// <para>
        /// Description:
        /// </para>
        /// <para>
        /// Supplies a method signature for <see cref="ProxyGenerator{T}"/> to call during proxy generation. This method injects
        /// IL into <paramref name="g"/> if possible. This is called before <see cref="ProxyGenerator{T}"/> emits the call to the
        /// proxied type if <see cref="IsBeforeCall"/> is true, otherwise it is called afterward. This method must be implemented.
        /// </para>
        /// </summary>
        /// <param name="g">The <see cref="ILGenerator"/> that contains the IL stream to inject into. This will never be null
        /// unless <see cref="ProxyGenerator{T}"/> has entered an invalid state.</param>
        public abstract void Inject(ILGenerator g);
    }
}
