namespace Transactions.Domain;

public enum TransactionStatus { Pending, Approved, Rejected }

public sealed class Transaction
{
    public Guid ExternalId { get; }
    public Guid SourceAccountId { get; }
    public Guid TargetAccountId { get; }
    public int TransferTypeId { get; }
    public decimal Value { get; }
    public TransactionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // ctor para Create()
    private Transaction(Guid id, Guid s, Guid t, int type, decimal value, TransactionStatus status, DateTimeOffset now)
    {
        ExternalId = id; SourceAccountId = s; TargetAccountId = t; TransferTypeId = type;
        Value = value; Status = status; CreatedAt = now; UpdatedAt = now;
    }

    // ctor para Restore() (desde la BD)
    private Transaction(Guid id, Guid s, Guid t, int type, decimal value, TransactionStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        ExternalId = id; SourceAccountId = s; TargetAccountId = t; TransferTypeId = type;
        Value = value; Status = status; CreatedAt = createdAt; UpdatedAt = updatedAt;
    }

    public static Transaction Create(Guid s, Guid t, int type, decimal value, DateTimeOffset? now = null)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        var ts = now ?? DateTimeOffset.UtcNow;
        return new Transaction(Guid.NewGuid(), s, t, type, value, TransactionStatus.Pending, ts);
    }

    public static Transaction Restore(Guid id, Guid s, Guid t, int type, decimal value, TransactionStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        => new Transaction(id, s, t, type, value, status, createdAt, updatedAt);

    public void Approve() { Status = TransactionStatus.Approved; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Reject() { Status = TransactionStatus.Rejected; UpdatedAt = DateTimeOffset.UtcNow; }
}
