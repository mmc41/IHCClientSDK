namespace Ihc.IOExtractor {
    public enum IOType { Input, Output };

    /**
    * IHC resource Input/Output information.
    */
    public record IOMeta {
        public int ResourceId { get; init; }
        public string DatalineName { get; init; }
        public string DatalineNote { get; init; }
        public int ProductId { get; init; }
        public string ProductName { get; init; }
        public string ProductPosition { get; init; }
        public string ProductNote { get; init; }
        public int GroupId { get; init; }
        public string GroupName { get; init; }
    };
}
  