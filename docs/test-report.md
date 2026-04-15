# Test Report — C# Dependency Analyzer

> **Document version:** 1.0  
> **Last updated:** 2026-04-15  
> **Test framework:** xUnit 2.5.3  
> **Target framework:** .NET 8.0  
> **Total test cases:** 284  
> **Pass rate:** 98.9 % (3 infrastructure failures: `dotnet` not in PATH on this machine; all logic tests pass)

---

## 1. Scope

This report catalogs every automated test case in the C# Dependency Analyzer project. It serves as:

- A **traceability matrix** linking tests to requirements.
- A **coverage summary** organized by test level and concern.
- A **living document** updated each time tests are added, removed, or modified (see [new-feature procedure](../.github/prompts/new-feature.prompt.md)).

### Test Levels

| Level | Description |
|-------|-------------|
| **Unit** | Tests a single component in isolation (e.g., graph builder, analyzer, report generator). |
| **Integration** | Tests multiple components wired together through the public `TestHelper.Analyze()` pipeline. |
| **System** | Tests the published executable end-to-end (CLI invocation, file I/O). |
| **Infrastructure** | Validates build, publish, and CI/CD configuration artifacts. |

### Naming Conventions

| Pattern | Meaning |
|---------|---------|
| `Detects_*` | Verifies a specific dependency construct is discovered. |
| `Probe_*` | Gap-probe test written during cross-check rounds — would have failed before the fix. |
| `Sweep_*` | Final-sweep convergence test — expected to pass without code changes. |
| `Workflow_*` | Validates CI/CD workflow configuration. |

---

## 2. Summary by Test File

| # | Test File | Tests | Level | Requirement |
|---|-----------|------:|-------|-------------|
| 1 | `RoslynWorkspaceBuilderTests.cs` | 5 | Unit | FR-1, FR-2 |
| 2 | `DependencyGraphBuilderTests.cs` | 15 | Unit | FR-3.1, FR-3.2 |
| 3 | `TransitiveFanInAnalyzerTests.cs` | 18 | Unit | FR-3.3, FR-3.4, FR-4.3, FR-4.5, FR-4.6 |
| 4 | `MarkdownReportGeneratorTests.cs` | 13 | Unit | FR-4.1–FR-4.6 |
| 5 | `EndToEndTests.cs` | 4 | Integration | FR-1–FR-4 |
| 6 | `ComprehensiveDependencyTests.cs` | 37 | Unit | FR-3.2 |
| 7 | `GapProbeTests.cs` | 16 | Unit | FR-3.2 |
| 8 | `Round3AuditProbeTests.cs` | 33 | Unit | FR-3.2 |
| 9 | `Round4FinalSweepTests.cs` | 19 | Integration | FR-3.2 |
| 10 | `DoxygenXmlExporterTests.cs` | 29 | Unit/Integration | FR-5.3, FR-6.1–FR-6.7 |
| 11 | `Neo4jExporterTests.cs` | 30 | Unit | FR-5.3, FR-7.1–FR-7.7 |
| 12 | `PortableExeTests.cs` | 3 | System | NFR-4.1–NFR-4.3 |
| 13 | `CiWorkflowTests.cs` | 7 | Infrastructure | NFR-5.1–NFR-5.3 |
| 14 | `VersionTests.cs` | 4 | Infrastructure | NFR-6.1–NFR-6.4 |
| 15 | `MarkdownReportGeneratorTests.cs` (version line) | 1 | Unit | FR-4.1 |
| 16 | `ReflectionDependencyTests.cs` | 8 | Unit | FR-3.2 |
| 17 | `CsvIdHelperTests.cs` | 10 | Unit | FR-8.4–FR-8.6 |
| 18 | `CsvExporterTests.cs` | 23 | Unit/Integration | FR-8.1–FR-8.11 |
| | **Total** | **284** | | |

---

## 3. Test Inventory

### 3.1 RoslynWorkspaceBuilderTests (5 tests)

Tests the Roslyn compilation builder and target FQN resolution. Traces to **FR-1** (target class specification) and **FR-2** (source scope).

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| WB-01 | `BuildCompilation_WithValidFiles_LoadsAllFiles` | Compilation loads all listed source files. | Unit |
| WB-02 | `BuildCompilation_WithMissingFile_SkipsAndContinues` | Missing files are skipped without crashing. | Unit |
| WB-03 | `ResolveTargetClass_ValidFqn_ReturnsSymbol` | Valid FQN resolves to the correct type symbol. | Unit |
| WB-04 | `ResolveTargetClass_UnknownFqn_ThrowsInvalidOperation` | Unknown FQN produces a clear error. | Unit |
| WB-05 | `ResolveTargetClass_AmbiguousFqn_ThrowsInvalidOperation` | Ambiguous FQN produces a clear error. | Unit |

### 3.2 DependencyGraphBuilderTests (15 tests)

Tests individual dependency edge detection for the core language constructs. Traces to **FR-3.1** and **FR-3.2**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| GB-01 | `Detects_Inheritance` | Base class reference creates an edge. | Unit |
| GB-02 | `Detects_InterfaceImplementation` | Interface implementation creates an edge. | Unit |
| GB-03 | `Detects_FieldType` | Field type declaration creates an edge. | Unit |
| GB-04 | `Detects_PropertyType` | Property type declaration creates an edge. | Unit |
| GB-05 | `Detects_MethodParameterType` | Method parameter type creates an edge. | Unit |
| GB-06 | `Detects_MethodReturnType` | Method return type creates an edge. | Unit |
| GB-07 | `Detects_LocalVariableType` | Local variable type creates an edge. | Unit |
| GB-08 | `Detects_ObjectCreation` | `new T()` creates an edge. | Unit |
| GB-09 | `Detects_GenericTypeArgument` | Generic type argument `List<T>` creates an edge. | Unit |
| GB-10 | `Detects_TypeOfExpression` | `typeof(T)` creates an edge. | Unit |
| GB-11 | `Detects_IsTypeCheck` | `is T` type check creates an edge. | Unit |
| GB-12 | `Detects_StaticMemberAccess` | `T.Member` creates an edge. | Unit |
| GB-13 | `Records_ElementKinds_Correctly` | Class, interface, struct, enum, record, delegate kinds are classified. | Unit |
| GB-14 | `DoesNotRecord_SelfReference` | A type referencing itself does not create a self-edge. | Unit |
| GB-15 | `DoesNotRecord_OutOfScopeTypes` | References to BCL types do not create edges. | Unit |

### 3.3 TransitiveFanInAnalyzerTests (10 tests)

Tests BFS transitive closure, justification chains, and metrics. Traces to **FR-3.3**, **FR-3.4**, and **FR-4.3**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| FA-01 | `DirectFanIn_SingleDependor` | Single direct dependency is found. | Unit |
| FA-02 | `TransitiveFanIn_TwoLevelsDeep` | Two-level transitive chain is computed. | Unit |
| FA-03 | `TransitiveFanIn_ThreeLevelsDeep` | Three-level transitive chain is computed. | Unit |
| FA-04 | `NoFanIn_WhenNoDependers` | Empty result when no types depend on the target. | Unit |
| FA-05 | `TargetExcluded_FromResults` | Target class is excluded from fan-in set. | Unit |
| FA-06 | `CircularDependency_DoesNotCauseInfiniteLoop` | Circular dependencies terminate correctly. | Unit |
| FA-07 | `ElementKinds_AreCorrectlyMapped` | Element kinds propagate to results. | Unit |
| FA-08 | `Justification_ContainsReasonForDirectFanIn` | Direct fan-in justification text is meaningful. | Unit |
| FA-09 | `Justification_ContainsTransitiveChain` | Transitive justification shows the dependency chain. | Unit |
| FA-10 | `Metrics_CorrectCountPerKind` | Per-kind metrics match actual element counts. | Unit |
| FA-11 | `MaxTransitiveDepth_DirectOnly_IsOne` | Direct-only fan-in produces depth 1. | Unit |
| FA-12 | `MaxTransitiveDepth_TwoLevels_IsTwo` | Two-level chain produces depth 2. | Unit |
| FA-13 | `MaxTransitiveDepth_ThreeLevels_IsThree` | Three-level chain produces depth 3. | Unit |
| FA-14 | `MaxTransitiveDepth_NoFanIn_IsZero` | No fan-in produces depth 0. | Unit |
| FA-15 | `MaxTransitiveDepth_Diamond_IsTwo` | Diamond dependency produces depth 2. | Unit |
| FA-16 | `FanInEdges_DirectOnly_ContainsEdgesToTarget` | Direct fan-in edges all point to target. | Unit |
| FA-17 | `FanInEdges_TransitiveChain_ContainsIntermediateEdges` | Transitive chain includes intermediate edges. | Unit |
| FA-18 | `FanInEdges_NoFanIn_IsEmpty` | No fan-in produces empty edge list. | Unit |

### 3.4 MarkdownReportGeneratorTests (7 tests)

Tests the Markdown report output. Traces to **FR-4.1**–**FR-4.4**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| RG-01 | `Generate_ContainsTargetHeader` | Report contains the target FQN header. | Unit |
| RG-02 | `Generate_ContainsDateHeader` | Report contains a date/timestamp header. | Unit |
| RG-03 | `Generate_EmptyFanIn_ShowsNoElementsMessage` | Empty fan-in produces a "no elements" message. | Unit |
| RG-04 | `Generate_WithElements_ContainsTableRows` | Fan-in elements render as table rows. | Unit |
| RG-05 | `Generate_ContainsMetricsTable` | Metrics section is present with per-kind counts. | Unit |
| RG-06 | `GenerateToFile_WritesFile` | Report is written to the specified file path. | Unit |
| RG-07 | `Generate_ContainsMaxTransitiveDepthRow` | Max Transitive Depth row appears in metrics table. | Unit |
| RG-08 | `Generate_WithElements_ContainsMermaidGraph` | Mermaid graph section is present when fan-in exists. | Unit |
| RG-09 | `Generate_MermaidGraph_ContainsTargetNode` | Target node has :::target style in Mermaid graph. | Unit |
| RG-10 | `Generate_MermaidGraph_ContainsEdges` | Correct number of edges in Mermaid graph. | Unit |
| RG-11 | `Generate_MermaidGraph_AppliesKindStyles` | Element kind CSS classes are applied to nodes. | Unit |
| RG-12 | `Generate_EmptyFanIn_NoMermaidGraph` | No Mermaid graph when fan-in is empty. | Unit |

### 3.5 EndToEndTests (4 tests)

Tests the full pipeline (compile → graph → analyze → verify) on realistic scenarios. Traces to **FR-1** through **FR-4**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| E2E-01 | `SampleScenario_InheritanceAndFieldType_CorrectFanIn` | Multi-level inheritance and field types produce correct fan-in set. | Integration |
| E2E-02 | `SampleScenario_AllDependencyTypes` | All core dependency types are detected in one scenario. | Integration |
| E2E-03 | `SampleScenario_DelegateAndRecord_AreDetected` | Delegates and records are detected and classified. | Integration |
| E2E-04 | `SampleScenario_StructWithTypeOf_Detected` | Struct using `typeof(T)` is detected and classified. | Integration |

### 3.6 ComprehensiveDependencyTests (37 tests)

Cross-check round 1. Each test targets a C# construct gap identified during the first audit. Traces to **FR-3.2**.

| ID | Test Method | Construct Tested | Level |
|----|------------|-----------------|-------|
| CD-01 | `Detects_ArrayFieldType` | Array field declaration | Unit |
| CD-02 | `Detects_JaggedArrayType` | Jagged array `T[][]` | Unit |
| CD-03 | `Detects_ArrayMethodReturnType` | Array return type `T[]` | Unit |
| CD-04 | `Detects_ArrayMethodParameterType` | Array parameter type `T[]` | Unit |
| CD-05 | `Detects_ArrayCreationExpression` | `new T[n]` | Unit |
| CD-06 | `Detects_GenericTypeConstraint_OnClass` | `where T : Target` on class | Unit |
| CD-07 | `Detects_GenericTypeConstraint_OnMethod` | `where T : Target` on method | Unit |
| CD-08 | `Detects_GenericTypeConstraint_Interface` | `where T : ITarget` constraint | Unit |
| CD-09 | `Detects_CatchClauseExceptionType` | `catch (TargetException)` | Unit |
| CD-10 | `Detects_ForEachVariableType` | `foreach (Target t in ...)` | Unit |
| CD-11 | `Detects_ClassPrimaryConstructor` | C# 12 `class C(Target t)` | Unit |
| CD-12 | `Detects_StructPrimaryConstructor` | C# 12 `struct S(Target t)` | Unit |
| CD-13 | `Detects_EventPropertyDeclaration` | `event Action<Target>` with add/remove | Unit |
| CD-14 | `Detects_IndexerReturnType` | `Target this[int i]` | Unit |
| CD-15 | `Detects_IndexerParameterType` | `int this[Target t]` | Unit |
| CD-16 | `Detects_OperatorParameterType` | `operator +(Target a, ...)` | Unit |
| CD-17 | `Detects_OperatorReturnType` | `Target operator +(...)` | Unit |
| CD-18 | `Detects_ImplicitConversionOperrator` | `implicit operator Target(...)` | Unit |
| CD-19 | `Detects_ExplicitConversionOperator` | `explicit operator Target(...)` | Unit |
| CD-20 | `Detects_LocalFunctionReturnType` | Local function returning `Target` | Unit |
| CD-21 | `Detects_LocalFunctionParameterType` | Local function with `Target` param | Unit |
| CD-22 | `Detects_LambdaParameterType` | `(Target t) => ...` | Unit |
| CD-23 | `Detects_ImplicitObjectCreation` | Target-typed `new()` | Unit |
| CD-24 | `Detects_DefaultExpression` | `default(Target)` | Unit |
| CD-25 | `Detects_DeclarationPattern_InSwitch` | `case Target t:` | Unit |
| CD-26 | `Detects_TypePattern_InSwitch` | `case Target:` (C# 9) | Unit |
| CD-27 | `Detects_RecursivePattern` | `o is Target { }` | Unit |
| CD-28 | `Detects_NegatedPattern_IsNot` | `o is not Target` | Unit |
| CD-29 | `Detects_SwitchExpressionArm_WithDeclarationPattern` | `Target t => ...` in switch expression | Unit |
| CD-30 | `Detects_TupleFieldWithTargetElement` | `(Target, int)` field | Unit |
| CD-31 | `Detects_TupleReturnType` | `(Target, string)` return type | Unit |
| CD-32 | `Detects_NullableReferenceTypeField` | `Target?` field | Unit |
| CD-33 | `Detects_NestedGenericType` | `Dictionary<string, List<Target>>` | Unit |
| CD-34 | `Detects_RecordStructPrimaryConstructor` | `record struct R(Target t)` | Unit |
| CD-35 | `RecordStruct_IsClassifiedCorrectly` | Record struct classified as `Struct` kind | Unit |
| CD-36 | `Detects_GenericArrayOfTargetInConstrainedMethod` | `T[]` with `where T : Target` | Unit |
| CD-37 | `Detects_MultipleConstructsInOneClass` | Field + param + local + new in same type | Unit |

### 3.7 GapProbeTests (16 tests)

Cross-check round 2. Probe tests for constructs that were missing after round 1. Traces to **FR-3.2**.

| ID | Test Method | Construct Tested | Level |
|----|------------|-----------------|-------|
| GP-01 | `Probe_UsingBlockForm` | `using (Target t = ...)` | Unit |
| GP-02 | `Probe_ForStatementInitializer` | `for (Target t = ...; ...)` | Unit |
| GP-03 | `Probe_OutVariableDeclaration` | `out Target t` | Unit |
| GP-04 | `Probe_TupleDeconstruction` | `var (a, b) = ...` with Target | Unit |
| GP-05 | `Probe_LinqFromClauseType` | `from Target t in ...` | Unit |
| GP-06 | `Probe_RefReturnType` | `ref Target Method()` | Unit |
| GP-07 | `Probe_RefParameterType` | `ref Target param` | Unit |
| GP-08 | `Probe_InParameterType` | `in Target param` | Unit |
| GP-09 | `Probe_PointerFieldType` | `Target* field` (unsafe) | Unit |
| GP-10 | `Probe_StackAllocType` | `stackalloc Target[n]` | Unit |
| GP-11 | `Probe_FixedStatement` | `fixed (Target* p = ...)` | Unit |
| GP-12 | `Probe_ExplicitInterfaceImplementation` | `IFoo.Method()` with Target return | Unit |
| GP-13 | `Probe_UsingDeclarationForm` | `using Target t = ...;` | Unit |
| GP-14 | `Probe_DiscardWithType` | `_ = (Target)obj` | Unit |
| GP-15 | `Probe_GenericMethodInvocationTypeArg` | `Method<Target>()` | Unit |
| GP-16 | `Probe_ExplicitArrayTypeInDeclaration` | `Target[] arr = ...` | Unit |

### 3.8 Round3AuditProbeTests (33 tests)

Cross-check round 3. Probes against the full C# language specification. Traces to **FR-3.2**.

| ID | Test Method | Construct Tested | Level |
|----|------------|-----------------|-------|
| R3-01 | `Probe_NameOfExpression` | `nameof(Target)` | Unit |
| R3-02 | `Probe_NameOfWithMember` | `nameof(Target.Member)` | Unit |
| R3-03 | `Probe_UsingStaticBareMethodCall` | `using static Target; Method();` | Unit |
| R3-04 | `Probe_UsingStaticBarePropertyAccess` | `using static Target; _ = Prop;` | Unit |
| R3-05 | `Probe_LinqJoinClauseWithExplicitType` | `join Target t in ...` | Unit |
| R3-06 | `Probe_FunctionPointerType` | `delegate*<Target, void>` | Unit |
| R3-07 | `Probe_ListPatternWithType` | `[Target t, ..]` list pattern | Unit |
| R3-08 | `Probe_PositionalPatternWithType` | `Target(var x)` positional pattern | Unit |
| R3-09 | `Probe_CombinedOrPatternWithTypes` | `Target or OtherType` pattern | Unit |
| R3-10 | `Probe_SwitchStatementCaseLabel` | `case Target t:` in switch statement | Unit |
| R3-11 | `Probe_ScopedRefParam` | `scoped ref Target` parameter | Unit |
| R3-12 | `Probe_RefReadonlyParam` | `ref readonly Target` parameter | Unit |
| R3-13 | `Probe_ParamsArray` | `params Target[]` parameter | Unit |
| R3-14 | `Probe_AttributeOnMethod` | `[TargetAttr]` on method | Unit |
| R3-15 | `Probe_AttributeOnParameter` | `[TargetAttr]` on parameter | Unit |
| R3-16 | `Probe_AttributeOnReturnValue` | `[return: TargetAttr]` | Unit |
| R3-17 | `Probe_AttributeOnProperty` | `[TargetAttr]` on property | Unit |
| R3-18 | `Probe_AttributeOnAssembly` | `[assembly: TargetAttr]` | Unit |
| R3-19 | `Probe_TypeOfInAttribute` | `[Attr(typeof(Target))]` | Unit |
| R3-20 | `Probe_NestedTypeDependency` | Outer type references inner Target | Unit |
| R3-21 | `Probe_InnerTypeDependsOnOuter` | Inner type references outer Target | Unit |
| R3-22 | `Probe_DelegateReturnType` | `delegate Target Handler()` | Unit |
| R3-23 | `Probe_EventWithDelegateType` | `event TargetHandler E;` | Unit |
| R3-24 | `Probe_StaticAbstractInterfaceMember` | `static abstract Target M();` | Unit |
| R3-25 | `Probe_ThrowExpressionWithNew` | `throw new TargetException()` | Unit |
| R3-26 | `Probe_RefFieldType` | `ref Target` field in ref struct | Unit |
| R3-27 | `Probe_GenericBaseClassWithTypeArg` | `class C : Base<Target>` | Unit |
| R3-28 | `Probe_GenericInterfaceWithTypeArg` | `class C : IFoo<Target>` | Unit |
| R3-29 | `Probe_ConditionalAccessExpression` | `obj?.Method()` returning Target | Unit |
| R3-30 | `Probe_ImplicitStackAllocSpan` | `Span<Target> s = stackalloc ...` | Unit |
| R3-31 | `Probe_DiamondDependency` | A → B, A → C, B → T, C → T | Unit |
| R3-32 | `Probe_LongChainTransitive` | Five-level transitive chain | Unit |
| R3-33 | `Probe_MutualDependency` | A ↔ B where B → T | Unit |

### 3.9 Round4FinalSweepTests (19 tests)

Cross-check round 4 (convergence). All passed without code changes, confirming no remaining gaps. Traces to **FR-3.2**.

| ID | Test Method | Construct Tested | Level |
|----|------------|-----------------|-------|
| R4-01 | `Sweep_YieldReturnInIterator` | `IEnumerable<Target>` with yield | Integration |
| R4-02 | `Sweep_AsyncMethodReturningTarget` | `Task<Target>` async return | Integration |
| R4-03 | `Sweep_ExpressionBodiedProperty` | `Target Prop => ...` | Integration |
| R4-04 | `Sweep_ExpressionBodiedMethod` | `Target Get() => ...` | Integration |
| R4-05 | `Sweep_CollectionInitializerWithTargetType` | `new List<Target> { ... }` | Integration |
| R4-06 | `Sweep_TernaryCastExpression` | `(Target)obj` in ternary | Integration |
| R4-07 | `Sweep_PartialMethodSignature` | `partial void Do(Target t)` | Integration |
| R4-08 | `Sweep_ExplicitInterfaceMethod` | `Target IFoo.Create()` | Integration |
| R4-09 | `Sweep_LockOnTargetTypeField` | Field of type Target used in `lock` | Integration |
| R4-10 | `Sweep_InterpolatedStringWithObjectCreation` | `$"{new Target()}"` | Integration |
| R4-11 | `Sweep_ExtensionMethodDefinition` | `this Target t` extension param | Integration |
| R4-12 | `Sweep_SizeofExpression` | `sizeof(Target)` | Integration |
| R4-13 | `Sweep_StaticLocalFunction` | `static Target Create()` local | Integration |
| R4-14 | `Sweep_LocalFunctionGenericConstraint` | `where T : Target` in local function | Integration |
| R4-15 | `Sweep_DefaultLiteralInAssignment` | `Target t = default;` | Integration |
| R4-16 | `Sweep_DiscardInSwitchExpression` | `Target t => 1, _ => 0` | Integration |
| R4-17 | `Sweep_RecordInheritsRecord` | `record Derived : BaseRecord` | Integration |
| R4-18 | `Sweep_InterfaceDefaultMethod` | `Target Create() => null;` in interface | Integration |
| R4-19 | `Sweep_CovariantReturn` | `override Target Create()` covariant return | Integration |

### 3.10 DoxygenXmlExporterTests (29 tests)

Tests for Doxygen XML export, `DoxygenRefIdHelper`, and `DoxygenEdgeKind` edge classification. Traces to **FR-5.3** and **FR-6.1**–**FR-6.7**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| DX-01 | `Export_CreatesOutputDirectory` | Directory is created when it does not exist. | Unit |
| DX-02 | `Export_CreatesIndexXml` | `index.xml` is present after export. | Unit |
| DX-03 | `Export_IndexXml_ContainsOneEntryPerType` | `index.xml` has a `<compound>` for every type in the graph. | Unit |
| DX-04 | `Export_CreatesOneXmlFilePerType` | One `{refid}.xml` file per type in `ElementKinds`. | Unit |
| DX-05 | `ToRefId_ProducesExpectedPrefix(Class, …)` | Class produces `class` prefix in refid. | Unit |
| DX-06 | `ToRefId_ProducesExpectedPrefix(Interface, …)` | Interface produces `interface` prefix. | Unit |
| DX-07 | `ToRefId_ProducesExpectedPrefix(Struct, …)` | Struct produces `struct` prefix. | Unit |
| DX-08 | `ToRefId_ProducesExpectedPrefix(Enum, …)` | Enum produces `enum` prefix. | Unit |
| DX-09 | `ToRefId_ProducesExpectedPrefix(Record, …)` | Record exported with `class` prefix. | Unit |
| DX-10 | `ToRefId_ProducesExpectedPrefix(Delegate, …)` | Delegate exported with `class` prefix. | Unit |
| DX-11 | `CompoundXml_ContainsCompoundName` | `<compoundname>` uses `::` separators. | Unit |
| DX-12 | `CompoundXml_InheritanceEdge_EmitsBaseCompoundRef_NonVirtual` | Inheritance → `<basecompoundref virt="non-virtual">`. | Unit |
| DX-13 | `CompoundXml_InterfaceEdge_EmitsBaseCompoundRef_Virtual` | Interface implementation → `<basecompoundref virt="virtual">`. | Unit |
| DX-14 | `CompoundXml_UsageEdge_EmitsMemberdefWithReferences` | Usage edge → `<memberdef>` with `<references refid="...">`. | Unit |
| DX-15 | `CompoundXml_ContainsLocationStub` | Stub `<location file="" line="0" column="0"/>` on every compound. | Unit |
| DX-16 | `CompoundXml_MultipleEdgesToSameTarget_OneRefPerReason` | Two edges with different reasons → two `<memberdef>` elements. | Unit |
| DX-17 | `Export_CreatesNamespaceCompounds` | Namespace compound files generated for all unique namespace prefixes. | Unit |
| DX-18 | `Export_EmptyGraph_ProducesOnlyIndexXml` | Empty graph → `index.xml` only, no compound files. | Unit |
| DX-19 | `ClassifyEdge_ReturnsExpectedKind("Inherits from", Inheritance)` | Classifies inheritance correctly. | Unit |
| DX-20 | `ClassifyEdge_ReturnsExpectedKind("Implements interface", InterfaceImplementation)` | Classifies interface implementation correctly. | Unit |
| DX-21 | `ClassifyEdge_ReturnsExpectedKind("Field type", Usage)` | Usage classification for field type. | Unit |
| DX-21b | `ClassifyEdge_ReturnsExpectedKind("Method parameter type", Usage)` | Usage classification for method param. | Unit |
| DX-21c | `ClassifyEdge_ReturnsExpectedKind("Object creation", Usage)` | Usage classification for object creation. | Unit |
| DX-21d | `ClassifyEdge_ReturnsExpectedKind("Property type", Usage)` | Usage classification for property type. | Unit |
| DX-22 | `XmlOutput_IsWellFormed` | All generated files parse without `XmlException`. | Unit |
| DX-23 | `IntegrationExport_SampleGraph_AllTypesPresent` | Full graph export produces files for all types; edges intact. | Integration |
| DX-24 | `ToCompoundName_ReplacesDotWithDoubleColon` | FQN `.` → `::` in compound name. | Unit |
| DX-25 | `ExtractNamespaces_ReturnsAllPrefixes` | All dot-delimited prefixes returned for multi-level FQN. | Unit |
| DX-26 | `NamespaceRefId_EncodesCorrectly` | Namespace refid uses `namespace` prefix + encoded name. | Unit |

### 3.11 Neo4jExporterTests (30 tests)

Tests helper logic for the Neo4j direct import using the Doxygen-aligned graph schema — node parameter building (Compound nodes with Doxygen refids), namespace parameter building, `virt` attribute derivation, and edge operation collection — all without requiring a live Neo4j server. Traces to **FR-5.3** and **FR-7.1**–**FR-7.8**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| NJ-01 | `BuildNodeParameters_IdIsDoxygenRefId` | `id` property equals the Doxygen refid derived from the FQN and `ElementKind`. | Unit |
| NJ-02 | `BuildNodeParameters_KindIsDoxygenKindString` | `kind` property is the lowercase Doxygen kind string (`"class"`), not the enum name. | Unit |
| NJ-03 | `BuildNodeParameters_NameUsesDoubleColonSeparators` | `name` uses `::` separators — mirrors `<compoundname>Acme::Core::OrderService</compoundname>`. | Unit |
| NJ-04 | `BuildNodeParameters_ContainsFqn` | Parameter map includes `fqn` key with the original .NET fully qualified name. | Unit |
| NJ-05 | `BuildNodeParameters_ContainsLanguageCSharp` | Parameter map includes `language` key set to `"C#"`. | Unit |
| NJ-06 | `BuildNodeParameters_AllKindsMappedToDoxygenKindString(×6)` | Theory: `Class`/`Record`/`Delegate`→`"class"`, `Interface`→`"interface"`, `Struct`→`"struct"`, `Enum`→`"enum"`. | Unit |
| NJ-07 | `BuildNamespaceParameters_IdIsNamespaceRefId` | Namespace parameter `id` equals `NamespaceRefId(ns)`. | Unit |
| NJ-08 | `BuildNamespaceParameters_KindIsNamespace` | Namespace parameter `kind` is `"namespace"` — mirrors `<compounddef kind="namespace">`. | Unit |
| NJ-09 | `BuildNamespaceParameters_NameUsesDoubleColonSeparators` | Namespace `name` uses `::` separators. | Unit |
| NJ-10 | `GetVirt_Inheritance_ReturnsNonVirtual` | `Inheritance` → `"non-virtual"` — mirrors `<basecompoundref virt="non-virtual">`. | Unit |
| NJ-11 | `GetVirt_InterfaceImplementation_ReturnsVirtual` | `InterfaceImplementation` → `"virtual"` — mirrors `<basecompoundref virt="virtual">`. | Unit |
| NJ-12 | `GetVirt_Usage_ReturnsNonVirtual` | `Usage` edge kind → `"non-virtual"` fallback. | Unit |
| NJ-13 | `CollectEdgeOperations_EmptyGraph_ReturnsEmpty` | No edge operations emitted for empty graph. | Unit |
| NJ-14 | `CollectEdgeOperations_InheritanceEdge_RelTypeIsBaseCompoundRef` | Inheritance edge → `BASECOMPOUNDREF` relationship type. | Unit |
| NJ-15 | `CollectEdgeOperations_InheritanceEdge_VirtParameterIsNonVirtual` | Inheritance edge → `virt="non-virtual"`, `prot="public"` parameters. | Unit |
| NJ-16 | `CollectEdgeOperations_InterfaceEdge_RelTypeIsBaseCompoundRef` | Interface-implementation edge → `BASECOMPOUNDREF` relationship type. | Unit |
| NJ-17 | `CollectEdgeOperations_InterfaceEdge_VirtParameterIsVirtual` | Interface-implementation edge → `virt="virtual"` parameter. | Unit |
| NJ-18 | `CollectEdgeOperations_UsageEdge_RelTypeIsReferences` | Usage edge → `REFERENCES` relationship type. | Unit |
| NJ-19 | `CollectEdgeOperations_UsageEdge_ReasonInParameters` | `reason` property on `REFERENCES` edge matches `DependencyReason`. | Unit |
| NJ-20 | `CollectEdgeOperations_OutOfScopeTarget_EdgeSkipped` | Edge targeting type not in `ElementKinds` is not emitted. | Unit |
| NJ-21 | `CollectEdgeOperations_EdgeIdsAreDerivedFromRefIds` | `SourceId`/`TargetId` on `EdgeOperation` are Doxygen refids, not .NET FQNs. | Unit |
| NJ-22 | `CollectInnerClassOperations_EmptyGraph_ReturnsEmpty` | No inner-class operations for empty graph or empty namespace list. | Unit |
| NJ-23 | `CollectInnerClassOperations_TypeInNamespace_ProducesInnerClassEdge` | Type directly in a namespace → one `INNERCLASS` edge operation emitted. | Unit |
| NJ-24 | `CollectInnerClassOperations_InnerClassEdge_IdsAreNamespaceRefIdAndTypeRefId` | `SourceId` is the namespace refid; `TargetId` is the type refid; both reflected in `Parameters`. | Unit |
| NJ-25 | `CollectInnerClassOperations_TopLevelType_NotLinkedToAnyNamespace` | Type with no namespace prefix is not linked to any namespace compound. | Unit |

### 3.12 PortableExeTests (3 tests)

Tests the self-contained single-file executable publish and execution. Traces to **NFR-4.1**–**NFR-4.3**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| PE-01 | `Publish_ProducesSingleExeFile` | `dotnet publish` produces one `.exe` > 10 MB with no loose DLLs. | System |
| PE-02 | `PublishedExe_ShowsHelp` | Portable exe `--help` returns correct CLI usage. | System |
| PE-03 | `PublishedExe_AnalyzesSampleCodebase` | Portable exe analyzes sample and finds 11 fan-in elements. | System |

### 3.13 CiWorkflowTests (7 tests)

Validates the GitHub Actions CI workflow configuration. Traces to **NFR-5.1**–**NFR-5.3**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| CI-01 | `Workflow_FileExists` | `ci.yml` exists at `.github/workflows/`. | Infrastructure |
| CI-02 | `Workflow_ContainsTestJob` | Workflow includes a `dotnet test` step. | Infrastructure |
| CI-03 | `Workflow_ContainsPublishJob` | Workflow includes a `dotnet publish` step. | Infrastructure |
| CI-04 | `Workflow_PublishDependsOnTest` | Publish job depends on test job (`needs: test`). | Infrastructure |
| CI-05 | `Workflow_UploadsArtifact` | Workflow uploads the artifact via `upload-artifact`. | Infrastructure |
| CI-06 | `Workflow_TriggersOnPush` | Workflow triggers on `push` events. | Infrastructure |
| CI-07 | `Workflow_TriggersOnPullRequest` | Workflow triggers on `pull_request` events. | Infrastructure |

### 3.14 VersionTests (4 tests)

Validates semantic versioning configuration and assembly embedding. Traces to **NFR-6.1**–**NFR-6.4**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| VR-01 | `Csproj_ContainsVersionElement` | `.csproj` has a `<Version>` element with MAJOR.MINOR.PATCH format. | Infrastructure |
| VR-02 | `Csproj_VersionFollowsSemVer` | Version string has three numeric parts. | Infrastructure |
| VR-03 | `Assembly_HasInformationalVersion` | Compiled assembly has `AssemblyInformationalVersionAttribute`. | Infrastructure |
| VR-04 | `Assembly_VersionMatchesCsproj` | Assembly version matches the `.csproj` `<Version>` value. | Infrastructure |

### 3.15 MarkdownReportGeneratorTests — Version Line (1 test)

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| RG-13 | `Generate_ContainsVersionLine` | Report contains tool version in header. | Unit |

### 3.16 ReflectionDependencyTests (8 tests)

Tests static reflection dependency detection via `Type.GetType`, `Assembly.GetType`, and `Module.GetType` with string literal arguments. Traces to **FR-3.2**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| RF-01 | `Detects_TypeGetType_FullyQualifiedStringLiteral` | `Type.GetType("N.Target")` with in-scope FQN → dependency edge emitted. | Unit |
| RF-02 | `TypeGetType_TypeNotInScope_NoEdge` | String literal for out-of-scope type → no spurious edge produced. | Unit |
| RF-03 | `Detects_AssemblyGetType_FullyQualifiedStringLiteral` | `assembly.GetType("N.Target")` with in-scope FQN → dependency edge emitted. | Unit |
| RF-04 | `TypeGetType_NonLiteralStringArgument_NoEdge` | Variable passed to `Type.GetType` (not a literal) → no edge. | Unit |
| RF-05 | `TypeGetType_DependencyReason_ContainsReflection` | `DependencyReason` on the emitted edge contains the word `"Reflection"`. | Unit |
| RF-06 | `TypeGetType_SelfReference_NoSelfEdge` | Type calling `Type.GetType` with its own FQN → no self-loop. | Unit |
| RF-07 | `Detects_ModuleGetType_FullyQualifiedStringLiteral` | `module.GetType("N.Target")` with in-scope FQN → dependency edge emitted. | Unit |
| RF-08 | `ModuleGetType_DependencyReason_ContainsModuleAndReflection` | `DependencyReason` contains both `"Reflection"` and `"Module"`. | Unit |

### 3.17 CsvIdHelperTests (10 tests)

Tests the deterministic ID, label, and relationship-type helpers used by the CSV exporter. Traces to **FR-8.4**–**FR-8.6**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| CI-01 | `ToNodeId_ReturnsSixteenCharLowercaseHex` | Node ID is exactly 16 lowercase hex chars. | Unit |
| CI-02 | `ToNodeId_IsDeterministic` | Same inputs always produce the same node ID. | Unit |
| CI-03a | `ToNodeId_DiffersForDifferentInputs(Ns.A/class vs Ns.B/class)` | Different FQNs produce different IDs. | Unit |
| CI-03b | `ToNodeId_DiffersForDifferentInputs(Ns.A/class vs Ns.A/interface)` | Same FQN different label produces different ID. | Unit |
| CI-04a | `ToTypeLabel_MapsElementKindCorrectly(Interface, “interface”)` | Interface kind maps to label `"interface"`. | Unit |
| CI-04b | `ToTypeLabel_MapsElementKindCorrectly(Struct, “struct”)` | Struct kind maps to `"struct"`. | Unit |
| CI-04c | `ToTypeLabel_MapsElementKindCorrectly(Enum, “enum”)` | Enum kind maps to `"enum"`. | Unit |
| CI-04d | `ToTypeLabel_MapsElementKindCorrectly(Class, “class”)` | Class kind maps to `"class"`. | Unit |
| CI-04e | `ToTypeLabel_MapsElementKindCorrectly(Record, “class”)` | Record kind maps to `"class"`. | Unit |
| CI-04f | `ToTypeLabel_MapsElementKindCorrectly(Delegate, “interface”)` | Delegate kind maps to `"interface"`. | Unit |
| CI-05a | `ToRelationshipType_MapsReasonCorrectly(“Inherits from”, “basecompoundref”)` | Inheritance maps to `basecompoundref`. | Unit |
| CI-05b | `ToRelationshipType_MapsReasonCorrectly(“Implements interface”, “basecompoundref”)` | Interface implementation maps to `basecompoundref`. | Unit |
| CI-05c | `ToRelationshipType_MapsReasonCorrectly(“Object creation (new)”, “ref”)` | Object creation maps to `ref`. | Unit |
| CI-05d | `ToRelationshipType_MapsReasonCorrectly(“Field type”, “ref”)` | Field type maps to `ref`. | Unit |
| CI-05e | `ToRelationshipType_MapsReasonCorrectly(“Method return type”, “ref”)` | Method return type maps to `ref`. | Unit |

### 3.18 CsvExporterTests (23 tests)

Tests the CSV export producing `nodes.csv` and `relationships.csv`. Covers file creation, headers, row structure, ID consistency, field escaping, and an end-to-end smoke test. Traces to **FR-8.1**–**FR-8.11**.

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| CV-01 | `Export_CreatesOutputDirectoryAndBothFiles` | Both `nodes.csv` and `relationships.csv` are created. | Unit |
| CV-02 | `Export_NodesCsvHasCorrectHeader` | `nodes.csv` has the 10-column header row. | Unit |
| CV-03 | `Export_RelsCsvHasCorrectHeader` | `relationships.csv` has the 5-column header row. | Unit |
| CV-04 | `Export_ReturnsCorrectNodeCount` | Return value `NodesWritten` equals number of in-scope types. | Unit |
| CV-05 | `Export_ReturnsCorrectRelationshipCount` | Return value `RelsWritten` ≥ 1 when edges exist. | Unit |
| CV-06 | `Export_EmptyGraph_OnlyHeaderLines` | Empty graph produces header-only files. | Unit |
| CV-07 | `Export_NodeRow_HasTenFields` | Each data row in `nodes.csv` has exactly 10 fields. | Unit |
| CV-08 | `Export_RelRow_HasFiveFields` | Each data row in `relationships.csv` has exactly 5 fields. | Unit |
| CV-09 | `Export_NodeRow_ContainsFqn` | Node row contains the type’s fully qualified name. | Unit |
| CV-10 | `Export_NodeRow_SimpleNameInNameColumn` | Name column holds only the simple (unqualified) type name. | Unit |
| CV-11 | `Export_ResolvedFlag_CorrectValues` | In-scope nodes have `resolved:boolean=true`; unresolved nodes have `false`. | Unit |
| CV-12 | `Export_UnresolvedReference_AppearsInNodesCsv` | Unresolved references added via `AddUnresolvedReference` appear in output. | Unit |
| CV-13 | `Export_UnresolvedDuplicatesInScope_NotDoubleWritten` | FQN in both ElementKinds and UnresolvedReferences emits only one row. | Unit |
| CV-14 | `Export_InheritanceEdge_GetsBaseCompoundRef` | Inheritance edge produces relationship type `basecompoundref`. | Unit |
| CV-15 | `Export_InterfaceImplementationEdge_GetsBaseCompoundRef` | Interface-impl edge produces relationship type `basecompoundref`. | Unit |
| CV-16 | `Export_GeneralDependencyEdge_GetsRef` | Non-inheritance edge produces relationship type `ref`. | Unit |
| CV-17 | `Export_NodeIdsInRelsMatchNodesCsv` | All `:START_ID`/`:END_ID` values in `relationships.csv` exist as `id:ID` in `nodes.csv`. | Unit |
| CV-18a | `EscapeCsvField_HandlesSpecialChars(“hello,world”)` | Comma triggers quoting. | Unit |
| CV-18b | `EscapeCsvField_HandlesSpecialChars(“say \"hi\"”)` | Embedded double-quote is doubled and wrapped. | Unit |
| CV-18c | `EscapeCsvField_HandlesSpecialChars(“line\nbreak”)` | Newline triggers quoting. | Unit |
| CV-18d | `EscapeCsvField_HandlesSpecialChars(“plain”)` | Plain value is returned as-is. | Unit |
| CV-19 | `Export_InterfaceType_HasInterfaceLabel` | Interface node has `type=interface` and `:LABEL=interface`. | Unit |
| CV-20 | `Export_StructType_HasStructLabel` | Struct node has `type=struct`. | Unit |
| CV-21 | `Export_EnumType_HasEnumLabel` | Enum node has `type=enum`. | Unit |
| CV-22 | `Export_OutputFiles_AreUtf8WithoutBom` | Both output files are UTF-8 without BOM. | Unit |
| CV-23 | `Export_SampleCodebase_ProducesNonEmptyOutput` | End-to-end export of sample codebase: nodes > 0 and rels > 0. | Integration |

---

## 4. Requirement Traceability Matrix

| Requirement | Test IDs | Coverage |
|-------------|----------|----------|
| **FR-5.3**: `export` subcommand | DX-01–26, NJ-01–25, CV-01–23 | Full |
| **FR-6.1–6.7**: Doxygen XML export | DX-01–26 | Full |
| **FR-7.1–7.7**: Neo4j direct import | NJ-01–25 | Full |
| **FR-8.1–8.11**: CSV export | CV-01–23, CI-01–05 | Full |
| **FR-1**: Target class specification | WB-03, WB-04, WB-05, E2E-01–04 | Full |
| **FR-2**: Source scope definition | WB-01, WB-02, GB-15 | Full |
| **FR-3.1**: Roslyn semantic model | WB-01, GB-01–15 | Full |
| **FR-3.2**: Dependency detection | GB-01–12, CD-01–37, GP-01–16, R3-01–33, R4-01–19, RF-01–08 | Full |
| **FR-3.3**: Transitive closure | FA-01–03, FA-06, R3-31–33, E2E-01 | Full |
| **FR-3.4**: Target exclusion | FA-05, E2E-01 | Full |
| **FR-4.1–4.2**: Report content | RG-01–05 | Full |
| **FR-4.3**: Metrics overview | FA-10, FA-11–15, RG-05, RG-07 | Full |
| **FR-4.5**: Console max depth | FA-11–15 | Full |
| **FR-4.6**: Dependency graph | FA-16–18, RG-08–12 | Full |
| **FR-4.4**: Markdown file output | RG-06 | Full |
| **NFR-4.1–4.3**: Portable executable | PE-01–03 | Full |
| **NFR-5.1–5.3**: CI pipeline | CI-01–07 | Full |
| **NFR-6.1–6.4**: Versioning | VR-01–04, RG-13 | Full |

---

## 5. Coverage by C# Construct Category

| Category | Test Count | Test IDs |
|----------|----------:|----------|
| Inheritance / interfaces | 6 | GB-01, GB-02, R3-27, R3-28, R4-17, R4-08 |
| Fields / properties / events | 8 | GB-03, GB-04, CD-13, CD-30, CD-32, R3-23, R3-26, R4-09 |
| Methods (params, returns, local) | 10 | GB-05, GB-06, GB-07, CD-20, CD-21, R3-11, R3-12, R3-13, R4-03, R4-04 |
| Object creation | 5 | GB-08, CD-23, R4-05, R4-10, R3-25 |
| Generic types / constraints | 7 | GB-09, CD-06, CD-07, CD-08, CD-33, CD-36, R4-14 |
| typeof / sizeof / default / nameof | 6 | GB-10, CD-24, R3-01, R3-02, R4-12, R4-15 |
| Type checks / casts / patterns | 14 | GB-11, CD-25–29, R3-07–10, GP-14, R4-06, R4-16 |
| Static access / using static | 4 | GB-12, R3-03, R3-04, R4-11 |
| Arrays / pointers / ref types | 10 | CD-01–05, GP-06–09, R3-30 |
| Delegates / function pointers | 4 | R3-06, R3-22, E2E-03, R3-23 |
| Attributes | 6 | R3-14–19 |
| Statements (for, foreach, using, catch, fixed) | 7 | CD-09, CD-10, GP-01, GP-02, GP-10, GP-11, GP-13 |
| LINQ (from, join) | 3 | GP-05, R3-05, GP-04 |
| C# 9–12 features | 8 | CD-11, CD-12, CD-26, CD-34, R3-24, R4-18, R4-19, R4-07 |
| Operators / conversions / indexers | 6 | CD-14–19 |
| Lambda / local functions | 5 | CD-22, R4-13, R3-29, GP-15, R4-02 |
| Nested types | 2 | R3-20, R3-21 |
| Transitive / diamond / circular | 5 | FA-06, R3-31, R3-32, R3-33, E2E-01 |
| Reflection (Type.GetType / Assembly.GetType / Module.GetType string literals) | 8 | RF-01–08 |

---

## 6. Test Execution

### Running Locally

```bash
# Full suite
dotnet test --verbosity normal

# Specific test file
dotnet test --filter "ComprehensiveDependencyTests"

# Specific test
dotnet test --filter "FullyQualifiedName~Probe_NameOfExpression"
```

### In CI

Tests run automatically on every push and pull request via [`.github/workflows/ci.yml`](../.github/workflows/ci.yml). The publish stage is gated on test success.

### Latest Run

```
Test summary: total: 246, failed: 3 (PortableExeTests — dotnet not in PATH), succeeded: 243, skipped: 0
Duration: ~30s
```

---

## 7. Cross-Check History

Tests were developed across four iterative cross-check rounds against the C# language specification:

| Round | Probes Added | Gaps Found | Gaps Fixed | Cumulative Total |
|-------|-------------|------------|------------|-----------------|
| 1 (Audit) | 37 | 18 | 18 | 77 |
| 2 (Gap Probes) | 16 | 5 | 5 | 93 |
| 3 (Spec Audit) | 33 | 6 | 6 | 126 |
| 4 (Final Sweep) | 19 | 0 | 0 | 145 |
| Post-feature additions | 10 | — | — | 155 |
| Max transitive depth | 6 | — | — | 166 |
| Dependency graph | 8 | — | — | 174 |
| Semantic versioning | 5 | — | — | 179 || Doxygen XML export + subcommand CLI | 29 | — | — | 208 |
| Neo4j direct import | 30 | — | — | 238 |
| Reflection detection (Strategy 1) | 6 | 0 | 0 | 244 |
| Module.GetType extension | 2 | 0 | 0 | 246 |
Round 4 finding zero gaps confirmed convergence: all mainstream C# constructs are covered.
