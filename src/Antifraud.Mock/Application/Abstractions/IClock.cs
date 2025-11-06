using System;

namespace Antifraud.Mock.Application.Abstractions;

public interface IClock { DateTime UtcNow { get; } }
