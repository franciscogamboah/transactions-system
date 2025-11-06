using System;
using Antifraud.Mock.Application.Abstractions;

namespace Antifraud.Mock.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
