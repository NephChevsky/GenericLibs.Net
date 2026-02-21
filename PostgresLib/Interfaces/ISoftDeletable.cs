namespace PostgresLib.Interfaces
{
    public interface ISoftDeletable
    {
        public bool IsDeleted { get; set; }
    }
}
