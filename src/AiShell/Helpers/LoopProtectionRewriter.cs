using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AJTools.AiShell.Helpers
{
    // KNOWN GAP: this only instruments while/for/do/foreach - a script with unbounded or mutual
    // recursion (e.g. a recursive family/geometry tree walk with a wrong base case) is NOT
    // protected and will hit a real StackOverflowException, which .NET cannot let any try/catch
    // intercept - it terminates the whole Revit process outright, unlike the "hangs" this file's
    // callers (RevitExecutionService/ReplSessionService) already document as a known, survivable
    // limitation. Closing this would mean instrumenting every method/local-function declaration
    // and call site with a depth counter - a much bigger, riskier change to this rewriter than
    // the loop checks below, and one that needs a live Revit test loop to verify safely. Left
    // undone here on purpose rather than guessed at blind; see docs/AJ-AI-Testing-Checklist.md
    // "Known limitations" for the user-facing note.
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
    }
}
