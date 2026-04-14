namespace SampleApp.Attributes;

using System;

/// <summary>
/// A custom attribute — has NO dependency on OrderService.
/// EXPECTED: NOT a fan-in.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class OrderAttribute : Attribute
{
    public string Description { get; }

    public OrderAttribute(string description)
    {
        Description = description;
    }
}
