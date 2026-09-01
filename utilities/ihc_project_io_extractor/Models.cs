namespace Ihc.IOExtractor {
    public enum IOType { Input, Output };

    /**
    * IHC resource Input/Output information.
    */
    public record IOMeta {
        public int ResourceId { get; init; }
        public required string DatalineName { get; init; }
        public required string DatalineNote { get; init; }
        public int ProductId { get; init; }
        public required string ProductName { get; init; }
        public required string ProductPosition { get; init; }
        public required string ProductNote { get; init; }
        public int GroupId { get; init; }
        public required string GroupName { get; init; }
    };
}
  