﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;
using Tracer.Fody.Helpers;
using Tracer.Fody.Weavers;

namespace Tracer.Fody.Filters.PatternFilter
{
    public class PatternFilter : ITraceLoggingFilter
    {
        private readonly List<PatternDefinition> _patternDefinitions;
        private readonly TraceAttributeHelper _traceAttributeHelper = new TraceAttributeHelper();

        public PatternFilter(IEnumerable<XElement> configElements) : this(ParseConfig(configElements))
        { }

        public PatternFilter(List<PatternDefinition> patternDefinitions)
        {
            _patternDefinitions = patternDefinitions;
        }

        public FilterResult ShouldAddTrace(MethodDefinition definition)
        {
            //attributes are stronger than patterns
            var shouldTrace = _traceAttributeHelper.ShouldTraceBasedOnMethodLevelInfo(definition) ??
                _traceAttributeHelper.ShouldTraceBasedOnClassLevelInfo(definition);

            if (shouldTrace.HasValue) return shouldTrace.Value;

            foreach (var patternDefinition in _patternDefinitions)
            {
                if (patternDefinition.IsMatching(definition))
                    return new FilterResult(patternDefinition.TraceEnabled, patternDefinition.Parameters);
            }

            //defaults to public methods of public classes only
            var result = (definition.IsPublic && definition.DeclaringType.IsPublic && !definition.IsConstructor &&
                    !definition.IsSetter
                    && !definition.IsGetter);

            return new FilterResult(result);
        }

        internal static List<PatternDefinition> ParseConfig(IEnumerable<XElement> configElements)
        {
            var patternDefinitions = configElements
                .Where(elem => elem.Name.LocalName.Equals("On", StringComparison.OrdinalIgnoreCase))
                .Select(it => PatternDefinition.ParseFromConfig(it, true))
                .Concat(configElements
                    .Where(elem => elem.Name.LocalName.Equals("Off", StringComparison.OrdinalIgnoreCase))
                    .Select(it => PatternDefinition.ParseFromConfig(it, false))).ToList();

            patternDefinitions.Sort();

            return patternDefinitions;
        }

        public void LogFilterInfo(IWeavingLogger weavingLogger)
        {
            weavingLogger.LogInfo("Using PatternFilter");
            weavingLogger.LogInfo("Pattern filter order:");
            int idx = 1;
            foreach (var patternDefinition in _patternDefinitions)
            {
                weavingLogger.LogInfo($"{idx++}. {patternDefinition}");
            }
        }
    }
}
