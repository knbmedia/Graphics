using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

using Object = UnityEngine.Object;

namespace UnityEditor.VFX
{
    class VFXExpressionGraph
    {
        private struct ExpressionData
        {
            public int depth;
            public int index;
        }

        public VFXExpressionGraph()
        {}

        private void AddExpressionsToContext(HashSet<VFXExpression> expressions, IVFXSlotContainer slotContainer)
        {
            int nbSlots = slotContainer.GetNbInputSlots();
            for (int i = 0; i < nbSlots; ++i)
            {
                var slot = slotContainer.GetInputSlot(i);
                slot.GetExpressions(expressions);
            }
        }

        private VFXExpression.Context CreateLocalExpressionContext(VFXContext context, VFXExpression.Context.ReductionOption option)
        {
            var expressionContext = new VFXExpression.Context(option);
            var expressions = new HashSet<VFXExpression>();

            // First add context slots
            AddExpressionsToContext(expressions, context);

            // Then block slots
            foreach (var child in context.children)
                AddExpressionsToContext(expressions, child);

            foreach (var exp in expressions)
                expressionContext.RegisterExpression(exp);

            return expressionContext;
        }

        private void AddExpressionDataRecursively(Dictionary<VFXExpression, ExpressionData> dst, VFXExpression exp, int depth = 0)
        {
            ExpressionData data;
            data.index = -1; // Will be overridden later on
            if (!dst.TryGetValue(exp, out data) || data.depth < depth)
            {
                data.depth = depth;
                dst[exp] = data;
                foreach (var parent in exp.Parents)
                    AddExpressionDataRecursively(dst, parent, depth + 1);
            }
        }

        public void CompileExpressions(VFXGraph graph, VFXExpression.Context.ReductionOption option)
        {
            Profiler.BeginSample("CompileExpressionGraph");

            try
            {
                m_Expressions.Clear();
                m_SlotsToExpressions.Clear();
                m_FlattenedExpressions.Clear();
                m_ExpressionsData.Clear();

                var models = new HashSet<Object>();
                graph.CollectDependencies(models);
                var contexts = models.OfType<VFXContext>();

                foreach (var context in contexts.ToArray())
                {
                    var expressionContext = CreateLocalExpressionContext(context, option);
                    expressionContext.Compile();

                    m_Expressions.UnionWith(expressionContext.BuildAllReduced());

                    models.Clear();
                    context.CollectDependencies(models);

                    var kvps = models.OfType<VFXSlot>()
                        .Where(s => s.IsMasterSlot())
                        .SelectMany(s => s.GetExpressionSlots())
                        .Select(s => new KeyValuePair<VFXSlot, VFXExpression>(s, expressionContext.GetReduced(s.GetExpression())));

                    foreach (var kvp in kvps)
                        m_SlotsToExpressions.Add(kvp.Key, kvp.Value);
                }

                // flatten
                foreach (var exp in m_SlotsToExpressions.Values)
                    AddExpressionDataRecursively(m_ExpressionsData, exp);

                var sortedList = m_ExpressionsData.Where(kvp => !kvp.Key.Is(VFXExpression.Flags.PerElement)).ToList(); // remove per element expression from flattened data
                sortedList.Sort((kvpA, kvpB) => kvpB.Value.depth.CompareTo(kvpA.Value.depth));
                m_FlattenedExpressions = sortedList.Select(kvp => kvp.Key).ToList();

                // update index in expression data
                for (int i = 0; i < m_FlattenedExpressions.Count; ++i)
                {
                    var data = m_ExpressionsData[m_FlattenedExpressions[i]];
                    data.index = i;
                    m_ExpressionsData[m_FlattenedExpressions[i]] = data;
                }

                //Debug.Log("---- Expression list");
                //for (int i = 0; i < m_FlattenedExpressions.Count; ++i)
                //    Debug.Log(string.Format("{0}\t\t{1}", i, m_FlattenedExpressions[i].GetType().Name));

                Debug.Log(string.Format("RECOMPILE EXPRESSION GRAPH - NB EXPRESSIONS: {0} - NB SLOTS: {1}", m_Expressions.Count, m_SlotsToExpressions.Count));
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        public int GetFlattenedIndex(VFXExpression exp)
        {
            if (m_ExpressionsData.ContainsKey(exp))
                return m_ExpressionsData[exp].index;
            return -1;
        }

        public HashSet<VFXExpression> Expressions { get { return m_Expressions; } }
        public Dictionary<VFXSlot, VFXExpression> SlotsToExpressions { get { return m_SlotsToExpressions; } }
        public List<VFXExpression> FlattenedExpressions { get { return m_FlattenedExpressions; } }

        private HashSet<VFXExpression> m_Expressions = new HashSet<VFXExpression>();
        private Dictionary<VFXSlot, VFXExpression> m_SlotsToExpressions = new Dictionary<VFXSlot, VFXExpression>();
        private List<VFXExpression> m_FlattenedExpressions = new List<VFXExpression>();
        private Dictionary<VFXExpression, ExpressionData> m_ExpressionsData = new Dictionary<VFXExpression, ExpressionData>();
    }
}
