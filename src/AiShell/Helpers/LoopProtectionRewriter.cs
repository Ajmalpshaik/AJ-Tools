using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AJTools.AiShell.Helpers
{
    // Instruments while/for/do/foreach loops with a cancellation checkpoint (below), and
    // method/local-function/lambda bodies with a recursion-depth guard (RecursionGuard region)
    // so unbounded/mutual recursion throws a catchable OperationCanceledException instead of a
    // real StackOverflowException - which .NET cannot let any try/catch intercept and terminates
    // the whole Revit process outright, unlike the "hangs" this file's callers
    // (RevitExecutionService/ReplSessionService) already document as a known, survivable limitation.
    //
    // RECURSION GUARD SCOPE, READ BEFORE CHANGING: only block-bodied ("{ ... }") local functions,
    // methods, and lambdas are instrumented. An expression-bodied recursive form (e.g.
    // "int Fib(int n) => n <= 1 ? n : Fib(n - 1) + Fib(n - 2);" or a lambda written the same way)
    // is deliberately left uninstrumented - safely rewriting one needs to know whether to emit a
    // "return" (Func) or not (Action), which needs real type/semantic info this is a purely
    // syntactic rewriter (matches the existing loop-rewriting style above). In this codebase's
    // actual generated scripts, every real example is block-bodied, so this covers the realistic
    // case, not the theoretical worst case.
    //
    // MaxRecursionDepth IS A CONSERVATIVE ESTIMATE, NOT A LIVE-VERIFIED NUMBER: no Revit connection
    // was available to actually run a runaway-recursion script and see how deep the real call stack
    // gets before overflowing (stack frame cost varies with how much a Revit API call chain adds on
    // top of each script-level recursion step, and interop/COM frames can be heavier than plain
    // managed ones). 300 is chosen to sit far above any legitimate BIM tree-walk (family/group/room-
    // boundary nesting essentially never exceeds double digits) and comfortably below where a 1MB
    // default thread stack is expected to overflow even under pessimistic per-frame assumptions -
    // but "comfortably below" here is an estimate, not a measurement. Tune this once it can actually
    // be tested live (see docs/AJ-AI-Testing-Checklist.md).
    public class LoopProtectionRewriter : CSharpSyntaxRewriter
    {
        private StatementSyntax GetCancellationCheck()
        {
            var code = @"if (CancellationToken.IsCancellationRequested) throw new System.OperationCanceledException(""Script execution timed out (infinite loop protection)."");";
            return SyntaxFactory.ParseStatement(code).NormalizeWhitespace();
        }

        private BlockSyntax EnsureBlock(StatementSyntax statement)
        {
            if (statement is BlockSyntax block)
            {
                return block.WithStatements(block.Statements.Insert(0, GetCancellationCheck()));
            }

            return SyntaxFactory.Block(
                GetCancellationCheck(),
                statement
            );
        }

        public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
        {
            var rewrittenNode = (WhileStatementSyntax)base.VisitWhileStatement(node);
            return rewrittenNode.WithStatement(EnsureBlock(rewrittenNode.Statement));
        }

        public override SyntaxNode VisitForStatement(ForStatementSyntax node)
        {
            var rewrittenNode = (ForStatementSyntax)base.VisitForStatement(node);
            return rewrittenNode.WithStatement(EnsureBlock(rewrittenNode.Statement));
        }

        public override SyntaxNode VisitDoStatement(DoStatementSyntax node)
        {
            var rewrittenNode = (DoStatementSyntax)base.VisitDoStatement(node);
            return rewrittenNode.WithStatement(EnsureBlock(rewrittenNode.Statement));
        }

        public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
        {
            var rewrittenNode = (ForEachStatementSyntax)base.VisitForEachStatement(node);
            return rewrittenNode.WithStatement(EnsureBlock(rewrittenNode.Statement));
        }

        #region RecursionGuard

        private const int MaxRecursionDepth = 300;
        private const string DepthCounterName = "__ajRecursionDepth";
        private bool _recursionGuardNeeded;

        private BlockSyntax GuardRecursiveBody(BlockSyntax body)
        {
            _recursionGuardNeeded = true;

            var depthCheck = SyntaxFactory.ParseStatement(
                $@"if ({DepthCounterName} > {MaxRecursionDepth}) throw new System.OperationCanceledException(""Recursion depth exceeded {MaxRecursionDepth} (unbounded recursion protection)."");")
                .NormalizeWhitespace();
            var increment = SyntaxFactory.ParseStatement($"{DepthCounterName}++;").NormalizeWhitespace();
            var decrement = SyntaxFactory.ParseStatement($"{DepthCounterName}--;").NormalizeWhitespace();

            var tryBlock = SyntaxFactory.Block(body.Statements.Insert(0, depthCheck));
            var tryStatement = SyntaxFactory.TryStatement(
                tryBlock,
                SyntaxFactory.List<CatchClauseSyntax>(),
                SyntaxFactory.FinallyClause(SyntaxFactory.Block(decrement)));

            return SyntaxFactory.Block(increment, tryStatement).NormalizeWhitespace();
        }

        public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var rewrittenNode = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node);
            if (rewrittenNode.Body == null) return rewrittenNode; // expression-bodied - see class doc
            return rewrittenNode.WithBody(GuardRecursiveBody(rewrittenNode.Body));
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var rewrittenNode = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
            if (rewrittenNode.Body == null) return rewrittenNode; // expression-bodied/abstract/extern
            return rewrittenNode.WithBody(GuardRecursiveBody(rewrittenNode.Body));
        }

        public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            var rewrittenNode = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node);
            if (!(rewrittenNode.Body is BlockSyntax block)) return rewrittenNode; // expression-bodied
            return rewrittenNode.WithBody(GuardRecursiveBody(block));
        }

        public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            var rewrittenNode = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node);
            if (!(rewrittenNode.Body is BlockSyntax block)) return rewrittenNode; // expression-bodied
            return rewrittenNode.WithBody(GuardRecursiveBody(block));
        }

        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            // Runs after every descendant has already been visited (base call recurses first),
            // so _recursionGuardNeeded is only set here once we know the real answer - avoids
            // declaring an unused __ajRecursionDepth in the (common) case of a script with no
            // local function/method/block-lambda to guard at all.
            var rewrittenNode = (CompilationUnitSyntax)base.VisitCompilationUnit(node);
            if (!_recursionGuardNeeded) return rewrittenNode;

            var counterDeclaration = SyntaxFactory.GlobalStatement(
                SyntaxFactory.ParseStatement($"int {DepthCounterName} = 0;").NormalizeWhitespace());
            return rewrittenNode.WithMembers(rewrittenNode.Members.Insert(0, counterDeclaration));
        }

        #endregion
    }
}
