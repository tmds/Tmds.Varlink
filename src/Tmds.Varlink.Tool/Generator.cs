using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Tmds.Varlink.Tool
{
    class Generator
    {
        private readonly AdhocWorkspace _workspace;
        private readonly SyntaxGenerator _generator;

        public Generator()
        {
            _workspace = new AdhocWorkspace();
            _generator = SyntaxGenerator.GetGenerator(_workspace, LanguageNames.CSharp);
        }

        private SyntaxNode[] ImportNamespaceDeclarations()
        {
            return new[] {
                _generator.NamespaceImportDeclaration("System"),
                _generator.NamespaceImportDeclaration(_generator.DottedName("System.Collections.Generic")),
                _generator.NamespaceImportDeclaration(_generator.DottedName("System.Threading.Tasks")),
                _generator.NamespaceImportDeclaration(_generator.DottedName("Tmds.Varlink")),
            };
        }

        public string Generate(Interface interf)
        {
            var importDeclarations = ImportNamespaceDeclarations();
            var namespaceDeclarations = new List<SyntaxNode>();
            foreach (var typedef in interf.Typedefs)
            {
                if (typedef.Kind == TypeKind.Struct)
                {
                    AddClassType(namespaceDeclarations, interf, typedef);
                }
                else if (typedef.Kind == TypeKind.Enum)
                {
                    AddEnumType(namespaceDeclarations, interf, typedef);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            List<MemberDeclarationSyntax> classDeclarations = new List<MemberDeclarationSyntax>();
            // InterfaceName
            var interfaceAddressField = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.PredefinedType(
                            SyntaxFactory.Token(SyntaxKind.StringKeyword)))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                            SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier("InterfaceName"))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(interf.Name)))))))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        new []{
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.ConstKeyword)}));
            classDeclarations.Add(interfaceAddressField);
            string[] splitName = interf.Name.Split('.').Select(name => $"{char.ToUpper(name[0])}{name.Substring(1)}").ToArray();
            string className = splitName[splitName.Length - 1];
            string namespaceName = string.Join('.', splitName.Take(splitName.Length - 1));
            // _conn field
            var connField = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.IdentifierName("IConnection"))
                        .WithVariables(
                            SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier("_conn")))))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            new []{
                                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)}));
            classDeclarations.Add(connField);
            // ctor
            var constructor = SyntaxFactory.ConstructorDeclaration(
                        SyntaxFactory.Identifier(className))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                                SyntaxFactory.Parameter(
                                    SyntaxFactory.Identifier("address"))
                                .WithType(
                                    SyntaxFactory.PredefinedType(
                                        SyntaxFactory.Token(SyntaxKind.StringKeyword))))))
                    .WithBody(
                        SyntaxFactory.Block(
                            SyntaxFactory.SingletonList<StatementSyntax>(
                                SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        SyntaxFactory.IdentifierName("_conn"),
                                        SyntaxFactory.ObjectCreationExpression(
                                            SyntaxFactory.IdentifierName("Connection"))
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                    SyntaxFactory.Argument(
                                                        SyntaxFactory.IdentifierName("address"))))))))));
            classDeclarations.Add(constructor);

            foreach (var method in interf.Methods)
            {
                string methodBody = "return _conn.CallAsync";
                TypeSyntax returnType;
                if (method.ReturnType.Members.Count > 0)
                {
                    method.ReturnType.Name = $"{method.Name}Result";
                    AddClassType(namespaceDeclarations, interf, method.ReturnType);
                    returnType = SyntaxFactory.ParseTypeName($"Task<{method.ReturnType.Name}>");
                    methodBody += $"<{method.ReturnType.Name}>";
                }
                else
                {
                    returnType = SyntaxFactory.ParseTypeName("System.Threading.Task");
                }
                methodBody += $"(\"{interf.Name}.{method.Name}\", GetErrorParametersType";
                SeparatedSyntaxList<ParameterSyntax> parameters = new SeparatedSyntaxList<ParameterSyntax>();
                if (method.ParameterType.Members.Count > 0)
                {
                    method.ParameterType.Name = $"{method.Name}Args";
                    AddClassType(namespaceDeclarations, interf, method.ParameterType);
                    parameters = parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier("args")).WithType(SyntaxFactory.ParseTypeName(method.ParameterType.Name)));
                    methodBody += ", args);";
                }
                else
                {
                    methodBody += ");";
                }
                MethodDeclarationSyntax newMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.List<AttributeListSyntax>(),
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
                    returnType,
                    null,
                    SyntaxFactory.Identifier($"{method.Name}Async"),
                    null,
                    SyntaxFactory.ParameterList(parameters),
                    SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
                    SyntaxFactory.Block(SyntaxFactory.ParseStatement(methodBody)),
                    SyntaxFactory.Token(SyntaxKind.SemicolonToken)
                );
                classDeclarations.Add(newMethod);
            }
            // GetErrorParametersType
            var getErrorParametersTypeMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Type")),
                    SyntaxFactory.Identifier("GetErrorParametersType"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        new []{
                            SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)}))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                            SyntaxFactory.Parameter(
                                SyntaxFactory.Identifier("args"))
                            .WithType(
                                SyntaxFactory.PredefinedType(
                                    SyntaxFactory.Token(SyntaxKind.StringKeyword))))))
                // TODO: generate body
                .WithBody(
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ReturnStatement(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.NullLiteralExpression)))));
            
            classDeclarations.Add(getErrorParametersTypeMethod);
            // TODO: generate error name const string members
            // dispose method
            var disposeMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("Dispose"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithBody(
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("_conn"),
                                        SyntaxFactory.IdentifierName("Dispose")))))));
            classDeclarations.Add(disposeMethod);
            namespaceDeclarations.Add(
                SyntaxFactory.ClassDeclaration(className)
                    .WithBaseList(
                        SyntaxFactory.BaseList(
                            SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                                SyntaxFactory.SimpleBaseType(
                                    SyntaxFactory.IdentifierName("IDisposable")))))
                                    .WithMembers(
                        SyntaxFactory.List<MemberDeclarationSyntax>(classDeclarations))
                );
            var namespaceDeclaration = _generator.NamespaceDeclaration(_generator.DottedName(namespaceName), namespaceDeclarations);
            var compilationUnit = _generator.CompilationUnit(importDeclarations.Concat(new[] { namespaceDeclaration }));
            return compilationUnit.NormalizeWhitespace().ToFullString();
        }

        private void AddEnumType(List<SyntaxNode> namespaceDeclarations, Interface interf, Type typedef)
        {
            var members = typedef.Members.Select(member => SyntaxFactory.EnumMemberDeclaration(SyntaxFactory.Identifier(member.Name)));
            var enumDeclaration = SyntaxFactory.EnumDeclaration(SyntaxFactory.Identifier(typedef.Name)).WithMembers(SyntaxFactory.SeparatedList(members));
            namespaceDeclarations.Add(enumDeclaration);
        }

        private void AddClassType(List<SyntaxNode> namespaceDeclarations, Interface interf, Type typedef)
        {
            var properties = new List<SyntaxNode>();
            foreach (var member in typedef.Members)
            {
                string suggestedTypeName = $"{typedef.Name}{char.ToUpper(member.Name[0])}{member.Name.Substring(1)}";
                (string typeName, _) = DetermineTypeName(namespaceDeclarations, interf, member.Type, suggestedTypeName);
                var propertyDeclaration = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(typeName), ToIdentifierToken(member.Name))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAccessorListAccessors(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                properties.Add(propertyDeclaration);
            }
            namespaceDeclarations.Add(_generator.ClassDeclaration(typedef.Name, null, Accessibility.NotApplicable, DeclarationModifiers.None, null, null, properties));
        }

        // TODO: support Maybe
        private (string name, bool isRef) DetermineTypeName(List<SyntaxNode> namespaceDeclarations, Interface interf, Type type, string suggestedTypeName)
        {
            if (type.Kind == TypeKind.Alias)
            {
                type = interf.Typedefs.First(td => td.Name == type.Name);
            }
            if (type.Kind == TypeKind.Maybe)
            {
                (string innerName, bool innerIsRef) = DetermineTypeName(namespaceDeclarations, interf, type.InnerType, suggestedTypeName);
                if (innerIsRef)
                {
                    return (innerName, true);
                }
                else
                {
                    return ($"{innerName}?", false);
                }
            }
            else if (type.Kind == TypeKind.Array)
            {
                (string innerName, bool innerIsRef) = DetermineTypeName(namespaceDeclarations, interf, type.InnerType, suggestedTypeName);
                return ($"{innerName}[]", true);
            }
            else if (type.Kind == TypeKind.Dictionary)
            {
                (string innerName, bool innerIsRef) = DetermineTypeName(namespaceDeclarations, interf, type.InnerType, suggestedTypeName);
                return ($"Dictionary<string,{innerName}>", true);
            }
            switch (type.Kind)
            {
                case TypeKind.Struct:
                    return (NameType(namespaceDeclarations, suggestedTypeName, interf, type), false);
                case TypeKind.Enum:
                    return (NameType(namespaceDeclarations, suggestedTypeName, interf, type), false);
                case TypeKind.Bool:
                    return ("bool", false);
                case TypeKind.Int:
                    return ("long", false);
                case TypeKind.Float:
                    return ("double", false);
                case TypeKind.String:
                    return ("string", true);
                case TypeKind.Object:
                    return ("object", true);
            }
            throw new NotSupportedException();
        }

        private string NameType(List<SyntaxNode> namespaceDeclarations, string suggestedTypeName, Interface interf, Type type)
        {
            if (type.Name != null)
            {
                return type.Name;
            }
            type.Name = suggestedTypeName;
            if (type.Kind == TypeKind.Struct)
            {
                AddClassType(namespaceDeclarations, interf, type);
            }
            else if (type.Kind == TypeKind.Enum)
            {
                AddEnumType(namespaceDeclarations, interf, type);
            }
            return suggestedTypeName;
        }

        private static string EscapeIdentifier(string identifier)
        {
            var nullIndex = identifier.IndexOf('\0');
            if (nullIndex >= 0)
            {
                identifier = identifier.Substring(0, nullIndex);
            }

            var needsEscaping = SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None;

            return needsEscaping ? "@" + identifier : identifier;
        }

        private static SyntaxToken ToIdentifierToken(string identifier)
        {
            var escaped = EscapeIdentifier(identifier);

            if (escaped.Length == 0 || escaped[0] != '@')
            {
                return SyntaxFactory.Identifier(escaped);
            }

            var unescaped = identifier.StartsWith("@", StringComparison.Ordinal)
                ? identifier.Substring(1)
                : identifier;

            var token = SyntaxFactory.Identifier(
                default(SyntaxTriviaList), SyntaxKind.None, "@" + unescaped, unescaped, default(SyntaxTriviaList));

            if (!identifier.StartsWith("@", StringComparison.Ordinal))
            {
                token = token.WithAdditionalAnnotations(Simplifier.Annotation);
            }

            return token;
        }
    }
}