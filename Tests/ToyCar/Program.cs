using System;
using RimArt;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

float speed = 0f;
for (int tick = 0; tick < 30; tick++)
{
    float next = ToyCarMotion.ChangeSpeed(speed, ToyCarMotion.MaxSpeed);
    Check(next > speed && next <= ToyCarMotion.MaxSpeed, "Acceleration must be gradual and capped");
    speed = next;
}
Check(Math.Abs(speed - ToyCarMotion.MaxSpeed) < 0.00001f, "Cruise reached in half a second");
Check(ToyCarMotion.CornerSpeed(90f) < ToyCarMotion.CornerSpeed(45f), "Sharp corners need lower speed");
Check(ToyCarMotion.CornerSpeed(180f) > 0f, "U-turns must not stall");
Check(ToyCarMotion.ApproachSpeed(0f, 0.1f) < ToyCarMotion.ApproachSpeed(0f, 0.5f), "Brake approaching destination");

foreach (float length in new[] { 0.1f, 1f, 1.41421356f, 12f })
{
    float travelled = 0f;
    speed = 0f;
    int ticks = 0;
    while (length - travelled > ToyCarMotion.ArrivalDistance && ticks++ < 2000)
    {
        float target = ToyCarMotion.ApproachSpeed(0f, length - travelled);
        float next = ToyCarMotion.ChangeSpeed(speed, target);
        Check(next <= ToyCarMotion.MaxSpeed, "Speed cap");
        Check(Math.Abs(next - speed) <= ToyCarMotion.Braking + 0.00001f, "No abrupt speed change");
        speed = next;
        travelled = Math.Min(length, travelled + speed);
    }
    Check(ticks < 2000, "Must arrive without asymptotic creep");
    Check(speed < ToyCarMotion.MaxSpeed * 0.15f, "Must brake before stopping");
    Console.WriteLine($"Arrived over {length} cells in {ticks} ticks");
}
Console.WriteLine("Toy car motion checks passed.");
