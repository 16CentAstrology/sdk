﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    internal static class AnalyzerOptionsExtensions
    {
        private const string DotnetAnalyzerDiagnosticPrefix = "dotnet_analyzer_diagnostic";
        private const string CategoryPrefix = "category";
        private const string SeveritySuffix = "severity";

        private const string DotnetAnalyzerDiagnosticSeverityKey = DotnetAnalyzerDiagnosticPrefix + "." + SeveritySuffix;

        private static string GetCategoryBasedDotnetAnalyzerDiagnosticSeverityKey(string category)
            => $"{DotnetAnalyzerDiagnosticPrefix}.{CategoryPrefix}-{category}.{SeveritySuffix}";

        /// <summary>
        /// Tries to get configured severity for the given <paramref name="descriptor"/>
        /// for the given <paramref name="tree"/> from bulk configuration analyzer config options, i.e.
        ///     'dotnet_analyzer_diagnostic.category-%RuleCategory%.severity = %severity%'
        ///         or
        ///     'dotnet_analyzer_diagnostic.severity = %severity%'
        /// </summary>
        public static bool TryGetSeverityFromBulkConfiguration(
            this AnalyzerOptions? analyzerOptions,
            SyntaxTree tree,
            Compilation compilation,
            DiagnosticDescriptor descriptor,
            out ReportDiagnostic severity)
        {
            // Analyzer bulk configuration does not apply to:
            //  1. Disabled by default diagnostics
            //  2. Compiler diagnostics
            //  3. Non-configurable diagnostics
            if (analyzerOptions == null ||
                !descriptor.IsEnabledByDefault ||
                descriptor.CustomTags.Any(tag => tag == WellKnownDiagnosticTags.Compiler || tag == WellKnownDiagnosticTags.NotConfigurable))
            {
                severity = default;
                return false;
            }

            // If user has explicitly configured severity for this diagnostic ID, that should be respected.
            if (compilation.Options.SpecificDiagnosticOptions.TryGetValue(descriptor.Id, out severity))
            {
                return true;
            }

            // If user has explicitly configured severity for this diagnostic ID, that should be respected.
            // For example, 'dotnet_diagnostic.CA1000.severity = error'
            if (tree.DiagnosticOptions.TryGetValue(descriptor.Id, out severity))
            {
                return true;
            }

            var analyzerConfigOptions = analyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);

            // If user has explicitly configured default severity for the diagnostic category, that should be respected.
            // For example, 'dotnet_analyzer_diagnostic.category-security.severity = error'
            var categoryBasedKey = GetCategoryBasedDotnetAnalyzerDiagnosticSeverityKey(descriptor.Category);
            if (analyzerConfigOptions.TryGetValue(categoryBasedKey, out var value) &&
                TryParseSeverity(value, out severity))
            {
                return true;
            }

            // Otherwise, if user has explicitly configured default severity for all analyzer diagnostics, that should be respected.
            // For example, 'dotnet_analyzer_diagnostic.severity = error'
            if (analyzerConfigOptions.TryGetValue(DotnetAnalyzerDiagnosticSeverityKey, out value) &&
                TryParseSeverity(value, out severity))
            {
                return true;
            }

            severity = default;
            return false;
        }

        internal static bool TryParseSeverity(string value, out ReportDiagnostic severity)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            if (comparer.Equals(value, "default"))
            {
                severity = ReportDiagnostic.Default;
                return true;
            }
            else if (comparer.Equals(value, "error"))
            {
                severity = ReportDiagnostic.Error;
                return true;
            }
            else if (comparer.Equals(value, "warning"))
            {
                severity = ReportDiagnostic.Warn;
                return true;
            }
            else if (comparer.Equals(value, "suggestion"))
            {
                severity = ReportDiagnostic.Info;
                return true;
            }
            else if (comparer.Equals(value, "silent") || comparer.Equals(value, "refactoring"))
            {
                severity = ReportDiagnostic.Hidden;
                return true;
            }
            else if (comparer.Equals(value, "none"))
            {
                severity = ReportDiagnostic.Suppress;
                return true;
            }

            severity = default;
            return false;
        }
    }
}
