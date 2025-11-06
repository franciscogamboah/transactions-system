namespace Antifraud.Mock.Config;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string TopicCreated { get; set; } = "transactions.created.v1";
    public string TopicValidated { get; set; } = "transactions.validated.v1";
    public string GroupId { get; set; } = "antifraud-mock";
    public string AutoOffsetReset { get; set; } = "Earliest"; // Earliest | Latest
    public bool EnableAutoCommit { get; set; } = false;       // queremos commit manual
}
