using System;
using System.Linq;
using System.Collections.Generic;
using RimArt;

static class Program
{
    static int checks;
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static FumaRules.Cell[] Trace(double x, double z, double tx, double tz)
    {
        var result = FumaRules.Trace(x, z, tx, tz).Take(1000).ToArray();
        Check(result.Length < 1000, "Traversal must terminate");
        Check(result.Last().X == Math.Floor(tx) && result.Last().Z == Math.Floor(tz), "Exact landing cell");
        Check(result.All(c => c.Entry >= 0 && c.Entry <= 1.00000001), "No steps beyond the selected landing point");
        Check(result.Select(c => c.Entry).SequenceEqual(result.Select(c => c.Entry).OrderBy(t => t)), "Flight order");
        return result;
    }

    static void Main()
    {
        Check(Trace(.5,.5,4.5,.5).Select(c => c.X).SequenceEqual(new[] {0,1,2,3,4}), "East corridor");
        Check(Trace(4.5,.5,.5,.5).Select(c => c.X).SequenceEqual(new[] {4,3,2,1,0}), "West corridor");
        var diagonal = Trace(.5,.5,2.5,2.5);
        Check(diagonal.Where(c => !c.Guard).Select(c => (c.X,c.Z)).SequenceEqual(new[] {(0,0),(1,1),(2,2)}), "Narrow diagonal");
        Check(diagonal.Where(c => c.Guard).Select(c => (c.X,c.Z)).SequenceEqual(new[] {(1,0),(0,1),(2,1),(1,2)}), "Wall guards at both corners");
        Check(diagonal.Any(c => c.Guard && c.X == 1 && c.Z == 0), "Wall beside corner blocks line");
        Check(!diagonal.Any(c => !c.Guard && c.X == 1 && c.Z == 0), "Pawn beside corner is outside damage corridor");
        Trace(2.5,.5,1,2); // Mixed-sign grid-boundary endpoint, formerly capable of overshooting forever.
        Trace(1,2,2.5,.5);
        Check(Trace(.5,.5,.5,.5).Length == 1, "Zero length segment");

        // Property: a frame-by-frame sweep must cover the same corridor as its preview.
        // Flight clips the same original ray using float progress, as the engine does.
        for (int x = -12; x <= 12; x++)
        for (int z = -12; z <= 12; z++)
        {
            if (x*x + z*z > 144) continue;
            var full = Trace(20.5,20.5,20.5+x,20.5+z);
            var preview = full.Where(c => !c.Guard).Select(c => (c.X,c.Z)).ToHashSet();
            var swept = new HashSet<(int,int)>();
            for (int tick = 0; tick < 31; tick++)
            {
                float a=tick/31f, b=(tick+1)/31f;
                foreach (var cell in FumaRules.Sweep(20.5,20.5,20.5+x,20.5+z,a,b).Where(c => !c.Guard))
                {
                    Check(cell.Entry >= a && cell.Exit <= b, "Shield subsegments stay within this tick");
                    swept.Add((cell.X,cell.Z));
                }
            }
            Check(preview.SetEquals(swept), $"Preview and swept path disagree at {x},{z}");
        }
        Check(Enumerable.Range(0,9).Select(FumaRules.Damage).SequenceEqual(new[] {30,24,19,15,12,10,8,8,8}), "Piercing falloff and floor");
        Check(FumaRules.Remaining(340,100) == 240 && FumaRules.Remaining(340,339) == 1
            && FumaRules.Remaining(340,400) == 0, "Absolute cooldown survives ownership changes and expires");
        Console.WriteLine($"Passed {checks} Fūma geometry, preview/sweep, damage and cooldown checks.");
    }
}
