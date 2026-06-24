using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AJTools.GeminiShell.Helpers
{
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
