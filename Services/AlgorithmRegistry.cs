using OdometerTool.Algorithms;
using OdometerTool.Models;

namespace OdometerTool.Services;

public static class AlgorithmRegistry
{
    public static readonly List<EepromAlgorithm> All = new()
    {
        new HondaAccordYNS93C76(),
        new HondaYNS93C86(),
    };
}
