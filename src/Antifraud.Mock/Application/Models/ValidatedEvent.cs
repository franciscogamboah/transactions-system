using System;

namespace Antifraud.Mock.Application.Models;

public sealed record ValidatedEvent(
    string transactionExternalId,
    string status,
    string? reason,
    DateTime evaluatedAt
);
