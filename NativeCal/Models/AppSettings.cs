using SQLite;

namespace NativeCal.Models
{
    [Table("AppSettings")]
    public class AppSettings
    {
        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }

        [NotNull, Unique, MaxLength(256), Column("Key")]
        public string Key { get; set; } = string.Empty;

        [MaxLength(2048), Column("Value")]
        public string Value { get; set; } = string.Empty;
    }
}
