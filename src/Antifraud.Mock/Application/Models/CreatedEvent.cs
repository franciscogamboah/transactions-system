using System;

namespace Antifraud.Mock.Application.Models;

public sealed record CreatedEvent(
    string transactionExternalId,
    string sourceAccountId,
    string targetAccountId,
    int transferTypeId,
    decimal value,
    DateTime createdAt
);
