using Framework;
using Google.Protobuf;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlowEditor.Models;

public sealed class FlowDef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<NodeDef> Nodes { get; set; } = [];
    public List<ConnectionDef> Connections { get; set; } = [];
}

public sealed class FlowListDef
{
    public string Name { get; set; } = "";
    public List<FlowDef> Flows { get; set; } = [];
}

public sealed class NodeDef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RunType { get; set; } = "";
    public FlowStepType StepType { get; set; } = FlowStepType.Action;
    public double? X { get; set; }
    public double? Y { get; set; }
}

public sealed class ConnectionDef
{
    public int FromNodeId { get; set; }
    public int ToNodeId { get; set; }
    public string Conditions { get; set; } = "";
    public string RunType { get; set; } = "";
}

public static class FlowListCodec
{
    public static FlowListDef Read(string path)
    {
        var source = Framework.FlowList.Parser.ParseFrom(File.ReadAllBytes(path));
        return new FlowListDef
        {
            Name = source.Name,
            Flows = source.Flows.Select(FromFlow).ToList()
        };
    }

    public static void Write(string path, string name, IEnumerable<FlowDef> flows)
    {
        var result = new Framework.FlowList { Name = name };
        result.Flows.AddRange(flows.Select(ToFlow));
        File.WriteAllBytes(path, result.ToByteArray());
    }

    public static Framework.Flow ToFlow(FlowDef source)
    {
        var result = new Framework.Flow { Id = source.Id, Name = source.Name };
        foreach (var node in source.Nodes.OrderBy(n => n.Id))
        {
            var step = new FlowStep
            {
                Id = node.Id,
                Name = node.Name,
                RunType = node.RunType,
                StepType = node.StepType,
                StepLocation = new StepLocation
                {
                    X = (float)(node.X ?? 0),
                    Y = (float)(node.Y ?? 0)
                }
            };
            step.NextSteps.AddRange(source.Connections
                .Where(c => c.FromNodeId == node.Id)
                .Select(c => new NextStep
                {
                    NextStepId = c.ToNodeId,
                    NextStepConditions = c.Conditions,
                    NextStepRunType = c.RunType
                }));
            result.Steps.Add(step);
        }
        return result;
    }

    private static FlowDef FromFlow(Framework.Flow source)
    {
        var result = new FlowDef { Id = source.Id, Name = source.Name };
        foreach (var step in source.Steps)
        {
            result.Nodes.Add(new NodeDef
            {
                Id = step.Id,
                Name = step.Name,
                RunType = step.RunType,
                StepType = step.StepType,
                X = step.StepLocation?.X,
                Y = step.StepLocation?.Y
            });
            result.Connections.AddRange(step.NextSteps.Select(next => new ConnectionDef
            {
                FromNodeId = step.Id,
                ToNodeId = next.NextStepId,
                Conditions = next.NextStepConditions,
                RunType = next.NextStepRunType
            }));
        }
        return result;
    }
}
