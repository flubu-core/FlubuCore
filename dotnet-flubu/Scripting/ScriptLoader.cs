﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FlubuCore.IO.Wrappers;
using FlubuCore.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace DotNet.Cli.Flubu.Scripting
{
    public class ScriptLoader : IScriptLoader
    {
        private readonly IFileWrapper _file;

        public ScriptLoader(IFileWrapper file)
        {
            _file = file;
        }

        public async Task<IBuildScript> FindAndCreateBuildScriptInstanceAsync(string fileName)
        {
            var dd = typeof(Enumerable).GetTypeInfo().Assembly.Location;
            var coreDir = Directory.GetParent(dd);

            List<MetadataReference> references = new List<MetadataReference>
            {
                // Here we get the path to the mscorlib and private mscorlib
                // libraries that are required for compilation to succeed.
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "mscorlib.dll"),
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DefaultBuildScript).GetTypeInfo().Assembly.Location)
            };

            // Enumerate all assemblies referenced by this executing assembly
            // and provide them as references to the build script we're about to
            // compile.
            AssemblyName[] referencedAssemblies = Assembly.GetEntryAssembly().GetReferencedAssemblies();
            foreach (var referencedAssembly in referencedAssemblies)
            {
                Assembly loadedAssembly = Assembly.Load(referencedAssembly);
                references.Add(MetadataReference.CreateFromFile(loadedAssembly.Location));
            }

            var opts = ScriptOptions.Default
                .WithReferences(references);

            string code = _file.ReadAllText(fileName);
            var className = GetClassNameFromBuildScriptCode(code);
            Script script = CSharpScript
                .Create(code, opts)
                .ContinueWith(string.Format("var sc = new {0}();", className));

            ScriptState result = await script.RunAsync();

            return result.Variables[0].Value as IBuildScript;
        }

        public string GetClassNameFromBuildScriptCode(string scriptCode)
        {
            using (StringReader sr = new StringReader(scriptCode))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var i = line.IndexOf("class", StringComparison.Ordinal);
                    if (i != -1)
                    {
                        var tmp = line.Substring(i + 6);
                        tmp = tmp.TrimStart();
                        i = tmp.IndexOf(" ", StringComparison.Ordinal);
                        if (i == -1)
                        {
                            i = tmp.Length;
                        }

                        var className = tmp.Substring(0, i);
                        return className;
                    }
                }
            }

            return null;
        }
    }
}