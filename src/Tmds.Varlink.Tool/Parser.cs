using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tmds.Varlink.Tool
{
    enum TokenType
    {
        Whitespace,
        Eol,
        Comment,
        GroupStart,
        GroupEnd,
        Separator,
        TypeSeparator,
        Colon,
        Arrow,
        Word,
        Eof
    }

    ref struct Token
    {
        public TokenType Type { get; set; }
        public Span<char> Data { get; set; }

        public override string ToString() => new string(Data);
    }

    class Scanner
    {
        private readonly char[] _chars;
        private int _offset;

        public Scanner(string definition)
        {
            _chars = definition.AsSpan().ToArray();
        }

        private static char[] s_whiteSpace = new [] {
            '\t', ' ', '\u00A0', '\uFEFF', '\u1680', '\u180E', '\u2000', '\u200A', '\u202F', '\u205F', '\u3000'
        }; // TODO: space not part of spec ?!?
        private static char[] s_eol = new[] {
            '\n', '\r', '\u2028', '\u2029'
        };
        private static char s_comment = '#';
        private static char s_groupStart = '(';
        private static char s_groupEnd = ')';
        private static char s_separator = ',';
        private static char s_colon = ':';
        private static string s_arrow = "->";
        private static char s_typeSeparator = ':';

        public Token Read()
        {
            int offset = _offset;
            TokenType type = ReadType(_chars, ref offset);
            if (type == TokenType.Word)
            {
                int startOffset = _offset;
                _offset = offset;
                while (ReadType(_chars, ref offset) == TokenType.Word)
                {
                    _offset = offset;
                }
                return new Token { Type = TokenType.Word, Data = _chars.AsSpan().Slice(startOffset, _offset - startOffset) };
            }
            else
            {
                _offset = offset;
                return new Token { Type = type };
            }
        }

        public static TokenType ReadType(char[] chars, ref int offset)
        {
            if (offset == chars.Length)
            {
                return TokenType.Eof;
            }
            char c = chars[offset++];
            if (s_whiteSpace.AsSpan().IndexOf(c) != -1)
            {
                return TokenType.Whitespace;
            }
            if (s_eol.AsSpan().IndexOf(c) != -1)
            {
                if (c == '\r')
                {
                    if (offset < chars.Length)
                    {
                        c = chars[offset];
                        if (c == '\n')
                        {
                            offset++;
                        }
                    }
                }
                return TokenType.Eol;
            }
            if (c == s_comment)
            {
                while (offset < chars.Length)
                {
                    c = chars[offset++];
                    if (s_eol.AsSpan().IndexOf(c) != -1)
                    {
                        break;
                    }
                }
                return TokenType.Comment;
            }
            else if (c == s_groupStart)
            {
                return TokenType.GroupStart;
            }
            else if (c == s_groupEnd)
            {
                return TokenType.GroupEnd;
            }
            else if (c == s_separator)
            {
                return TokenType.Separator;
            }
            else if (c == s_colon)
            {
                return TokenType.Colon;
            }
            else if (c == s_typeSeparator)
            {
                return TokenType.TypeSeparator;
            }
            else if (c == s_arrow[0] && offset < chars.Length && chars[offset] == s_arrow[1])
            {
                offset++;
                return TokenType.Arrow;
            }
            else
            {
                return TokenType.Word;
            }
        }
    }

    class Method
    {
        public string Name { get; set; }
        public Type ReturnType { get; set; }
        public Type ParameterType { get; set; }

        public Method(string methodName, Type returnType, Type parameterType)
        {
            this.Name = methodName;
            this.ReturnType = returnType;
            this.ParameterType = parameterType;
        }

        public override string ToString()
        {
            return $"method {Name} {ParameterType} -> {ReturnType}";
        }
    }

    public enum TypeKind
    {
        Alias,
        Struct,
        Enum,
        Bool,
        Int,
        Float,
        String,
        Object,
        Maybe,
        Array,
        Dictionary
    }

    class Member
    {
        public string Name { get; set; }
        public Type Type { get; set; }
    }

    class Type
    {
        public static readonly Type Bool = new Type { Kind = TypeKind.Bool };
        public static readonly Type Int = new Type { Kind = TypeKind.Int };
        public static readonly Type Float = new Type { Kind = TypeKind.Float };
        public static readonly Type String = new Type { Kind = TypeKind.String };
        public static readonly Type Object= new Type { Kind = TypeKind.Object };

        public Type InnerType { get; set; }
        public TypeKind Kind { get; set; }
        public string Name { get; set; }
        public List<Member> Members { get; set; }

        public override string ToString()
        {
            switch (Kind)
            {
                case TypeKind.Alias:
                    return Name;
                case TypeKind.Struct:
                    return $"(" + string.Join(", ", Members.Select(member => $"{member.Name}: {member.Type}")) + ")";
                case TypeKind.Enum:
                    return $"(" + string.Join(", ", Members.Select(member => member.Name)) + ")";
                case TypeKind.Bool:
                    return "bool";
                case TypeKind.Int:
                    return "int";
                case TypeKind.Float:
                    return "float";
                case TypeKind.String:
                    return "string";
                case TypeKind.Object:
                    return "object";
                case TypeKind.Maybe:
                    return $"?{InnerType}";
                case TypeKind.Array:
                    return $"[]{InnerType}";
                case TypeKind.Dictionary:
                    return $"[string]{InnerType}";
            }
            throw new NotSupportedException();
        }
    }

    class Error
    {
        public string Name  { get; set; }
        public Type Type  { get; set; }

        public Error(string errorName, Type errorType)
        {
            this.Name = errorName;
            this.Type = errorType;
        }

        public override string ToString()
        {
            return $"error {Name} {Type}";
        }
    }

    class Parser
    {
        private readonly Scanner _scanner;
        private Parser(string definition)
        {
            _scanner = new Scanner(definition);
        }

        private Interface ReadInterface()
        {
            ReadString("interface");
            string interfaceName = ReadString();
            (List<Method> methods, List<Type> typedefs, List<Error> errors) = ReadMembers();
            return new Interface(interfaceName, methods, typedefs, errors);
        }

        private (List<Method>, List<Type>, List<Error>) ReadMembers()
        {
            var methods = new List<Method>();
            var typedefs = new List<Type>();
            var errors = new List<Error>();
            while (true)
            {
                Token token = ReadNext(new [] { TokenType.Eof, TokenType.Word });
                if (token.Type == TokenType.Eof)
                {
                    break;
                }
                string memberType = token.ToString();
                switch (memberType)
                {
                    case "method":
                        methods.Add(ReadMethod());
                        break;
                    case "type":
                        typedefs.Add(ReadNamedType());
                        break;
                    case "error":
                        errors.Add(ReadError());
                        break;
                    default:
                        throw new FormatException($"Unknown member type: {memberType}");
                }
            }
            return (methods, typedefs, errors);
        }

        private Error ReadError()
        {
            string errorName = ReadString();
            Type errorType = ReadUnnamedType();
            return new Error(errorName, errorType);
        }

        private Type ReadNamedType()
        {
            string typeName = ReadString();
            Type type = ReadUnnamedType();
            type.Name = typeName;
            return type;
        }

        private Type ReadUnnamedType()
        {
            Token token = ReadNext(new[] { TokenType.GroupStart, TokenType.Word});
            if (token.Type == TokenType.GroupStart)
            {
                var members = new List<Member>();
                bool? isEnum = null;
                while (true)
                {
                    string memberName;
                    token = ReadNext(new[] { TokenType.GroupEnd, TokenType.Word });
                    if (token.Type == TokenType.GroupEnd)
                    {
                        if (isEnum != null)
                        {
                            throw new FormatException("Unexpected end of group.");
                        }
                        isEnum = false;
                        break;
                    }
                    else // token.Type == TokenType.Word
                    {
                        memberName = token.ToString();
                    }
                    token = ReadNext(new[] { TokenType.Colon, TokenType.Separator, TokenType.GroupEnd });
                    if (isEnum == null)
                    {
                        // determine type
                        if (token.Type == TokenType.Colon)
                        {
                            isEnum = false;
                        }
                        else if (token.Type == TokenType.Separator
                            || token.Type == TokenType.GroupEnd)
                        {
                            isEnum = true;
                        }
                    }
                    if (isEnum == true)
                    {
                        members.Add(new Member { Name = memberName });
                    }
                    else // isEnum == false
                    {
                        Type memberType = ReadUnnamedType();
                        members.Add(new Member { Name = memberName, Type = memberType });
                        token = ReadNext(new[] { TokenType.Separator, TokenType.GroupEnd});
                    }
                    if (token.Type == TokenType.GroupEnd)
                    {
                        break;
                    }
                }
                if (isEnum == true)
                {
                    return new Type { Kind = TypeKind.Enum, Members = members };
                }
                else // isEnum == false
                {
                    return new Type { Kind = TypeKind.Struct, Members = members };
                }
            }
            else // token.Type == TokenType.Word
            {
                return ParseTypeWord(token.ToString());
            }
        }

        private Type ParseTypeWord(string typeWord)
        {
            if (typeWord.Length == 0)
            {
                return ReadUnnamedType();
            }
            if (typeWord.StartsWith("?"))
            {
                Type innerType = ParseTypeWord(typeWord.Substring(1));
                return new Type { Kind = TypeKind.Maybe, InnerType = innerType };
            }
            else if (typeWord.StartsWith("[]"))
            {
                Type innerType = ParseTypeWord(typeWord.Substring(2));
                return new Type { Kind = TypeKind.Array, InnerType = innerType };
            }
            else if (typeWord.StartsWith("[string]"))
            {
                Type innerType = ParseTypeWord(typeWord.Substring(8));
                return new Type { Kind = TypeKind.Dictionary, InnerType = innerType };
            }
            switch (typeWord)
            {
                case "bool": return Type.Bool;
                case "int": return Type.Int;
                case "float": return Type.Float;
                case "string": return Type.String;
                case "object": return Type.Object;
            }
            return new Type { Kind = TypeKind.Alias, Name = typeWord };
        }

        private Method ReadMethod()
        {
            string methodName = ReadString();
            Type parameterType = ReadUnnamedType();
            ReadNext(TokenType.Arrow);
            Type returnType = ReadUnnamedType();
            return new Method (methodName, returnType, parameterType);
        }

        private string ReadString(string expected = null)
        {
            Token token = ReadNext(TokenType.Word);
            string rv = token.ToString();
            if (expected != null && expected != rv)
            {
                throw new FormatException($"Expected: {expected}, but actual: {rv}");
            }
            return rv;
        }

        Token ReadNext(TokenType expected)
            => ReadNext(new [] { expected });

        Token ReadNext(TokenType[] expected)
        {
            while (true)
            {
                Token token = _scanner.Read();
                // skip whitespace, comments, eol
                if (token.Type == TokenType.Comment
                 || token.Type == TokenType.Whitespace
                 || token.Type == TokenType.Eol)
                {
                    continue;
                }
                if (expected != null && !expected.Contains(token.Type))
                {
                    throw new FormatException($"Expected type: {expected}, but actual: {token.Type}");
                }
                return token;
            }
        }

        public static Interface Parse(string definition)
        {
            Parser parser = new Parser(definition);
            return parser.ReadInterface();
        }
    }

    class Interface
    {
        public string Name { get; set; }

        public List<Method> Methods { get; set; }
        public List<Type> Typedefs { get; set; }
        public List<Error> Errors { get; set; }

        public Interface(string interfaceName, List<Method> methods, List<Type> typedefs, List<Error> errors)
        {
            Name = interfaceName;
            Methods = methods;
            Typedefs = typedefs;
            Errors = errors;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"interface {Name}");
            sb.AppendLine();

            sb.AppendJoin('\n', Typedefs.Select(td => $"type {td.Name} {td}"));
            sb.AppendLine();

            sb.AppendJoin('\n', Errors);
            sb.AppendLine();

            sb.AppendJoin('\n', Methods);
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
