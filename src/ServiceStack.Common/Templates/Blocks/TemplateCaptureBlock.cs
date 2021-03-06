﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Text;

namespace ServiceStack.Templates
{
    /// <summary>
    /// Captures the output and assigns it to the specified variable.
    /// Accepts an optional Object Dictionary as scope arguments when evaluating body.
    /// Effectively is similar 
    ///
    /// Usages: {{#capture output}} {{#each args}} - [{{it}}](/path?arg={{it}}) {{/each}} {{/capture}}
    ///         {{#capture output {nums:[1,2,3]} }} {{#each nums}} {{it}} {{/each}} {{/capture}}
    ///         {{#capture appendTo output {nums:[1,2,3]} }} {{#each nums}} {{it}} {{/each}} {{/capture}}
    /// </summary>
    public class TemplateCaptureBlock : TemplateBlock
    {
        public override string Name => "capture";

        //Extract Span out of async method 
        private (string name, Dictionary<string, object> scopeArgs, bool appendTo) Parse(TemplateScopeContext scope, PageBlockFragment fragment)
        {
            if (fragment.Argument.IsNullOrWhiteSpace())
                throw new NotSupportedException("'capture' block is missing name of variable to assign captured output to");
            
            var literal = fragment.Argument.AdvancePastWhitespace();
            bool appendTo = false;
            if (literal.StartsWith("appendTo "))
            {
                appendTo = true;
                literal = literal.Advance("appendTo ".Length);
            }
                
            literal = literal.ParseVarName(out var name);
            if (name.IsNullOrEmpty())
                throw new NotSupportedException("'capture' block is missing name of variable to assign captured output to");

            literal = literal.AdvancePastWhitespace();

            var argValue = literal.GetJsExpressionAndEvaluate(scope);

            var scopeArgs = argValue as Dictionary<string, object>;

            if (argValue != null && scopeArgs == null)
                throw new NotSupportedException("Any 'capture' argument must be an Object Dictionary");

            return (name.ToString(), scopeArgs, appendTo);
        }

        public override async Task WriteAsync(TemplateScopeContext scope, PageBlockFragment fragment, CancellationToken cancel)
        {
            var (name, scopeArgs, appendTo) = Parse(scope, fragment);
            
            var ms = MemoryStreamFactory.GetStream();
            var useScope = scope.ScopeWith(scopeArgs, ms);

            await WriteBodyAsync(useScope, fragment, cancel);

            var capturedOutput = ms.ReadToEnd();

            if (appendTo && scope.PageResult.Args.TryGetValue(name, out var oVar)
                && oVar is string existingString)
            {
                scope.PageResult.Args[name] = existingString + capturedOutput;
                return;
            }
            
            scope.PageResult.Args[name] = capturedOutput;
        }
    }
}