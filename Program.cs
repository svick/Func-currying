using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using Svicks_Tools;

namespace ConsoleApplication1
{
    static class Program
    {
        private static readonly CodeTypeParameter ResultTypeParameter = new CodeTypeParameter("TResult");

        static CodeTypeParameter CreateTypeParameter(int i)
        {
            return new CodeTypeParameter("T" + i);
        }

        static CodeTypeReference CreateCurriedFuncType(int currentLevel, int maxLevel)
        {
            if (currentLevel == maxLevel)
                return CreateFuncType(CreateTypeParameter(currentLevel), ResultTypeParameter);

            return CreateFuncType(CreateTypeParameter(currentLevel).ToCodeTypeReference(),
                                  CreateCurriedFuncType(currentLevel + 1, maxLevel));
        }

        static CodeTypeReference CreateFuncType(params CodeTypeParameter[] arguments)
        {
            return CreateFuncType((IEnumerable<CodeTypeParameter>)arguments);
        }

        static CodeTypeReference CreateFuncType(params CodeTypeReference[] arguments)
        {
            return CreateFuncType((IEnumerable<CodeTypeReference>)arguments);
        }

        static CodeTypeReference CreateFuncType(IEnumerable<CodeTypeParameter> arguments)
        {
            return CreateFuncType(arguments.Select(ToCodeTypeReference));
        }

        static CodeTypeReference CreateFuncType(IEnumerable<CodeTypeReference> arguments)
        {
            var argumentsArr = arguments.ToArray();

            var sampleFuncType = argumentsArr.Length <= 9 ? typeof(Func<,>) : typeof(Func<,,,,,,,,,>);
            var assembly = Assembly.GetAssembly(sampleFuncType);
            var type = assembly.GetType("System.Func`" + argumentsArr.Length);

            var result = new CodeTypeReference(type);
            result.TypeArguments.AddRange(argumentsArr.ToArray());
            return result;
        }

        static CodeTypeReference CreateFuncType(int arguments)
        {
            return CreateFuncType(CreateFuncTypeParameters(arguments).Select(ToCodeTypeReference));
        }

        private static IEnumerable<CodeTypeParameter> CreateFuncTypeParameters(int arguments)
        {
            for (int i = 1; i <= arguments; i++)
                yield return CreateTypeParameter(i);
            yield return ResultTypeParameter;
        }

        static CodeTypeReference ToCodeTypeReference(this CodeTypeParameter parameter)
        {
            return new CodeTypeReference(parameter);
        }

        static void Main()
        {
            var compileUnit = new CodeCompileUnit();

            var curryingNamespace = new CodeNamespace("Currying");
            compileUnit.Namespaces.Add(curryingNamespace);

            var funcExtensionsClass = new CodeTypeDeclaration("FuncExtensions");
            curryingNamespace.Types.Add(funcExtensionsClass);

            for (int i = 1; i <= 16; i++)
            {
                string funcParameterName = "func";

                var curryMethod =
                    new CodeMemberMethod
                    {
                        Name = "Curry",
                        Attributes = MemberAttributes.Static | MemberAttributes.Public,
                        ReturnType = CreateCurriedFuncType(1, i),
                        Parameters =
                            { new CodeParameterDeclarationExpression(CreateFuncType(i), funcParameterName) }
                    };
                curryMethod.TypeParameters.AddRange(CreateFuncTypeParameters(i).ToArray());

                var parameters = Enumerable.Range(1, i).Select(j => "p" + j).ToArray();

                string lambda = string.Format("return {0} => {1}({2});", string.Join(" => ", parameters),
                                              funcParameterName, string.Join(", ", parameters));

                curryMethod.Statements.Add(new CodeSnippetStatement(lambda));

                funcExtensionsClass.Members.Add(curryMethod);
            }

            var writer = new StringWriter();

            var provider = new CSharpCodeProvider();

            provider.GenerateCodeFromCompileUnit(compileUnit, writer, new CodeGeneratorOptions { BracingStyle = "C" });

            string code = writer.ToString();

            code = code.Replace("class FuncExtensions", "static class FuncExtensions");
            code = code.Replace("(Func<", "(this Func<");

            code.Dump();

            provider.CompileAssemblyFromSource(
                new CompilerParameters(new[] { "System.Core.dll" }, "Currying.dll") { GenerateInMemory = false },
                code).Errors.Dump();
        }
    }
}