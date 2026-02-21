namespace PostgresLib.Interfaces
{
    public interface IDateTimeTrackable
    {
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
