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
namespace Proxier.Core.Internals
{
    using System;
    using System.Reflection;
    using System.Reflection.Emit;
    //This ugly little duckling provides a global cache for all proxy generator generated types. It takes advantage of the .net lazy type to provide thread safe
    //(ish) access to its members, masking the lazy types behind properties that directly call the value property.
    internal static class ProxyGeneratorCache
    {
        public static AssemblyBuilder Assembly =>
            AssemblyCache.Value;
        public static ModuleBuilder Module =>
            ModuleCache.Value;

        private static Lazy<AssemblyBuilder> AssemblyCache { get; } =
            new Lazy<AssemblyBuilder>(() => AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(AssemblyName.Value), AssemblyBuilderAccess.RunAndCollect), true);
        private static Lazy<string> AssemblyName { get; } =
            new Lazy<string>(() => $"ProxyAssembly_{ProxyGuid()}", true);
        private static Lazy<ModuleBuilder> ModuleCache { get; } =
            new Lazy<ModuleBuilder>(() => Assembly.DefineDynamicModule(ModuleName.Value), true);
        private static Lazy<string> ModuleName { get; } =
            new Lazy<string>(() => $"ProxyModule_{ProxyGuid()}", true);

        static ProxyGeneratorCache() { }

        internal static string ProxyGuid() =>
            Guid.NewGuid().ToString().Replace('-', '_');
    }
}
