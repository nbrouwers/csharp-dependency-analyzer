using DependencyAnalyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependencyAnalyzer.Analysis;

/// <summary>
/// A CSharpSyntaxWalker that collects all type references within a type declaration,
/// producing TypeDependency edges for each discovered reference.
/// </summary>
public sealed class DependencyVisitor : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly HashSet<string> _inScopeTypes;
    private readonly List<TypeDependency> _dependencies = new();

    private string? _currentTypeFqn;

    public IReadOnlyList<TypeDependency> Dependencies => _dependencies;

    public DependencyVisitor(SemanticModel semanticModel, HashSet<string> inScopeTypes)
    {
        _semanticModel = semanticModel;
        _inScopeTypes = inScopeTypes;
    }

    // --- Type declaration tracking ---

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        VisitTypeDeclaration(node, () => base.VisitClassDeclaration(node));
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        VisitTypeDeclaration(node, () => base.VisitInterfaceDeclaration(node));
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        VisitTypeDeclaration(node, () => base.VisitStructDeclaration(node));
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var outerFqn = _currentTypeFqn;
        _currentTypeFqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(symbol);
        // Enums have no base list or members that create type dependencies
        base.VisitEnumDeclaration(node);
        _currentTypeFqn = outerFqn;
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var outerFqn = _currentTypeFqn;
        _currentTypeFqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(symbol);

        // Base list
        if (node.BaseList != null)
        {
            foreach (var baseType in node.BaseList.Types)
            {
                var typeInfo = _semanticModel.GetTypeInfo(baseType.Type);
                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    var reason = namedType.TypeKind == TypeKind.Interface
                        ? "Implements interface"
                        : "Inherits from";
                    RecordDependency(namedType, reason);
                }
            }
        }

        // Primary constructor parameters (record-specific)
        if (node.ParameterList != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Record primary constructor parameter type");
            }
        }

        base.VisitRecordDeclaration(node);
        _currentTypeFqn = outerFqn;
    }

    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var outerFqn = _currentTypeFqn;
        _currentTypeFqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(symbol);

        // Return type
        RecordTypeReference(node.ReturnType, "Delegate return type");

        // Parameters
        foreach (var param in node.ParameterList.Parameters)
        {
            if (param.Type != null)
                RecordTypeReference(param.Type, "Delegate parameter type");
        }

        _currentTypeFqn = outerFqn;
    }

    private void VisitTypeDeclaration(TypeDeclarationSyntax node, Action visitBase)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var outerFqn = _currentTypeFqn;
        _currentTypeFqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(symbol);

        // Base list: inheritance and interface implementation
        if (node.BaseList != null)
        {
            foreach (var baseType in node.BaseList.Types)
            {
                var typeInfo = _semanticModel.GetTypeInfo(baseType.Type);
                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    var reason = namedType.TypeKind == TypeKind.Interface
                        ? "Implements interface"
                        : "Inherits from";
                    RecordDependency(namedType, reason);
                }
            }
        }

        // Primary constructor parameters (C# 12 for classes/structs)
        if (node.ParameterList != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Primary constructor parameter type");
            }
        }

        visitBase();
        _currentTypeFqn = outerFqn;
    }

    // --- Member declarations ---

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Declaration.Type, "Field type");
        base.VisitFieldDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "Property type");
        base.VisitPropertyDeclaration(node);
    }

    public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Declaration.Type, "Event type");
        base.VisitEventFieldDeclaration(node);
    }

    public override void VisitEventDeclaration(EventDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "Event property type");
        base.VisitEventDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            RecordTypeReference(node.ReturnType, "Method return type");
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Method parameter type");
            }
        }
        base.VisitMethodDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Constructor parameter type");
            }
        }
        base.VisitConstructorDeclaration(node);
    }

    public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            RecordTypeReference(node.Type, "Indexer return type");
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Indexer parameter type");
            }
        }
        base.VisitIndexerDeclaration(node);
    }

    public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            RecordTypeReference(node.ReturnType, "Operator return type");
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Operator parameter type");
            }
        }
        base.VisitOperatorDeclaration(node);
    }

    public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            RecordTypeReference(node.Type, "Conversion operator type");
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Conversion operator parameter type");
            }
        }
        base.VisitConversionOperatorDeclaration(node);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            RecordTypeReference(node.ReturnType, "Local function return type");
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Local function parameter type");
            }
        }
        base.VisitLocalFunctionStatement(node);
    }

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Lambda parameter type");
            }
        }
        base.VisitParenthesizedLambdaExpression(node);
    }

    // --- Expressions ---

    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Declaration.Type, "Local variable type");
        base.VisitLocalDeclarationStatement(node);
    }

    public override void VisitUsingStatement(UsingStatementSyntax node)
    {
        // using (Target t = ...) {} — block-form using with explicit type
        if (_currentTypeFqn != null && node.Declaration != null)
            RecordTypeReference(node.Declaration.Type, "Using statement variable type");
        base.VisitUsingStatement(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        // for (Target t = ...; ...) — initializer with explicit type
        if (_currentTypeFqn != null && node.Declaration != null)
            RecordTypeReference(node.Declaration.Type, "For statement variable type");
        base.VisitForStatement(node);
    }

    public override void VisitFixedStatement(FixedStatementSyntax node)
    {
        // fixed (Target* p = ...) — fixed statement with explicit type
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Declaration.Type, "Fixed statement variable type");
        base.VisitFixedStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "ForEach variable type");
        base.VisitForEachStatement(node);
    }

    public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "Catch clause exception type");
        base.VisitCatchDeclaration(node);
    }

    public override void VisitTypeConstraint(TypeConstraintSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "Generic type constraint");
        base.VisitTypeConstraint(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            // Get the type from the expression itself (more reliable than node.Type)
            var typeInfo = _semanticModel.GetTypeInfo(node);
            if (typeInfo.Type is INamedTypeSymbol namedType)
                RecordDependency(namedType, "Object creation (new)");
        }
        base.VisitObjectCreationExpression(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            // Target-typed new: `Target t = new();`
            var typeInfo = _semanticModel.GetTypeInfo(node);
            if (typeInfo.Type is INamedTypeSymbol namedType)
                RecordDependency(namedType, "Implicit object creation (new())");
        }
        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type.ElementType, "Array creation element type");
        base.VisitArrayCreationExpression(node);
    }

    public override void VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "stackalloc element type");
        base.VisitStackAllocArrayCreationExpression(node);
    }

    public override void VisitDefaultExpression(DefaultExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "default() expression");
        base.VisitDefaultExpression(node);
    }

    public override void VisitSizeOfExpression(SizeOfExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "sizeof() expression");
        base.VisitSizeOfExpression(node);
    }

    public override void VisitAttribute(AttributeSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol?.ContainingType is INamedTypeSymbol attrType)
                RecordDependency(attrType, "Attribute usage");
        }
        base.VisitAttribute(node);
    }

    public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "typeof() expression");
        base.VisitTypeOfExpression(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            if (node.IsKind(SyntaxKind.IsExpression) || node.IsKind(SyntaxKind.AsExpression))
            {
                if (node.Right is TypeSyntax typeSyntax)
                    RecordTypeReference(typeSyntax, "Type check (is/as)");
            }
        }
        base.VisitBinaryExpression(node);
    }

    public override void VisitIsPatternExpression(IsPatternExpressionSyntax node)
    {
        // DeclarationPattern, TypePattern, RecursivePattern handled by their own visitors below
        base.VisitIsPatternExpression(node);
    }

    public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
    {
        // Covers: `is Target t`, `case Target t:`, switch expression arms with declaration
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "Pattern matching (declaration pattern)");
        base.VisitDeclarationPattern(node);
    }

    public override void VisitTypePattern(TypePatternSyntax node)
    {
        // Covers: C# 9+ `case Target:`, `is Target` without variable
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "Pattern matching (type pattern)");
        base.VisitTypePattern(node);
    }

    public override void VisitRecursivePattern(RecursivePatternSyntax node)
    {
        // Covers: `is Target { Prop: value }`, recursive patterns with type
        if (_currentTypeFqn != null && node.Type != null)
            RecordTypeReference(node.Type, "Pattern matching (recursive pattern)");
        base.VisitRecursivePattern(node);
    }

    public override void VisitConstantPattern(ConstantPatternSyntax node)
    {
        // In some contexts (switch expression arms, negated patterns),
        // the parser produces ConstantPattern for what is semantically a type reference.
        // Check if the expression resolves to a type symbol.
        if (_currentTypeFqn != null)
        {
            var typeInfo = _semanticModel.GetTypeInfo(node.Expression);
            if (typeInfo.Type is INamedTypeSymbol namedType)
            {
                RecordDependency(namedType, "Pattern matching (constant pattern type reference)");
            }
            else
            {
                // Also try GetSymbolInfo — the expression might resolve to a type symbol directly
                var symbolInfo = _semanticModel.GetSymbolInfo(node.Expression);
                if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
                    RecordDependency(typeSymbol, "Pattern matching (constant pattern type reference)");
            }
        }
        base.VisitConstantPattern(node);
    }

    public override void VisitCastExpression(CastExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
            RecordTypeReference(node.Type, "Type cast");
        base.VisitCastExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;
            if (symbol is { IsStatic: true, ContainingType: INamedTypeSymbol containingType })
            {
                RecordDependency(containingType, "Static member access");
            }

            // Handle nameof(Target.Member) — the left side (Expression) is a type reference
            // In nameof context, GetSymbolInfo on the left side may resolve to the type
            if (node.Expression is IdentifierNameSyntax identifier)
            {
                var leftSymbol = _semanticModel.GetSymbolInfo(identifier);
                if (leftSymbol.Symbol is INamedTypeSymbol typeSymbol)
                    RecordDependency(typeSymbol, "nameof() type reference");
            }
        }
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                if (method.IsExtensionMethod)
                {
                    // The first parameter type of the extension method's definition
                    var reducedFrom = method.ReducedFrom ?? method;
                    if (reducedFrom.Parameters.Length > 0)
                    {
                        var thisParamType = reducedFrom.Parameters[0].Type;
                        if (thisParamType is INamedTypeSymbol namedType)
                            RecordDependency(namedType, "Extension method target type");
                    }
                }

                // Detect bare method calls from `using static` imports and nameof()
                // When node.Expression is a simple IdentifierName (no qualifier), the method
                // may come from a `using static` import.
                if (node.Expression is IdentifierNameSyntax && method.ContainingType is INamedTypeSymbol callContainingType)
                {
                    RecordDependency(callContainingType, "Static method call (using static)");
                }

                // Detect Type.GetType("FQN") and Assembly.GetType("FQN") with a string literal argument.
                // Only statically-resolvable string literals are captured; dynamic strings are not
                // detectable without runtime tracing (Strategy 1 — see README for full discussion).
                if (method.Name == "GetType" &&
                    IsReflectionGetTypeSource(method) &&
                    node.ArgumentList.Arguments.Count >= 1 &&
                    node.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax reflLit &&
                    reflLit.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var reflReason = method.ContainingType.Name == "Assembly"
                        ? "Reflection: Assembly.GetType string literal"
                        : "Reflection: Type.GetType string literal";
                    RecordDependencyByFqn(reflLit.Token.ValueText, reflReason);
                }
            }
        }
        base.VisitInvocationExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Detect bare identifier references from `using static` imports (properties, fields, etc.)
        // and nameof(Target) expressions.
        if (_currentTypeFqn != null)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;

            // Case 1: bare static member from `using static` (field, property, constant)
            if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol)
            {
                if (symbol.IsStatic && symbol.ContainingType is INamedTypeSymbol memberContaining)
                {
                    // Only if accessed without qualifier (parent is not MemberAccessExpression)
                    if (node.Parent is not MemberAccessExpressionSyntax mas || mas.Name != node)
                    {
                        RecordDependency(memberContaining, "Static member access (using static)");
                    }
                }
            }

            // Case 2: nameof(Target) — the argument resolves to a type symbol
            if (symbol is INamedTypeSymbol typeRef)
            {
                // Verify this is inside a nameof context or a standalone type reference
                if (node.Parent is ArgumentSyntax)
                {
                    RecordDependency(typeRef, "nameof() type reference");
                }
            }
            // Also check via GetTypeInfo for aliases
            else if (symbol is null)
            {
                var typeInfo = _semanticModel.GetTypeInfo(node);
                if (typeInfo.Type is INamedTypeSymbol namedType && node.Parent is ArgumentSyntax)
                {
                    RecordDependency(namedType, "nameof() type reference");
                }
            }
        }
        base.VisitIdentifierName(node);
    }

    public override void VisitFromClause(FromClauseSyntax node)
    {
        // LINQ: `from Target t in collection` — explicit type in from clause
        if (_currentTypeFqn != null && node.Type != null)
            RecordTypeReference(node.Type, "LINQ from clause type");
        base.VisitFromClause(node);
    }

    public override void VisitJoinClause(JoinClauseSyntax node)
    {
        // LINQ: `join Target t in collection on ...` — explicit type in join clause
        if (_currentTypeFqn != null && node.Type != null)
            RecordTypeReference(node.Type, "LINQ join clause type");
        base.VisitJoinClause(node);
    }

    public override void VisitFunctionPointerType(FunctionPointerTypeSyntax node)
    {
        // `delegate*<Target, void>` — function pointer parameter/return types
        if (_currentTypeFqn != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type != null)
                    RecordTypeReference(param.Type, "Function pointer parameter type");
            }
        }
        base.VisitFunctionPointerType(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        if (_currentTypeFqn != null)
        {
            foreach (var typeArg in node.TypeArgumentList.Arguments)
            {
                RecordTypeReference(typeArg, "Generic type argument");
            }
        }
        base.VisitGenericName(node);
    }

    // --- Helper methods ---

    private void RecordTypeReference(TypeSyntax typeSyntax, string reason)
    {
        // Unwrap ref types: `ref Target` → `Target`
        if (typeSyntax is RefTypeSyntax refType)
        {
            RecordTypeReference(refType.Type, reason);
            return;
        }

        var typeInfo = _semanticModel.GetTypeInfo(typeSyntax);
        var typeSymbol = typeInfo.Type;

        // Unwrap array types to their element type
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var elementType = arrayType.ElementType;
            while (elementType is IArrayTypeSymbol nested)
                elementType = nested.ElementType;
            if (elementType is INamedTypeSymbol arrayElementType)
            {
                RecordDependency(arrayElementType, reason);
                if (arrayElementType.IsGenericType)
                {
                    foreach (var typeArg in arrayElementType.TypeArguments)
                    {
                        if (typeArg is INamedTypeSymbol argType)
                            RecordDependency(argType, "Generic type argument");
                    }
                }
            }
            return;
        }

        // Unwrap pointer types: `Target*` → `Target`
        if (typeSymbol is IPointerTypeSymbol pointerType)
        {
            if (pointerType.PointedAtType is INamedTypeSymbol pointedAt)
                RecordDependency(pointedAt, reason);
            return;
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            RecordDependency(namedType, reason);

            // Also record dependencies on generic type arguments
            if (namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    if (typeArg is INamedTypeSymbol argType)
                        RecordDependency(argType, "Generic type argument");
                }
            }
        }

        // Unwrap nullable
        if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length > 0
            && nullable.TypeArguments[0] is INamedTypeSymbol underlyingType)
        {
            RecordDependency(underlyingType, reason);
        }
    }

    private void RecordDependency(INamedTypeSymbol typeSymbol, string reason)
    {
        if (_currentTypeFqn == null) return;

        // Unwrap to original definition for open generics
        var resolved = typeSymbol.OriginalDefinition ?? typeSymbol;
        var fqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(resolved);

        // Only record if the target is in scope and not self-referencing
        if (fqn != _currentTypeFqn && _inScopeTypes.Contains(fqn))
        {
            _dependencies.Add(new TypeDependency(_currentTypeFqn, fqn, reason));
        }
    }

    /// <summary>
    /// Records a dependency edge directly from a string FQN — used for reflection
    /// call sites where the target type is known via a string literal rather than
    /// a type syntax node.
    /// </summary>
    private void RecordDependencyByFqn(string targetFqn, string reason)
    {
        if (_currentTypeFqn == null) return;
        if (targetFqn != _currentTypeFqn && _inScopeTypes.Contains(targetFqn))
            _dependencies.Add(new TypeDependency(_currentTypeFqn, targetFqn, reason));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the method is <c>Type.GetType</c> or
    /// <c>Assembly.GetType</c> — the two primary reflection entry points whose
    /// first argument is a fully-qualified type name string.
    /// </summary>
    private static bool IsReflectionGetTypeSource(IMethodSymbol method)
    {
        var containingTypeName = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return containingTypeName is "global::System.Type" or "global::System.Reflection.Assembly";
    }
}
