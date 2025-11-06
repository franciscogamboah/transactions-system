using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Transactions.Api.BackgroundWorkers;
using Transactions.Application.Abstractions;
using Transactions.Domain;
using Xunit;

namespace Transactions.Api.Tests;

public class ValidatedEventProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenStatusApproved_CallsUpdateWithApproved()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payload = $"{{\"transactionExternalId\":\"{id}\",\"status\":\"approved\",\"reason\":\"ok\",\"evaluatedAt\":\"2025-11-06T00:00:00Z\"}}";

        var repoMock = new Mock<ITransactionRepository>();
        repoMock.Setup(r => r.UpdateStatusAsync(id, TransactionStatus.Approved, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .Verifiable();

        var processor = new ValidatedEventProcessor(repoMock.Object, NullLogger<ValidatedEventProcessor>.Instance);

        // Act
        var result = await processor.ProcessAsync(payload, CancellationToken.None);

        // Assert
        Assert.True(result);
        repoMock.Verify();
    }

    [Fact]
    public async Task ProcessAsync_WhenPayloadEmpty_ReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<ITransactionRepository>();
        var processor = new ValidatedEventProcessor(repoMock.Object, NullLogger<ValidatedEventProcessor>.Instance);

        // Act
        var result = await processor.ProcessAsync(string.Empty, CancellationToken.None);

        // Assert
        Assert.False(result);
        repoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<TransactionStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenIdInvalid_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"transactionExternalId\":\"not-a-guid\",\"status\":\"approved\",\"reason\":\"ok\"}";
        var repoMock = new Mock<ITransactionRepository>();
        var processor = new ValidatedEventProcessor(repoMock.Object, NullLogger<ValidatedEventProcessor>.Instance);

        // Act
        var result = await processor.ProcessAsync(payload, CancellationToken.None);

        // Assert
        Assert.False(result);
        repoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<TransactionStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenStatusUnknown_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payload = $"{{\"transactionExternalId\":\"{id}\",\"status\":\"unknown\",\"reason\":\"ok\"}}";
        var repoMock = new Mock<ITransactionRepository>();
        var processor = new ValidatedEventProcessor(repoMock.Object, NullLogger<ValidatedEventProcessor>.Instance);

        // Act
        var result = await processor.ProcessAsync(payload, CancellationToken.None);

        // Assert
        Assert.False(result);
        repoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<TransactionStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenInvalidJson_ReturnsFalse()
    {
        // Arrange
        var payload = "not-a-json";
        var repoMock = new Mock<ITransactionRepository>();
        var processor = new ValidatedEventProcessor(repoMock.Object, NullLogger<ValidatedEventProcessor>.Instance);

        // Act
        var result = await processor.ProcessAsync(payload, CancellationToken.None);

        // Assert
        Assert.False(result);
        repoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<TransactionStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenStatusRejected_CallsUpdateWithRejected()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payload = $"{{\"transactionExternalId\":\"{id}\",\"status\":\"rejected\",\"reason\":\"fraud\",\"evaluatedAt\":\"2025-11-06T00:00:00Z\"}}";

        var repoMock = new Mock<ITransactionRepository>();
        repoMock.Setup(r => r.UpdateStatusAsync(id, TransactionStatus.Rejected, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .Verifiable();

        var processor = new ValidatedEventProcessor(repoMock.Object, NullLogger<ValidatedEventProcessor>.Instance);

        // Act
        var result = await processor.ProcessAsync(payload, CancellationToken.None);

        // Assert
        Assert.True(result);
        repoMock.Verify();
    }
}
