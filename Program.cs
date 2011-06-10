using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CSharp;
using Svicks_Tools;
using Currying;

namespace ConsoleApplication1
{
    class Program
    {
        private static string ResultTypeParameterName = "TResult";

        static string CreateTypeParameterName(int i)
        {
            return "T" + i;
        }

        static string CreateCurriedFuncType(int currentLevel, int maxLevel)
        {
            if (currentLevel == maxLevel)
                return CreateFuncType(CreateTypeParameterName(currentLevel), ResultTypeParameterName);

            return CreateFuncType(CreateTypeParameterName(currentLevel),
                                  CreateCurriedFuncType(currentLevel + 1, maxLevel));
        }

        static string CreateFuncType(params string[] arguments)
        {
            return CreateFuncType((IEnumerable<string>)arguments);
        }

        static string CreateFuncType(IEnumerable<string> arguments)
        {
            return string.Format("Func{0}", CreateFuncArguments(arguments));
        }

        static string CreateFuncType(int arguments)
        {
            return CreateFuncType(CreateFuncTypeParameters(arguments));
        }

        private static string CreateFuncArguments(IEnumerable<string> arguments)
        {
            return string.Format("<{0}>", string.Join(", ", arguments));
        }

        private static IEnumerable<string> CreateFuncTypeParameters(int arguments)
        {
            for (int i = 1; i <= arguments; i++)
                yield return CreateTypeParameterName(i);
            yield return ResultTypeParameterName;
        }

        static void Main()
        {
            var provider = new CSharpCodeProvider();

            var compileUnit = new CodeCompileUnit();

            var curryingNamespace = new CodeNamespace("Currying");
            compileUnit.Namespaces.Add(curryingNamespace);

            curryingNamespace.Imports.Add(new CodeNamespaceImport("System"));

            var funcExtensionsClass = new CodeTypeDeclaration("FuncExtensions");

            curryingNamespace.Types.Add(funcExtensionsClass);

            for (int i = 1; i <= 3; i++)
            {
                string funcParameterName = "func";

                var curryMethod =
                    new CodeMemberMethod
                    {
                        Name = "Curry",
                        Attributes = MemberAttributes.Static | MemberAttributes.Public,
                        ReturnType = new CodeTypeReference(CreateCurriedFuncType(1, i)),
                        Parameters =
                            { new CodeParameterDeclarationExpression("this " + CreateFuncType(i), funcParameterName) }
                    };
                curryMethod.TypeParameters.AddRange(CreateFuncTypeParameters(i).Select(p => new CodeTypeParameter(p)).ToArray());

                var parameters = Enumerable.Range(1, i).Select(j => "p" + j).ToArray();

                string lambda = string.Format("return {0} => {1}({2});", string.Join(" => ", parameters),
                                              funcParameterName, string.Join(", ", parameters));

                curryMethod.Statements.Add(new CodeSnippetStatement(lambda));

                funcExtensionsClass.Members.Add(curryMethod);
            }

            var writer = new StringWriter();

            provider.GenerateCodeFromCompileUnit(compileUnit, writer, new CodeGeneratorOptions{BracingStyle = "C"});

            string code = writer.ToString();

            code = code.Replace("class FuncExtensions", "static class FuncExtensions");

            code.Dump();

            provider.CompileAssemblyFromSource(
                new CompilerParameters(new[] { "System.Core.dll" }, "Currying.dll") { GenerateInMemory = false },
                code).Errors.Dump();
        }
    }
}