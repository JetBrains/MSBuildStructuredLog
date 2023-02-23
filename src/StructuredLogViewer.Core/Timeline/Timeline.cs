﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Timeline
    {
        public Dictionary<int, Lane> Lanes { get; set; } = new();

        public Timeline(Build build, bool analyzeCpp)
        {
            Populate(build, analyzeCpp: analyzeCpp);
        }

        private void Populate(Build build, bool analyzeCpp = false)
        {
            build.VisitAllChildren<NamedNode>(node =>
            {
                if (analyzeCpp && node is CppAnalyzer.CppAnalyzerNode cppAnalyzerNode)
                {
                    var cppAnalyzer = cppAnalyzerNode.GetCppAnalyzer();
                    var cppTimedNodes = cppAnalyzer.GetAnalyzedTimedNode();

                    foreach (var cppTimedNode in cppTimedNodes)
                    {
                        // cppAnalyzer shouldn't create any new lanes.
                        if (Lanes.TryGetValue(cppTimedNode.NodeId, out var lane))
                        {
                            var block = new Block()
                            {
                                StartTime = cppTimedNode.StartTime,
                                EndTime = cppTimedNode.EndTime,
                                Text = cppTimedNode.Text,
                                Node = cppTimedNode.Node,
                            };
                            lane.Add(block);
                        }
                    }
                }

                if (node is not TimedNode timedNode)
                {
                    return;
                }

                if (timedNode is Build)
                {
                    return;
                }

                if (timedNode is Microsoft.Build.Logging.StructuredLogger.Task task &&
                    (string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(task.Name, "CallTarget", StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                {
                    var nodeId = timedNode.NodeId;
                    if (!Lanes.TryGetValue(nodeId, out var lane))
                    {
                        lane = new Lane();
                        Lanes[nodeId] = lane;
                    }

                    lane.Add(CreateBlock(timedNode));
                }
            });
        }

        private Block CreateBlock(TimedNode node)
        {
            var block = new Block();
            block.StartTime = node.StartTime;
            block.EndTime = node.EndTime;
            block.Text = node.Name;
            block.Indent = node.GetParentChainIncludingThis().Count();
            block.Node = node;
            block.HasError = node is Microsoft.Build.Logging.StructuredLogger.Task && node.FindFirstDescendant<Error>() != null;
            return block;
        }
    }
}
