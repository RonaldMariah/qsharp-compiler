﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Quantum.QsCompiler.SyntaxTree;

#nullable enable

namespace Microsoft.Quantum.QsCompiler.DependencyAnalysis
{
    using Range = DataTypes.Range;

    /// <summary>
    /// Base class for call graph edge types.
    /// </summary>
    public abstract class CallGraphEdgeBase : IEquatable<CallGraphEdgeBase>
    {
        /// <summary>
        /// Name of the callable where the reference was made.
        /// </summary>
        public QsQualifiedName FromCallableName { get; }

        /// <summary>
        /// Name of the callable being referenced.
        /// </summary>
        public QsQualifiedName ToCallableName { get; }

        /// <summary>
        /// The range of the reference represented by the edge.
        /// </summary>
        public Range ReferenceRange { get; }

        /// <summary>
        /// Base constructor for call graph edges. Initializes CallGraphEdgeBase properties.
        /// Throws an ArgumentNullException if any of the arguments are null.
        /// </summary>
        protected CallGraphEdgeBase(QsQualifiedName fromCallableName, QsQualifiedName toCallableName, Range referenceRange)
        {
            if (fromCallableName is null)
            {
                throw new ArgumentNullException(nameof(fromCallableName));
            }

            if (toCallableName is null)
            {
                throw new ArgumentNullException(nameof(toCallableName));
            }

            if (referenceRange is null)
            {
                throw new ArgumentNullException(nameof(referenceRange));
            }

            this.FromCallableName = fromCallableName;
            this.ToCallableName = toCallableName;
            this.ReferenceRange = referenceRange;
        }

        /// <inheritdoc/>
        public bool Equals(CallGraphEdgeBase edge) =>
            this.FromCallableName.Equals(edge.FromCallableName)
            && this.ToCallableName.Equals(edge.ToCallableName)
            && this.ReferenceRange.Equals(edge.ReferenceRange);
    }

    /// <summary>
    /// Base class for call graph node types.
    /// </summary>
    public abstract class CallGraphNodeBase : IEquatable<CallGraphNodeBase>
    {
        /// <summary>
        /// The name of the represented callable.
        /// </summary>
        public QsQualifiedName CallableName { get; }

        /// <summary>
        /// Base constructor for call graph nodes. Initializes CallableName.
        /// Throws an ArgumentNullException if argument is null.
        /// </summary>
        protected CallGraphNodeBase(QsQualifiedName callableName)
        {
            if (callableName is null)
            {
                throw new ArgumentException(nameof(callableName));
            }

            this.CallableName = callableName;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CallGraphNodeBase && this.Equals((CallGraphNodeBase)obj);
        }

        /// <inheritdoc/>
        public bool Equals(CallGraphNodeBase other) =>
            this.CallableName.Equals(other.CallableName);

        /// <inheritdoc/>
        public override int GetHashCode() => this.CallableName.GetHashCode();
    }

    /// <summary>
    /// Base class for call graph types.
    /// </summary>
    public abstract class CallGraphBase<TNode, TEdge>
        where TNode : CallGraphNodeBase
        where TEdge : CallGraphEdgeBase
    {
        // Static Elements

        /// <summary>
        /// Returns an empty dependency for a node.
        /// </summary>
        private static ILookup<TNode, TEdge> EmptyDependency() =>
            ImmutableArray<KeyValuePair<TNode, TEdge>>.Empty
            .ToLookup(kvp => kvp.Key, kvp => kvp.Value);

        // Member Fields

        /// <summary>
        /// This is a dictionary mapping source nodes to information about target nodes. This information is represented
        /// by a dictionary mapping target node to the edges pointing from the source node to the target node.
        /// </summary>
        private readonly Dictionary<TNode, Dictionary<TNode, ImmutableArray<TEdge>>> dependencies =
            new Dictionary<TNode, Dictionary<TNode, ImmutableArray<TEdge>>>();

        // Properties

        /// <summary>
        /// The number of nodes in the call graph.
        /// </summary>
        public int Count => this.dependencies.Count;

        /// <summary>
        /// A hash set of the nodes in the call graph.
        /// </summary>
        public ImmutableHashSet<TNode> Nodes => this.dependencies.Keys.ToImmutableHashSet();

        // Member Methods

        /// <summary>
        /// Returns the children nodes of a given node. Each key in the returned lookup is a child
        /// node of the given node. Each value in the lookup is an edge connecting the given node to
        /// the child node represented by the associated key.
        /// Returns an empty ILookup if the node was found with no dependencies or was not found in
        /// the graph.
        /// Throws ArgumentNullException if argument is null.
        /// </summary>
        public ILookup<TNode, TEdge> GetDirectDependencies(TNode node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (this.dependencies.TryGetValue(node, out var dep))
            {
                return dep
                    .SelectMany(kvp => kvp.Value, Tuple.Create)
                    .ToLookup(tup => tup.Item1.Key, tup => tup.Item2);
            }
            else
            {
                return EmptyDependency();
            }
        }

        /// <summary>
        /// Adds a dependency to the call graph using the two nodes and the edge between them.
        /// The nodes are added to the graph if they are not already there. The edge is always added.
        /// Throws ArgumentNullException if any of the arguments are null.
        /// </summary>
        protected void AddDependency(TNode fromNode, TNode toNode, TEdge edge)
        {
            if (fromNode is null)
            {
                throw new ArgumentNullException(nameof(fromNode));
            }

            if (toNode is null)
            {
                throw new ArgumentNullException(nameof(toNode));
            }

            if (edge is null)
            {
                throw new ArgumentNullException(nameof(edge));
            }

            if (this.dependencies.TryGetValue(fromNode, out var deps))
            {
                if (!deps.TryGetValue(toNode, out var edges))
                {
                    deps[toNode] = ImmutableArray.Create(edge);
                }
                else
                {
                    deps[toNode] = edges.Add(edge);
                }
            }
            else
            {
                var newDeps = new Dictionary<TNode, ImmutableArray<TEdge>>();
                newDeps[toNode] = ImmutableArray.Create(edge);
                this.dependencies[fromNode] = newDeps;
            }

            // Need to make sure the each dependencies has an entry for each node
            // in the graph, even if node has no dependencies.
            this.AddNode(toNode);
        }

        /// <summary>
        /// Adds the given node to the call graph, if it is not already in the graph.
        /// Throws ArgumentNullException if the argument is null.
        /// </summary>
        internal void AddNode(TNode node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (!this.dependencies.ContainsKey(node))
            {
                this.dependencies[node] = new Dictionary<TNode, ImmutableArray<TEdge>>();
            }
        }
    }
}
